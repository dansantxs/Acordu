# Acordu — Estratégia de Deploy e CI/CD (v1)

| | |
|---|---|
| **Versão** | 0.2 |
| **Data** | 05/10/2026 |
| **Alvo** | Nível 0 do ADR 0005: API no **Google Cloud Run** (`us-east4`, projeto `acordu-prod`), banco **Neon** (Postgres 17, `aws-us-east-1`), SPA no **Cloudflare Pages**. |
| **Base** | ADR 0005. Provisionamento em `setup-infra-acordu.md`. |

> A versão 0.1 descrevia a VM Oracle compartilhada com o Saldu, que deixou de existir. O modelo de VM com Caddy + Postgres compartilhados volta só no nível 3 (ADR 0005 §8).

---

## 1. Princípios

- **Custo zero até ter usuário pagante**, com proteções (alerta de orçamento, `max-instances` baixo, limpeza de imagens e lifecycle de buckets).
- **Sem SDK de provedor; tudo por variável de ambiente.** Mudar de nível ou de provedor troca variáveis e DNS, não código.
- **Nada de chave de longa duração no GitHub:** deploy por Workload Identity Federation.
- **Migrations são etapa explícita do pipeline**, nunca no startup da API.

---

## 2. Topologia

```
                 HTTPS                                   HTTPS
 navegador ───────────────► Cloudflare Pages  (acordu.app.br)  SPA estático
     │
     └──────── /api ──────► Cloud Run  acordu-api  (api.acordu.app.br, us-east4)
                               │  endpoint pooled (acordu_app)
                               ├──────────────► Neon · Postgres 17 · aws-us-east-1
                               ├──────────────► Cloud Storage  acordu-anexos  (API S3, HMAC)
                               └──────────────► Resend

 GitHub Actions ── WIF ──► Artifact Registry / Cloud Run deploy
                └─ endpoint direto (acordu_migrator) ─► Neon   (migrations, pg_dump)
 Cron (Actions / Cloud Scheduler) ── X-Job-Token ──► /internal/jobs/*
 UptimeRobot ── a cada 5 min ──► /api/health   (não toca o banco)
```

- **Domínios:** `acordu.app.br` no Pages; `api.acordu.app.br` no Cloud Run (mapeamento de domínio do Cloud Run; se indisponível em `us-east4`, proxy de `/api/*` por uma Pages Function para a URL `*.run.app`). Com SPA e API em subdomínios do mesmo site, o CORS da API aceita só a origem do SPA, com credenciais.
- **Headers de segurança:** HSTS, CSP e afins vêm do arquivo `_headers` do Pages (SPA) e de middleware na API.

### 2.1 Configuração do serviço Cloud Run

| Parâmetro | Valor (nível 0) | Por quê |
|---|---|---|
| Região | `us-east4` | Tier 1 (free tier); mais perto do Neon `aws-us-east-1`. |
| CPU / memória | 1 vCPU / 512 MiB | Suficiente para .NET com ReadyToRun; medir e ajustar. |
| Cobrança de CPU | Por requisição (padrão) | Fora de requisição a CPU é estrangulada: por isso jobs são endpoints. |
| `min-instances` / `max-instances` | 0 / 2 | Escala a zero; teto contra pico ou abuso. Nível 1: `min-instances=1`. |
| Concorrência | 80 (padrão) | |
| Startup CPU boost | Ligado | Reduz cold start. |
| Acesso | Público (`allUsers` invoker) | A API é pública; autenticação na aplicação. Jobs protegidos por token. |
| Service account | `acordu-api@acordu-prod` | Só acesso de objeto ao bucket de anexos (via HMAC dessa conta). |

### 2.2 Imagem

- `docker/api.Dockerfile`: build com SDK, `PublishReadyToRun=true`, runtime `linux-x64`; base final `aspnet:10.0` **chiseled extra** (traz **ICU e tzdata**, exigidos pelo SLA com fusos IANA como `America/Sao_Paulo`), usuário não-root, porta `8080`.
- Imagem pequena = pull e cold start menores. Medir tamanho no PR do Dockerfile.

---

## 3. CI (`ci.yml`) — a cada push/PR

- **Backend:** restore → build → todos os testes (incluindo `ArchitectureTests`); integração com Testcontainers (`postgres:17`, mesma versão do Neon). O teste do object storage usa um emulador S3 em container no runner (escolha no marco de anexos).
- **Frontend:** install → lint → Vitest → build.
- Merge em `main` só com tudo verde e PR revisado.

---

## 4. Deploy (`deploy.yml`) — na `main`

1. **Autenticação** no Google por **Workload Identity Federation** (`google-github-actions/auth`), restrita ao repositório `dansantxs/acordu` e à branch `main`, impersonando `acordu-deployer@acordu-prod`.
2. **Build e push** da imagem para `us-east4-docker.pkg.dev/acordu-prod/acordu/acordu-api`, tag = SHA do commit.
3. **Migrations:** migration bundle do EF Core contra o **endpoint direto** do Neon, com o usuário `acordu_migrator`. Falhou → para aqui, a revisão em produção continua a anterior.
4. **Deploy** da revisão nova no Cloud Run (`gcloud run deploy --image ...:<sha>`), com as variáveis de ambiente.
5. **Healthcheck** `GET /api/health` na URL da revisão; falhou → tráfego volta para a revisão anterior (`gcloud run services update-traffic --to-revisions`).
6. **SPA:** build de `apps/web` e publicação no Cloudflare Pages (integração Git do Pages ou `wrangler pages deploy` com token restrito ao projeto).
7. Discord: falha sempre; sucesso com a tag.

**Artifact Registry:** política de limpeza mantém as **5 imagens mais recentes** e apaga as sem tag com mais de 7 dias — a cota gratuita de armazenamento é pequena.

---

## 5. Jobs agendados

A API escala a zero, então não há processo vivo para `HostedService`. Cada job é um **endpoint interno** (`POST /internal/jobs/<nome>`):
- cabeçalho `X-Job-Token` comparado em tempo constante com `JOBS__TOKEN`; ausente/errado → 404 (não revela a rota);
- fora do OpenAPI e dos documentos interno/portal;
- **idempotente** e processa todos os tenants; responde com contagens (sem dados de cliente).

| Job | Frequência | Disparador |
|---|---|---|
| `alertas-sla` + varredura do outbox de e-mail (`tick`) | a cada 5 min | **Cloud Scheduler** (1 dos 3 jobs gratuitos da conta `utile-billing`, divididos com o Saldu) |
| `recorrencias` | diário, 03:00 BRT | GitHub Actions `schedule` (`jobs.yml`) |
| `fechamento-automatico` | diário, 03:10 BRT | GitHub Actions `schedule` |
| Backup `pg_dump` | diário, 04:00 BRT | GitHub Actions `schedule` (não chama a API) |

Por que não tudo no Actions: o `schedule` atrasa minutos e cada execução consome minutos do repositório privado — inviável a cada 5 min. Por que não tudo no Scheduler: só 3 jobs gratuitos para os dois produtos.

O outbox é despachado logo após o commit da requisição que gerou o e-mail; o `tick` só recolhe o que falhou ou sobrou.

---

## 6. Banco de dados (Neon)

- Projeto Neon **próprio do Acordu** (`acordu`), Postgres 17, `aws-us-east-1`; banco `acordu`.
- Dois usuários: `acordu_migrator` (dono do schema, DDL, só no Actions) e `acordu_app` (DML, usado pela API).
- **A API usa o endpoint pooled** (`...-pooler...`, PgBouncer em modo transação), com `SSL Mode=Require`. Pool do Npgsql pequeno (`Maximum Pool Size=10`): o limite real é o do pooler.
- **Migrations e `pg_dump` usam o endpoint direto:** o lock de sessão das migrations e comandos de sessão não funcionam pelo pooler em modo transação.
- Extensões `pg_trgm` e `unaccent` criadas pela migration (disponíveis no Neon).
- **O `/api/health` não consulta o banco.** Ping no banco a cada 5 min manteria a computação acordada e consumiria as **100 CU-h/mês** do plano Free.

---

## 7. Segredos e configuração

- **GitHub Actions (repositório acordu):** nenhuma chave do Google (WIF). Secrets: connection string direta do `acordu_migrator`, variáveis da API (abaixo), token do Cloudflare Pages, webhook do Discord.
- **Variáveis da API no Cloud Run** (definidas no deploy): `ConnectionStrings__Acordu` (pooled, `acordu_app`), `Jwt__ChaveAssinatura` (**própria**, não a do Saldu), `Resend__ApiKey`, `Armazenamento__Endpoint` (`https://storage.googleapis.com`), `Armazenamento__AccessKey` / `__SecretKey` (HMAC da `acordu-api`), `Armazenamento__BucketAnexos`, `JOBS__TOKEN`, `Sentry__Dsn`, `Cors__Origem`.
- Secret Manager fica como evolução (a cota gratuita também é por conta de faturamento).

---

## 8. Backup e restore

- **Banco:** `pg_dump -Fc` (cliente **17**, imagem `postgres:17` no runner) no endpoint direto → bucket privado **`acordu-backups`**, autenticado por WIF com `acordu-backup@acordu-prod` (só criação de objeto). Retenção de 14 dias por lifecycle do bucket.
- **Anexos:** `acordu-anexos` com versionamento e lifecycle que apaga versões não atuais após 30 dias.
- **Restore testado** antes do primeiro cliente real: `pg_restore` num branch do Neon (ou Postgres local) e API apontada para ele.
- Discord: falha sempre; heartbeat diário de sucesso.

---

## 9. Monitoramento

- **UptimeRobot** em `https://api.acordu.app.br/api/health` a cada 5 min (mantém a instância quente e avisa queda).
- **Pré-aquecimento pela UI:** o SPA chama `/api/health` ao abrir o login/portal, sem esperar.
- Sentry (erros), Discord (deploy, jobs, backup), alerta de orçamento do Google (e-mail).
- Acompanhar mensalmente: armazenamento e CU-h no painel do Neon; uso do Cloud Run na conta `utile-billing` (somado ao Saldu). São os gatilhos dos níveis 1–2 do ADR 0005.

---

## 10. Checklist de riscos

- [ ] Alerta de orçamento na `utile-billing` filtrado por `acordu-prod`.
- [ ] `max-instances=2` aplicado.
- [ ] Limpeza do Artifact Registry e lifecycle dos buckets configurados.
- [ ] Imagem da API com ICU e tzdata (teste de fumaça resolvendo `America/Sao_Paulo` dentro do container).
- [ ] `/api/health` sem acesso ao banco (coberto por teste funcional).
- [ ] Endpoints `/internal/jobs/*` respondem 404 sem token e são idempotentes.
- [ ] WIF restrito a `dansantxs/acordu` na `main`; nenhuma chave JSON de service account criada.
- [ ] `acordu_app` sem permissão de DDL; app só no endpoint pooled.
- [ ] Chave JWT própria do Acordu.
- [ ] Buckets privados; HMAC restrito ao bucket de anexos.
- [ ] Backup diário + restore testado.
- [ ] Domínio de envio verificado no Resend (SPF/DKIM) antes do primeiro e-mail a cliente.
- [ ] Política de privacidade informa dados hospedados nos EUA (LGPD, transferência internacional).
