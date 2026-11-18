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
 Worker vigia ── a cada 5 min ──► api.acordu.app.br/api/health   (não toca o banco; queda → #alertas)
```

### 2.1 Worker da borda (`acordu-api-borda`)

Proxy fino, sem lógica de negócio:
- repassa método, caminho, query, corpo e cabeçalhos para `https://<serviço>.run.app`, sem buffer (o corpo vai em stream; uploads de até 20 MB);
- acrescenta `X-Origem-Token` (segredo do Worker) e grava `CF-Connecting-IP` em `X-Forwarded-For`, descartando o que o cliente mandou nesse cabeçalho;
- não armazena em cache nenhuma resposta da API;
- código em `infra/cloudflare/api-borda/` (TypeScript, `wrangler`), publicado pelo `deploy.yml`; `ORIGEM` (variável) e `TOKEN_ORIGEM` (segredo) definidos no provisionamento; sem eles, o Worker responde 500 em vez de repassar sem o token;
- testado na CI (job **Worker (api-borda)**: tipos, testes do repasse e empacotamento com `wrangler deploy --dry-run`).

### 2.1a Worker da vigia (`acordu-vigia`, F13h, ADR 0012)

- Cron Trigger de 5 min: `GET https://api.acordu.app.br/api/health` pela internet e pela borda, como um cliente (`global_fetch_strictly_public`), com 15 s de espera;
- duas falhas seguidas avisam "API fora do ar desde HH:MM" em #alertas, uma vez por queda; a volta avisa "API voltou, fora por N min"; uma falha só (partida a frio) não avisa;
- estado no KV `ESTADO`, gravado só quando muda (uma queda gasta três gravações com o Discord respondendo; sem webhook ou com o Discord fora, a queda grava a cada checagem, até 288 por dia, abaixo do teto de 1.000); a volta guarda a hora em que voltou, para a duração não crescer se o aviso atrasar; o KV é criado no primeiro deploy pelo provisionamento automático do `wrangler` (o id não fica no repositório);
- `DISCORD_ALERTAS` vai como segredo do Worker pelo `deploy.yml`; sem ele, a vigia só registra no log;
- sem rota pública; código em `infra/cloudflare/vigia/`, testado na CI (job **Worker (vigia)**).

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
- No PR, o job `O que mudou` (`dorny/paths-filter`) decide: **backend** com `src/`, `tests/`, `openapi/` (o `ContratoOpenApiTests`), solução, `global.json`, `Directory.*.props` ou `.config/`; **front** com `apps/web/` ou `openapi/`; **E2E** quando qualquer um dos dois muda; **imagem** com o código da API (`src/`, solução, `global.json`, `Directory.*.props`), `docker/` ou `.dockerignore`; **borda** com `infra/cloudflare/`; **Ações** (testes do aviso do Discord, `avisar.test.sh`) com `.github/actions/avisar-discord/` ou `ci.yml`. **Specs** (testes da fábrica e dos scripts, `conferir-specs`) com `docs/specs/`, `docs/processo.md`, `.claude/`, os scripts `conferir-*`, `rotulos*` e `ci-local*`, `ci.yml` ou `deploy.yml`; o job baixa só essas pastas (sparse-checkout), mais `.github/workflows`, porque o `conferir-fabrica` confere no `deploy.yml` a normalização de `TEXTOS_LEGAIS_PUBLICADOS` (F14-121). Mudar `ci.yml`, `.github/actions/subir-acordu/`, `.github/actions/cache-nuget/`, `.editorconfig` ou `.gitattributes` roda tudo. Job pulado conta como verde; o job **`CI ok`** falha se algum job falhou ou foi cancelado, e é o check a exigir na proteção da `main`. PR só de documentos não roda nenhum job pesado.
- **PR em rascunho não roda E2E nem imagem** (decidido em 17/11): os dois jobs mais caros esperam o PR ser marcado como pronto (`ready_for_review`), que roda a CI inteira de novo. Backend, front, specs e borda rodam no rascunho, que é como a sessão na nuvem valida o .NET. O `CI ok` verde de um rascunho, portanto, não cobre a fumaça.
- **Caches:** pacotes do NuGet (ação `cache-nuget`, chave pelos `.csproj`, `Directory.*.props`, `global.json` e `.config/dotnet-tools.json`) na fumaça e nos bots de migrations, OpenAPI e capturas (o backend e o deploy ficam sem, para o bundle das migrations seguir conferindo o checkout limpo, #180); Chromium do Playwright pela versão do `@playwright/test`; camadas do Docker da imagem no cache do Actions (`type=gha`, `mode=max`); `pnpm` pelo `setup-node`.
- Na `main`, um run não cancela o anterior (no PR, sim).
- Na `main` (merge) e no disparo manual, roda tudo.

- **Backend:** restore → build → todos os testes (incluindo `ArchitectureTests`); integração com Testcontainers (`postgres:17`, mesma versão do Neon). O teste do object storage usa o emulador Adobe S3Mock (Testcontainers); os testes funcionais usam armazenamento em memória.
- **Frontend:** install → lint → Vitest → build.
- **Imagem:** build do `api.Dockerfile` e teste de fumaça (§2.3), sem push.
- Merge em `main` só com tudo verde e PR revisado.

---

### 3.1 Validação local (`scripts/ci-local.sh`)

Roda na máquina o que a CI rodaria na branch, para o PR chegar verde e gastar um run só. As áreas vêm da diferença para a `origin/main` com as mesmas regras do `O que mudou` (testadas em `scripts/ci-local.test.sh`, no job **Specs**): mudou o filtro do `ci.yml`, muda o script no mesmo PR.

- `scripts/ci-local.sh`: as áreas que mudaram, com os testes completos (o backend precisa do Docker, pelos Testcontainers).
- `--rapido`: sem bundle das migrations, integração, funcionais, build do front nem empacotamento dos Workers. No backend, só unidade e arquitetura.
- `--tudo`: todas as áreas, sem olhar a diferença.
- E2E e imagem ficam para a CI. Ferramenta ausente (`dotnet`, `pnpm`, `node`) pula a área com aviso, sem falhar: na sessão na nuvem o backend fica sempre para a CI.
- Pre-push opcional: `git config core.hooksPath .githooks` liga o `.githooks/pre-push`, que roda o `--rapido` (sem `origin/main` local, pula com aviso); `git push --no-verify` pula uma vez.

---

## 4. Deploy (`deploy.yml`) — na `main`

**Disparo manual** (`workflow_dispatch`, só na `main`) até haver cliente pagante; depois, automático a cada merge (spec do F8, decisão 1, 03/11/2026). O plano Free com repositório privado não tem aprovação obrigatória em *environments*.


0. **CI verde:** sem o check `CI ok` verde no commit da `main`, o deploy não começa (o disparo manual não espera a CI).
1. **Autenticação** no Google por WIF (`google-github-actions/auth`), restrita a este repositório na `main` (condição do provider por `repository_id`, não pelo nome; setup A4), impersonando `acordu-deployer@acordu-prod`.
2. **Build e push** para `us-east4-docker.pkg.dev/acordu-prod/acordu/acordu-api`, tag = SHA do commit.
3. **Migrations:** migration bundle do EF Core (`scripts/bundle-migrations.sh`, que a CI também roda num checkout limpo) no **endpoint direto** do Neon, com `acordu_migrator`. Falhou → para aqui; a revisão em produção continua a anterior.
4. **Nova revisão** no Cloud Run (`gcloud run deploy --image ...:<sha>`) com as variáveis do §7, num arquivo (`--env-vars-file`) que substitui todas as da revisão anterior; depois, `update-traffic --to-latest`, que desfaz um rollback anterior.
5. **Healthcheck** `GET /api/health` pela borda; falhou → tráfego de volta à revisão anterior (`gcloud run services update-traffic --to-revisions`). Até a borda apontar para o serviço (variável `BORDA_ATIVA=true`, runbook C4), confere direto na URL `*.run.app`, com o `X-Origem-Token`. Só "serviço não existe" conta como primeiro deploy; outro erro ao ler o serviço para o deploy, para não desligar o rollback.
6. **Worker:** `wrangler deploy` de `infra/cloudflare/api-borda` (`keep_vars`: `ORIGEM` e `TOKEN_ORIGEM` ficam como estão).
7. **SPA:** build de `apps/web` com `VITE_URL_API=https://api.acordu.app.br`, `VITE_TEMPO_REAL=polling`, `VITE_TURNSTILE_CHAVE` (variável `TURNSTILE_CHAVE_SITE`, obrigatória) e `VITE_CONTATO_NEBRYS` (variável `CONTATO_NEBRYS`, obrigatória; F14e) e `VITE_TEXTOS_LEGAIS_PUBLICADOS` (o valor normalizado da variável `TEXTOS_LEGAIS_PUBLICADOS` que sai do passo "Variáveis da API", o mesmo que vai à API como `TextosLegais__Publicados`, §7; F14f e F14h: só com `true` o SPA mostra `/termos`, `/privacidade`, os links delas e a caixa de aceite; sem ela, ficam escondidos, até a issue #194), publicado no Pages (`wrangler pages deploy` no projeto `acordu-web`, criado no primeiro deploy). O `public/_headers` vai junto: além da CSP, `no-cache` no `sw.js` e no `manifest.webmanifest` (app instalável, F12f). O token do Cloudflare tem o mínimo de conta e zona (runbook C3); o Cloudflare não restringe token a um script ou projeto.
8. **Vigia:** `wrangler deploy` de `infra/cloudflare/vigia` (o KV `ESTADO` é criado no primeiro deploy) e `wrangler secret put DISCORD_ALERTAS`. Vem por último e não segura o deploy: se falhar, o passo avisa no log e a API e o SPA ficam no ar.
9. Discord (`.github/actions/avisar-discord`, F13g): sucesso em #deploy com o commit; falha em #alertas com o link da execução. Sem o webhook do canal, pula; o Discord fora só vira aviso no log.

---

## 5. Jobs agendados

Cada job é um endpoint interno (`POST /internal/jobs/<nome>`): `X-Job-Token` comparado em tempo constante, 404 sem token válido, fora do OpenAPI, idempotente, todos os tenants por chamada. Jobs por tenant rodam cada tenant num escopo próprio; a falha de um é logada, não para os demais e entra em `falhas`. Desde o F14b, só nas empresas graváveis: VENCIDA e CANCELADA ficam de fora (RF-EMP-02, ADR 0015, item 4), sem contar falha. Desde o F14d, a outbox também não entrega mensagem de empresa só leitura: ela sai da fila como descartada (conta em `outboxProcessadas`, com a quantidade no log), sem nova tentativa. Na tabela `outbox_mensagens`, falha é `falhou_em` preenchido (com `tentativas`), e descarte é `processada_em` com `erro` começando por `descartada:`; consulta que conte falhas usa `falhou_em`, nunca `erro IS NOT NULL`; e, na volta de uma empresa ao gravável (`Tenant.GravavelDesde`), as recorrências não geram as datas que passaram no só leitura. Os alertas de SLA da volta saem pela situação do momento: o prazo não pausou (F14-111).

| Job do Scheduler | Agenda (`America/Sao_Paulo`) | Endpoint | Faz |
|---|---|---|---|
| `acordu-tick` | `*/15 7-19 * * 1-6` | `/internal/jobs/tick` | Tarefas recorrentes, outbox pendente, alertas de SLA e cronômetros esquecidos (F12e). Reenvia as sugestões que não saíram em #sugestoes (F13e). Responde `{ tarefasRecorrentes, outboxProcessadas, alertasSla, cronometrosEsquecidos, sugestoesReenviadas, falhas }`. Fora da janela do agendamento (depois das 19h e aos domingos), o aviso de esquecido sai no primeiro tick seguinte. |
| `acordu-diario` | `40 6 * * *` | `/internal/jobs/diario` | Roda em sequência `vencimento-testes` (F14a: empresa em TESTE com o fim do teste já alcançado passa a VENCIDA, RF-EMP-01; primeiro, para a recém-vencida ficar fora dos passos seguintes), `avisos-situacao` (F14j: e-mail aos Donos 7 e 1 dia antes do fim do teste, no vencimento e 7 dias antes do expurgo, RF-EMP-04, contados em dias civis no fuso da empresa; `Empresas__AvisosAntesDoFim` e `Empresas__AvisoAntesDoExpurgo`; laço próprio, porque as só leitura também recebem; antes da liberação do envio, só o criador; um aviso por empresa e por diário, marcado no `Tenant` antes de sair e sem repetir; o e-mail sai logo depois, fora da outbox, e a falha conta em `falhas` e libera o aviso para o próximo diário; nada para quem tem a data do expurgo já passada), `fechamento-automatico`, `expurgo-auditoria`, `expurgo-notificacoes` (que apaga também o feed de atividade com mais de 90 dias) `expurgo-cadastros` (empresa não liberada há 7 dias ou mais e sem dados, RF-AUT-07; desde o F14i, o registro do documento dela sai junto, F14-104), `expurgo-empresas` (F14i: empresa VENCIDA ou CANCELADA com o início do só leitura há `Empresas__DiasAteExpurgo` dias ou mais, padrão 30, é apagada inteira, com anexos e logotipo no storage e as contas sem presença em outra empresa; o registro do documento fica; cada empresa numa transação, e a falha de uma conta em `falhas`, RF-EMP-03), `expurgo-ocorrencias-erro` (ocorrências de erro sem repetição há 90 dias, ADR 0012) e `expurgo-marcas-operacao` (marcas de "já avisado" com mais de 7 dias). Responde `{ empresasVencidas, avisosSituacao, fechadas, auditoriaRemovida, notificacoesRemovidas, atividadesRemovidas, cadastrosRemovidos, empresasExpurgadas, ocorrenciasErroRemovidas, marcasOperacaoRemovidas, falhas }`; um passo que falha é logado, conta em `falhas` e não impede os seguintes. |

- Os endpoints diários individuais continuam existindo para disparo manual; os expurgos de cadastros e de empresas rodam só pelo diário.
- **Avisos (F13c, ADR 0012):** passo com falha no tick ou no diário vai para #alertas, uma vez por passo e por dia (dia de Brasília); o diário sem falha manda o resumo em #deploy, uma vez por dia, e a falta dele avisa que o agendamento parou.
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

- **GitHub Actions:** nenhuma chave do Google (WIF). Nomes das variáveis e dos secrets no runbook ("Segredos do GitHub"). Variáveis `GCP_WIF_PROVIDER`, `GCP_PROJECT_ID`, `BORDA_ATIVA`, `BACKUP_ATIVO`, `TURNSTILE_CHAVE_SITE` (chave do site do Turnstile, pública, para o build do SPA; conferida antes de publicar) e `CONTATO_NEBRYS` (o canal da Nebrys, `contato@acordu.app.br`, decisão 14 do G2 do F14; vai para o build do SPA como `VITE_CONTATO_NEBRYS`, citado nas faixas da situação da empresa, e é conferida antes de publicar; desde o F14g vai também à API como `Nebrys__Contato`, D15, citado na recusa do documento no cadastro). Secrets: connection string direta do `acordu_migrator`, variáveis da API (abaixo), token do Cloudflare (Pages, Workers e o KV da vigia), token R2 de backup, webhooks do Discord (`DISCORD_ALERTAS`, `DISCORD_DEPLOY`, `DISCORD_SUGESTOES`, um por canal; ADR 0012). Variáveis opcionais `OPERACAO_LINK_ERROS` (link do Error Reporting nos avisos de erro) e `TEXTOS_LEGAIS_PUBLICADOS` (F14f e F14h, design do F14, D16: publica os textos legais; ligar só depois da issue #194). Esta é conferida e normalizada no passo "Variáveis da API": ausente ou vazia → `false`; `true` ou `false`, sem diferenciar maiúsculas → o próprio; outro valor (`1`, `sim`) para o deploy antes de publicar. O valor normalizado sai do passo e alimenta os dois leitores: `TextosLegais__Publicados` na API (aceite exigido e gravado, pendentes no `/auth/eu`, "ciente") e `VITE_TEXTOS_LEGAIS_PUBLICADOS` no build do SPA (páginas, links, caixa de aceite e aviso), para os dois verem o mesmo valor (convenções §4).
- **Variáveis da API no Cloud Run:**
  - `ConnectionStrings__Acordu` (pooled, `acordu_app`);
  - `Jwt__ChaveAssinatura`; `Jobs__Token`;
  - `Cors__Origem=https://acordu.app.br`;
  - `App__UrlPublica=https://acordu.app.br/` (links dos e-mails) e `App__UrlApi=https://api.acordu.app.br/` (feed ICS da agenda);
  - `Proxy__ConfiarEncaminhamento=true`, `Proxy__Saltos=2`, `Proxy__TokenOrigem` (o mesmo segredo do Worker);
  - `TempoReal__Sse=false`;
  - `Email__ResendChave`, `Email__Remetente` (padrão `nao-responda@send.acordu.app.br`), `Email__NomeRemetente`;
  - `Armazenamento__Endpoint=https://<conta>.r2.cloudflarestorage.com`, `Armazenamento__Regiao=auto`, `Armazenamento__Bucket=acordu-anexos`, `Armazenamento__ChaveAcesso` / `Armazenamento__ChaveSecreta` (token R2 de leitura e escrita só em `acordu-anexos`);
  - limites opcionais (`RateLimit__ErrosWeb__PorMinuto` 10, `RateLimit__Auth__PorMinuto` 10, `RateLimit__Refresh__PorMinuto` 30, `RateLimit__Portal__PorMinuto` 10, `RateLimit__Agenda__PorMinuto` 30, `RateLimit__Registro__PorHora` 3);
  - prazos da situação da empresa, opcionais (F14): `Empresas__DiasDeTeste` (30, decisão 1 do G2), `Empresas__DiasAteExpurgo` (30, decisão 2), `Empresas__DiasFaixaTeste` (7, decisão 3: dias antes do fim do teste em que a faixa aparece, devolvidos no `/auth/eu`), `Empresas__AvisosAntesDoFim` (`7,1`, decisão 3: dias antes do fim do teste em que sai o e-mail aos Donos, separados por vírgula) e `Empresas__AvisoAntesDoExpurgo` (7, decisão 3; F14j); valor presente e inválido impede a subida;
  - `Turnstile__Segredo` (proteção do cadastro, RNF-SEG-10; obrigatório no `deploy.yml`, secret `API_TURNSTILE_SEGREDO`);
  - `Documentos__ChaveHmac` (chave do HMAC do registro "um teste por documento", F14g, design D8 e segurança §4; ≥ 32 bytes, **não gira**; obrigatória: a API não sobe sem ela e o `deploy.yml` para antes de publicar; secret `API_DOCUMENTOS_CHAVE`);
  - `Nebrys__Contato` (canal da Nebrys nas recusas e avisos da API, D15; o `deploy.yml` passa a variável `CONTATO_NEBRYS`, obrigatória; sem ela, os textos dizem só "fale com a Nebrys");
  - `Nebrys__Operadores` (F14k, ADR 0014, item 1: e-mails das contas que abrem a área da Nebrys, separados por vírgula; secret opcional `API_NEBRYS_OPERADORES`). Sem ele, ninguém é operador e a área inteira responde `404` (a API sobe e o deploy avisa no log); um item que não é e-mail impede a subida, e o `deploy.yml` para antes de publicar. Trocar o operador é mudar o secret e fazer um deploy (nova revisão); o token de quem saiu da lista para de valer na revisão nova;
  - `TextosLegais__Publicados` (`true` ou `false`, padrão desligado; F14h, D16: com `true`, o cadastro e o aceite de convite da equipe exigem e gravam o aceite dos textos legais e o `/auth/eu` lista os pendentes; valor presente e inválido impede a subida; o `deploy.yml` passa o valor normalizado de `TEXTOS_LEGAIS_PUBLICADOS`, o mesmo do build do SPA);
  - `Logs__Formato=json` (logs estruturados para o Cloud Logging e o Error Reporting, RNF-OPS-01) e `App__Versao` (o commit, no `serviceContext` dos erros); fixas no `deploy.yml` (ADR 0012).
  - `Discord__Alertas`, `Discord__Deploy`, `Discord__Sugestoes` (webhooks dos canais, só https; sem, o canal não envia), `Operacao__AvisosPorHora` (teto de avisos de erro, padrão 20) e `Operacao__LinkErros` (link do Error Reporting na mensagem). O `deploy.yml` passa os mesmos webhooks dos workflows (`DISCORD_*`) e a variável `OPERACAO_LINK_ERROS`; sem um webhook, a API sobe e o deploy avisa no log que aquele canal fica sem avisos.
- **Segredo do Worker da borda:** `TOKEN_ORIGEM`; variável `ORIGEM` com a URL `*.run.app`. **Da vigia:** `DISCORD_ALERTAS`, posto pelo `deploy.yml`.
- **Risco aceito (nível 0):** os secrets de produção são do repositório, não de um *environment* com restrição de branch (o plano Free com repositório privado não oferece). O WIF limita o Google à `main`, mas um workflow alterado em qualquer branch do próprio repositório alcança Neon (migrator), R2 e Cloudflare. A guarda `if: github.ref == 'refs/heads/main'` do `deploy.yml` evita engano, não abuso. Mitigação: só o Daniel tem escrita no repositório, com MFA; PRs de fork não recebem secrets. Revisar quando houver outro colaborador ou cliente pagante (environment protegido ou Secret Manager).
- **Risco aceito: janela da virada dos textos legais (F14h, design do F14, D16).** O `deploy.yml` publica a API antes do SPA. No deploy que liga `TEXTOS_LEGAIS_PUBLICADOS`, por alguns minutos (revisão, health, borda, build, Pages) e numa aba aberta com o SPA anterior até recarregar, a tela sem a caixa de aceite encontra a API que a exige, e o cadastro recebe `400` no campo do aceite. A virada acontece uma vez (depois da #194), num deploy autorizado no G4: **faça-a fora do horário de uso**. Desligar é inofensivo: a tela antiga manda o aceite e a API o ignora.
- Secret Manager fica como evolução.

---

## 8. Backup e restore

- **Banco:** `backup.yml`, às 04:00 (`0 7 * * *` UTC), só com `BACKUP_ATIVO=true`: `pg_dump -Fc` (imagem `postgres:17` no runner, confere o arquivo com `pg_restore --list`) no endpoint direto → `acordu-backups/db/AAAA-MM-DD.dump` no R2, com token que só escreve nesse bucket. Retenção de 14 dias por lifecycle do bucket.
- **Anexos:** o R2 não tem versionamento. A aplicação só apaga objeto ao trocar o logotipo do portal, e o token da API só alcança `acordu-anexos`. Cópia fora do Cloudflare quando houver cliente pagante (ADR 0010 §8).
- **Restore testado** antes do primeiro cliente real: `pg_restore` num branch do Neon ou num Postgres 17 local, com a API apontada para ele (passo a passo no runbook, "Restore").
- Discord: heartbeat diário de sucesso em #deploy; falha em #alertas.

---

## 9. Monitoramento

- **Vigia** (Worker `acordu-vigia`, §2.1a) em `https://api.acordu.app.br/api/health` a cada 5 min: avisa a queda e a volta em #alertas e mantém a instância quente parte do tempo, como fazia o UptimeRobot (ADR 0012).
- **Pré-aquecimento pela UI:** ao carregar qualquer tela o SPA já renova a sessão (`POST /api/v1/auth/refresh`), o que acorda o container da API antes de a pessoa digitar; por isso não há chamada extra ao `/api/health` (F8d). O banco acorda na primeira requisição que o consulta (com cookie, a própria renovação; sem cookie, o login).
- Logs do Cloud Run em JSON (`Logs__Formato=json`): os erros aparecem agrupados no Error Reporting, por serviço e versão, sem SDK (ADR 0012, F13a). Discord (deploy e backup: sucesso em #deploy, falha em #alertas, F13g), alerta do Cloud Monitoring para os jobs e de orçamento (e-mail). Erro 500 novo vira aviso em #alertas pela própria API (F13b), um por erro a cada 24 h e no máximo 20 por hora; as falhas dos jobs vão para #alertas e o resumo do diário para #deploy (F13c); os erros do app no navegador chegam por `POST /api/v1/erros-web` como serviço `acordu-web`, só com `VITE_VERSAO` no build (o commit, no `deploy.yml`; F13d).
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
- [ ] Política de privacidade informa dados hospedados nos EUA (LGPD, transferência internacional): página `https://acordu.app.br/privacidade` (F14f, segurança §6), com o texto revisado pelo Daniel e os marcadores "a preencher" resolvidos antes de valer.
- [ ] MFA com códigos de recuperação em Google, Neon, Cloudflare, GitHub e Resend.
