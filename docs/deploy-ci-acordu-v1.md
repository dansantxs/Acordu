# Acordu — Estratégia de Deploy e CI/CD (v1)

| | |
|---|---|
| **Versão** | 0.5 |
| **Data** | 03/11/2026 |
| **Alvo** | Nível 0 do ADR 0010: SPA no **Cloudflare Pages** (`acordu.app.br`), borda da API num **Worker** (`api.acordu.app.br`), API no **Google Cloud Run** (`us-east4`, projeto `acordu-prod`), banco **Neon** (Postgres 17, `aws-us-east-1`), arquivos no **Cloudflare R2**. |
| **Base** | ADR 0010. Provisionamento em `setup-infra-acordu.md`. |

> A versão 0.3 (06/10/2026) descrevia a VM Oracle compartilhada do ADR 0006, revogado em 26/10/2026. Está no histórico do git.

---

## 1. Princípios

- **Custo zero até ter usuário pagante**, com proteções: alerta de orçamento, `max-instances` baixo, limpeza de imagens, lifecycle no R2 e acompanhamento mensal das cotas.
- **Sem SDK de provedor; tudo por variável de ambiente.** Mudar de nível ou de provedor troca variáveis e DNS, não código.
- **Nada de chave do Google no GitHub:** deploy por Workload Identity Federation.
- **Migrations são etapa explícita do pipeline**, nunca no startup da API, e sempre aditivas.
- **PostgreSQL 17 em todos os ambientes.**

---

## 2. Topologia

```
 navegador ── HTTPS ──► Cloudflare Pages      acordu.app.br        SPA estático
     │
     └──── HTTPS ─────► Cloudflare Worker     api.acordu.app.br    proxy + X-Origem-Token
                            │
                            ▼
                        Cloud Run  acordu-api  (us-east4, *.run.app)
                            ├─ endpoint pooled (acordu_app) ──► Neon · Postgres 17 · aws-us-east-1
                            ├─ API S3 (região auto) ──────────► R2  acordu-anexos
                            └─ HTTP ──────────────────────────► Resend

 GitHub Actions ── WIF ──► Artifact Registry / Cloud Run
                ├─ endpoint direto (acordu_migrator) ─► Neon   (migrations, pg_dump)
                └─ token R2 só de escrita ────────────► R2  acordu-backups
 Cloud Scheduler ── X-Job-Token ──► api.acordu.app.br/internal/jobs/{tick,diario}
 UptimeRobot ── a cada 5 min ──► api.acordu.app.br/api/health   (não toca o banco)
```

### 2.1 Worker da borda (`acordu-api-borda`)

Proxy fino, sem lógica de negócio:
- repassa método, caminho, query, corpo e cabeçalhos para `https://<serviço>.run.app`, sem buffer (o corpo vai em stream; uploads de até 20 MB);
- acrescenta `X-Origem-Token` (segredo do Worker) e grava `CF-Connecting-IP` em `X-Forwarded-For`, descartando o que o cliente mandou nesse cabeçalho;
- não armazena em cache nenhuma resposta da API;
- código em `infra/cloudflare/api-borda/` (TypeScript, `wrangler`), publicado pelo `deploy.yml`; `ORIGEM` (variável) e `TOKEN_ORIGEM` (segredo) definidos no provisionamento; sem eles, o Worker responde 500 em vez de repassar sem o token;
- testado na CI (job **Borda (Worker)**: tipos, testes do repasse e empacotamento com `wrangler deploy --dry-run`).

Com `Proxy__TokenOrigem` definido, a API responde 404 a requisição sem o token (inclusive `/api/health` e `/internal/jobs/*`), antes de proxy, autenticação e rate limit, e retira o cabeçalho depois de conferir. A URL `*.run.app` deixa de ser porta de entrada. `Proxy__Saltos` só passa de 1 com o token ativo.

### 2.2 Configuração do serviço Cloud Run

| Parâmetro | Valor (nível 0) | Por quê |
|---|---|---|
| Região | `us-east4` | Tier 1 (free tier); perto do Neon `aws-us-east-1`. |
| CPU / memória | 1 vCPU / 512 MiB | Suficiente para .NET com ReadyToRun; medir e ajustar. |
| Cobrança | Por requisição (padrão) | Fora de requisição a CPU é estrangulada: por isso jobs são endpoints e a outbox sai antes da resposta. |
| `min-instances` / `max-instances` | 0 / 2 | Escala a zero; teto contra pico ou abuso. Nível 1: `min-instances=1`. |
| Startup CPU boost | Ligado | Reduz cold start. |
| Acesso | Público (`allUsers` invoker) | Autenticação na aplicação; borda protegida pelo token de origem; jobs pelo `X-Job-Token`. |
| Service account | `acordu-api@acordu-prod` | Sem papéis: a API não chama nenhum serviço do Google. |

### 2.3 Imagem

- `docker/api.Dockerfile`: build com SDK, `PublishReadyToRun=true`, `linux-x64`; base final `aspnet:10.0` **chiseled extra** (traz ICU e tzdata, exigidos pelo SLA com fusos IANA como `America/Sao_Paulo`), usuário não-root, porta `8080`.
- Plataforma **`linux/amd64`**: o Cloud Run não executa ARM, e o runner do Actions já é x64 (sem QEMU).
- **A API confere o ambiente ao subir** (`AmbienteDeExecucao`): o fuso `America/Sao_Paulo` (tzdata) e a cultura `pt-BR` (ICU). Se faltar algum, ela nem sobe, e o healthcheck do deploy volta para a revisão anterior.
- Teste de fumaça no CI (job **Imagem da API**): build `linux/amd64`, usuário da imagem diferente de root e `/api/health` respondendo sem banco (o que também prova fuso e ICU, pela conferência acima).

---

## 3. CI (`ci.yml`) — no PR e na `main`

**Quando roda (economia de minutos, spec do F8, F8a):**
- Branch de feature roda **só pelo PR** contra a `main`; o push da branch não dispara de novo. Para validar algo antes da revisão, abre-se o PR como rascunho.
- No PR, o job `O que mudou` (`dorny/paths-filter`) decide: **backend** com `src/`, `tests/`, `openapi/` (o `ContratoOpenApiTests`), solução, `global.json`, `Directory.*.props` ou `.config/`; **front** com `apps/web/` ou `openapi/`; **E2E** quando qualquer um dos dois muda; **imagem** com o código da API (`src/`, solução, `global.json`, `Directory.*.props`), `docker/` ou `.dockerignore`; **borda** com `infra/cloudflare/`. Mudar `ci.yml`, `.github/actions/`, `.editorconfig` ou `.gitattributes` roda tudo. Job pulado conta como verde; o job **`CI ok`** falha se algum job falhou ou foi cancelado, e é o check a exigir na proteção da `main`. PR só de documentos não roda nenhum job pesado.
- Na `main`, um run não cancela o anterior (no PR, sim).
- Na `main` (merge) e no disparo manual, roda tudo.

- **Backend:** restore → build → todos os testes (incluindo `ArchitectureTests`); integração com Testcontainers (`postgres:17`, mesma versão do Neon). O teste do object storage usa o emulador Adobe S3Mock (Testcontainers); os testes funcionais usam armazenamento em memória.
- **Frontend:** install → lint → Vitest → build.
- **Imagem:** build do `api.Dockerfile` e teste de fumaça (§2.3), sem push.
- Merge em `main` só com tudo verde e PR revisado.

---

## 4. Deploy (`deploy.yml`) — na `main`

**Disparo manual** (`workflow_dispatch`, só na `main`) até haver cliente pagante; depois, automático a cada merge (spec do F8, decisão 1, 03/11/2026). O plano Free com repositório privado não tem aprovação obrigatória em *environments*.


0. **CI verde:** sem o check `CI ok` verde no commit da `main`, o deploy não começa (o disparo manual não espera a CI).
1. **Autenticação** no Google por WIF (`google-github-actions/auth`), restrita a `dansantxs/acordu` na `main`, impersonando `acordu-deployer@acordu-prod`.
2. **Build e push** para `us-east4-docker.pkg.dev/acordu-prod/acordu/acordu-api`, tag = SHA do commit.
3. **Migrations:** migration bundle do EF Core no **endpoint direto** do Neon, com `acordu_migrator`. Falhou → para aqui; a revisão em produção continua a anterior.
4. **Nova revisão** no Cloud Run (`gcloud run deploy --image ...:<sha>`) com as variáveis do §7, num arquivo (`--env-vars-file`) que substitui todas as da revisão anterior; depois, `update-traffic --to-latest`, que desfaz um rollback anterior.
5. **Healthcheck** `GET /api/health` pela borda; falhou → tráfego de volta à revisão anterior (`gcloud run services update-traffic --to-revisions`). Até a borda apontar para o serviço (variável `BORDA_ATIVA=true`, runbook C4), confere direto na URL `*.run.app`, com o `X-Origem-Token`. Só "serviço não existe" conta como primeiro deploy; outro erro ao ler o serviço para o deploy, para não desligar o rollback.
6. **Worker:** `wrangler deploy` de `infra/cloudflare/api-borda` (`keep_vars`: `ORIGEM` e `TOKEN_ORIGEM` ficam como estão).
7. **SPA:** build de `apps/web` com `VITE_URL_API=https://api.acordu.app.br` e `VITE_TEMPO_REAL=polling`, publicado no Pages (`wrangler pages deploy` no projeto `acordu-web`, criado no primeiro deploy). O token do Cloudflare tem o mínimo de conta e zona (runbook C3); o Cloudflare não restringe token a um script ou projeto.
8. Discord: falha sempre; sucesso com a tag.

---

## 5. Jobs agendados

Cada job é um endpoint interno (`POST /internal/jobs/<nome>`): `X-Job-Token` comparado em tempo constante, 404 sem token válido, fora do OpenAPI, idempotente, todos os tenants por chamada. Jobs por tenant rodam cada tenant num escopo próprio; a falha de um é logada, não para os demais e entra em `falhas`.

| Job do Scheduler | Agenda (`America/Sao_Paulo`) | Endpoint | Faz |
|---|---|---|---|
| `acordu-tick` | `*/15 7-19 * * 1-6` | `/internal/jobs/tick` | Tarefas recorrentes, outbox pendente e alertas de SLA. Responde `{ tarefasRecorrentes, outboxProcessadas, alertasSla, falhas }`. |
| `acordu-diario` | `40 6 * * *` | `/internal/jobs/diario` | Roda em sequência `fechamento-automatico`, `expurgo-auditoria`, `expurgo-notificacoes` (que apaga também o feed de atividade com mais de 90 dias) e `expurgo-cadastros` (empresa não liberada há 7 dias ou mais e sem dados, RF-AUT-07). Responde `{ fechadas, auditoriaRemovida, notificacoesRemovidas, atividadesRemovidas, cadastrosRemovidos, falhas }`; um passo que falha é logado, conta em `falhas` e não impede os seguintes. |

- Os endpoints diários individuais continuam existindo para disparo manual.
- Retry do Scheduler com backoff curto; o próximo disparo cobre o anterior.
- **Por que essa frequência:** cada tick acorda o Neon por ~5 min; a cada 15 min no expediente, o banco fica ligado ~1/3 das 13 horas, dentro das 100 CU-h (ADR 0010 §5).
- Resposta não-2xx: política de alerta do Cloud Monitoring sobre as tentativas falhas do Scheduler, com aviso por e-mail (conferir no provisionamento que cabe na cota gratuita).
- **Backup** fica no Actions (§8), às 04:00, e não chama a API.

---

## 6. Banco de dados (Neon)

- Projeto `acordu`, Postgres 17, `aws-us-east-1`; banco `acordu`. Autoscaling de 0,25 a 1 CU; suspensão padrão do Free.
- Dois usuários: `acordu_migrator` (dono do schema, DDL, só no Actions) e `acordu_app` (DML, usado pela API).
- **A API usa o endpoint pooled** (`-pooler`, PgBouncer em modo transação), com `SSL Mode=Require` e `Maximum Pool Size=10`.
- **Migrations e `pg_dump` usam o endpoint direto.**
- Extensões `pg_trgm` e `unaccent` criadas pela migration (disponíveis no Neon).
- **`/api/health` não consulta o banco.**

---

## 7. Segredos e configuração

- **GitHub Actions:** nenhuma chave do Google (WIF). Nomes das variáveis e dos secrets no runbook ("Segredos do GitHub"). Variáveis `GCP_WIF_PROVIDER`, `GCP_PROJECT_ID` e `BACKUP_ATIVO`. Secrets: connection string direta do `acordu_migrator`, variáveis da API (abaixo), token do Cloudflare (Pages + Worker), token R2 de backup, webhook do Discord.
- **Variáveis da API no Cloud Run:**
  - `ConnectionStrings__Acordu` (pooled, `acordu_app`);
  - `Jwt__ChaveAssinatura`; `Jobs__Token`;
  - `Cors__Origem=https://acordu.app.br`;
  - `App__UrlPublica=https://acordu.app.br/` (links dos e-mails) e `App__UrlApi=https://api.acordu.app.br/` (feed ICS da agenda);
  - `Proxy__ConfiarEncaminhamento=true`, `Proxy__Saltos=2`, `Proxy__TokenOrigem` (o mesmo segredo do Worker);
  - `TempoReal__Sse=false`;
  - `Email__ResendChave`, `Email__Remetente` (padrão `nao-responda@send.acordu.app.br`), `Email__NomeRemetente`;
  - `Armazenamento__Endpoint=https://<conta>.r2.cloudflarestorage.com`, `Armazenamento__Regiao=auto`, `Armazenamento__Bucket=acordu-anexos`, `Armazenamento__ChaveAcesso` / `Armazenamento__ChaveSecreta` (token R2 de leitura e escrita só em `acordu-anexos`);
  - limites opcionais (`RateLimit__Auth__PorMinuto` 10, `RateLimit__Refresh__PorMinuto` 30, `RateLimit__Portal__PorMinuto` 10, `RateLimit__Agenda__PorMinuto` 30);
  - (Sentry fora do F8: o SDK é dependência de fornecedor; erros de aplicação ficam nos logs do Cloud Run até uma decisão própria, spec do F8, decisão 3.)
- **Segredo do Worker:** `TOKEN_ORIGEM`; variável `ORIGEM` com a URL `*.run.app`.
- **Risco aceito (nível 0):** os secrets de produção são do repositório, não de um *environment* com restrição de branch (o plano Free com repositório privado não oferece). O WIF limita o Google à `main`, mas um workflow alterado em qualquer branch do próprio repositório alcança Neon (migrator), R2 e Cloudflare. A guarda `if: github.ref == 'refs/heads/main'` do `deploy.yml` evita engano, não abuso. Mitigação: só o Daniel tem escrita no repositório, com MFA; PRs de fork não recebem secrets. Revisar quando houver outro colaborador ou cliente pagante (environment protegido ou Secret Manager).
- Secret Manager fica como evolução.

---

## 8. Backup e restore

- **Banco:** `backup.yml`, às 04:00 (`0 7 * * *` UTC), só com `BACKUP_ATIVO=true`: `pg_dump -Fc` (imagem `postgres:17` no runner, confere o arquivo com `pg_restore --list`) no endpoint direto → `acordu-backups/db/AAAA-MM-DD.dump` no R2, com token que só escreve nesse bucket. Retenção de 14 dias por lifecycle do bucket.
- **Anexos:** o R2 não tem versionamento. A aplicação só apaga objeto ao trocar o logotipo do portal, e o token da API só alcança `acordu-anexos`. Cópia fora do Cloudflare quando houver cliente pagante (ADR 0010 §8).
- **Restore testado** antes do primeiro cliente real: `pg_restore` num branch do Neon ou num Postgres 17 local, com a API apontada para ele (passo a passo no runbook, "Restore").
- Discord: falha sempre; heartbeat diário de sucesso.

---

## 9. Monitoramento

- **UptimeRobot** em `https://api.acordu.app.br/api/health` a cada 5 min (avisa queda e mantém a instância quente parte do tempo).
- **Pré-aquecimento pela UI:** ao carregar qualquer tela o SPA já renova a sessão (`POST /api/v1/auth/refresh`), o que acorda o container da API antes de a pessoa digitar; por isso não há chamada extra ao `/api/health` (F8d). O banco acorda na primeira requisição que o consulta (com cookie, a própria renovação; sem cookie, o login).
- Logs do Cloud Run (erros), Discord (deploy e backup), alerta do Cloud Monitoring para os jobs e de orçamento (e-mail). Monitoramento de erros de aplicação (Sentry ou OpenTelemetry) fica para decisão própria.
- **Mensal:** CU-h e armazenamento no Neon; vCPU-s no Cloud Run; requisições/dia no Worker; armazenamento no R2. São os gatilhos dos níveis 1 e 2 do ADR 0010.

---

## 10. Checklist de riscos

- [ ] Alerta de orçamento na `nebrys-billing`.
- [ ] `max-instances=2` aplicado.
- [ ] Limpeza do Artifact Registry e lifecycle de `acordu-backups`.
- [ ] Imagem amd64 com ICU e tzdata (teste de fumaça).
- [ ] `/api/health` sem acesso ao banco (teste funcional).
- [ ] `/internal/jobs/*` com 404 sem token e idempotentes; `/diario` implementado.
- [ ] API responde 404 sem `X-Origem-Token` em produção; rate limit por IP real conferido pela borda.
- [ ] CORS só para `https://acordu.app.br`; login, refresh e logout funcionando entre os subdomínios.
- [ ] `TempoReal__Sse=false` e polling ativo no SPA.
- [ ] WIF restrito a `dansantxs/acordu` na `main`; nenhuma chave JSON de service account.
- [ ] Escrita no repositório só do Daniel (secrets de repositório, risco aceito no §7).
- [ ] `acordu_app` sem DDL; app só no endpoint pooled.
- [ ] Tokens R2 separados (anexos para a API, escrita em backups para o Actions).
- [ ] Backup diário + restore testado.
- [ ] Domínio de envio verificado no Resend (SPF/DKIM) antes do primeiro e-mail a cliente.
- [ ] Política de privacidade informa dados hospedados nos EUA (LGPD, transferência internacional).
- [ ] MFA com códigos de recuperação em Google, Neon, Cloudflare, GitHub e Resend.
