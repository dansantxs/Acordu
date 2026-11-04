# Acordu — Setup de Infraestrutura (nível 0)

| | |
|---|---|
| **Versão** | 0.4 |
| **Data** | 03/11/2026 |
| **Objetivo** | Provisionar o nível 0 do ADR 0010: Google Cloud (Cloud Run, Artifact Registry, Scheduler), Neon, Cloudflare (DNS, Pages, Worker, R2) e Resend. |
| **Natureza** | Runbook operacional (ordem de execução) + registro do estado provisionado (ver fim). |

> Substitui o runbook da Oracle Cloud (`setup-infra-oracle-acordu.md`), descartado com o ADR 0006. O histórico está no git.

> **Aviso de recência:** planos gratuitos mudam sem aviso. Antes de provisionar, confirme os limites vigentes do Cloud Run, Cloud Scheduler, Artifact Registry, Neon Free, R2 e Workers (tabela no ADR 0010).

---

## 0. Nomenclatura

| Recurso | Nome |
|---|---|
| Conta de faturamento Google | `nebrys-billing` |
| Projeto Google | `acordu-prod` |
| Serviço Cloud Run | `acordu-api` (`us-east4`) |
| Repositório do Artifact Registry | `acordu` (`us-east4`, Docker) |
| Service accounts | `acordu-api`, `acordu-deployer` |
| Pool de Workload Identity | `github` (provider `acordu-repo`) |
| Jobs do Cloud Scheduler | `acordu-tick`, `acordu-diario` |
| Projeto Neon | `acordu` (banco `acordu`; roles `acordu_migrator`, `acordu_app`) |
| Buckets R2 | `acordu-anexos`, `acordu-backups` |
| Tokens R2 | `acordu-api-anexos`, `acordu-backup` |
| Worker | `acordu-api-borda` (`api.acordu.app.br`) |
| Projeto Pages | `acordu-web` (`acordu.app.br`) |

---

## Parte 0 — Contas

1. **MFA** em Google, Neon, Cloudflare, GitHub e Resend, com códigos de recuperação guardados **fora** do provedor e um segundo fator cadastrado (ADR 0010 §10).
2. Conta de faturamento **`nebrys-billing`** com cartão. O free tier do Google exige faturamento ativo; o alerta de orçamento (A1) é a proteção.

---

## Parte A — Google Cloud

### 1. Projeto e faturamento
1. Criar o projeto **`acordu-prod`** e vinculá-lo à `nebrys-billing`.
2. Ativar APIs: Cloud Run, Artifact Registry, Cloud Scheduler, IAM Credentials, Security Token Service, Cloud Monitoring.
3. **Alerta de orçamento** na `nebrys-billing`, valor baixo (ex.: R$ 10), alertas em 50/90/100%. Não bloqueia gasto.

### 2. Artifact Registry
- Repositório Docker **`acordu`** em `us-east4`.
- **Política de limpeza:** apagar qualquer imagem com mais de 7 dias, mantendo sempre as 5 versões mais recentes (a regra *Keep* vence a *Delete*). Só apagar as sem tag não limparia nada: cada deploy marca a imagem com o SHA do commit. Aplicada em 04/11 pelo Cloud Shell:
  ```
  cat > limpeza.json <<'EOF'
  [
    {"name": "manter-5", "action": {"type": "Keep"}, "mostRecentVersions": {"keepCount": 5}},
    {"name": "apagar-antigas", "action": {"type": "Delete"}, "condition": {"tagState": "ANY", "olderThan": "7d"}}
  ]
  EOF
  gcloud artifacts repositories set-cleanup-policies acordu --location=us-east4 --policy=limpeza.json --no-dry-run
  ```

### 3. Service accounts
| Service account | Papéis | Uso |
|---|---|---|
| `acordu-api` | nenhum | Identidade do Cloud Run (a API não chama serviços do Google). |
| `acordu-deployer` | `roles/run.admin`; `roles/artifactregistry.writer` no repositório `acordu`; `roles/iam.serviceAccountUser` em `acordu-api` | Deploy pelo GitHub Actions. |

**Nenhuma chave JSON** é criada.

### 4. Workload Identity Federation
1. Pool **`github`** com provider OIDC **`acordu-repo`** (`https://token.actions.githubusercontent.com`).
2. Condição: `assertion.repository == 'dansantxs/acordu' && assertion.ref == 'refs/heads/main'`.
3. `roles/iam.workloadIdentityUser` em `acordu-deployer` para o principal do provider.
4. No repositório: variáveis `GCP_WIF_PROVIDER` e `GCP_PROJECT_ID` (ver "Segredos do GitHub").

### 5. Cloud Run
Criado pelo primeiro deploy (Actions → **Deploy** → Run workflow, na `main`; `deploy-ci-acordu-v1.md` §4), com os parâmetros do §2.2 de lá. Anotar a URL `*.run.app` para a variável `ORIGEM` do Worker. Enquanto a variável `BORDA_ATIVA` não for `true`, o deploy confere o health direto na URL `*.run.app` (com o token de origem), porque a borda ainda não aponta para o serviço; depois de C4, pela borda.

### 6. Cloud Scheduler (depois do primeiro deploy e do Worker)
Fuso `America/Sao_Paulo`, região `us-east4`, cabeçalho `X-Job-Token` (o valor do secret `API_JOBS_TOKEN`), retry com backoff curto:
- **`acordu-tick`:** `*/15 7-19 * * 1-6` → `POST https://api.acordu.app.br/internal/jobs/tick`.
- **`acordu-diario`:** `40 6 * * *` → `POST https://api.acordu.app.br/internal/jobs/diario`.

Política de alerta do Cloud Monitoring sobre tentativas falhas dos dois jobs, com aviso por e-mail (conferir o custo antes de criar).

---

## Parte B — Neon

1. Projeto **`acordu`**, **Postgres 17**, **`aws-us-east-1`**, plano Free. Autoscaling de **0,25 a 1 CU**; suspensão padrão.
2. Banco **`acordu`**, dono **`acordu_migrator`** (criado pelo console).
3. Role da aplicação, pelo SQL Editor, como `acordu_migrator`:
   ```sql
   CREATE ROLE acordu_app LOGIN PASSWORD '<gerar>';
   REVOKE ALL ON DATABASE acordu FROM PUBLIC;
   GRANT CONNECT ON DATABASE acordu TO acordu_app;
   GRANT USAGE ON SCHEMA public TO acordu_app;
   ALTER DEFAULT PRIVILEGES FOR ROLE acordu_migrator IN SCHEMA public
     GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO acordu_app;
   ALTER DEFAULT PRIVILEGES FOR ROLE acordu_migrator IN SCHEMA public
     GRANT USAGE, SELECT ON SEQUENCES TO acordu_app;
   ```
4. Connection strings no formato do Npgsql (`Host=...;Database=acordu;Username=...;Password=...;SSL Mode=Require`), não a URI `postgresql://` do console:
   - **direta** (host sem `-pooler`) com `acordu_migrator` → secret `NEON_MIGRATOR` (migrations e `pg_dump`; o `backup.yml` converte para a libpq);
   - **pooled** (host com `-pooler`) com `acordu_app` e `Maximum Pool Size=10` → secret `API_CONNECTION_STRING`.

---

## Parte C — Cloudflare

### 1. Domínios
1. Registrar **`acordu.app.br`** e **`acordu.com.br`** no registro.br e delegar para o Cloudflare.
2. `acordu.com.br` e `www.acordu.app.br`: regra de redirecionamento permanente para `https://acordu.app.br`.

### 2. R2
Dois buckets **privados**, sem domínio público nem `r2.dev`:

| Bucket | Uso | Configuração |
|---|---|---|
| `acordu-anexos` | Anexos e logotipos (runtime) | Sem lifecycle. CORS desligado (download por URL pré-assinada com redirecionamento). |
| `acordu-backups` | Dumps diários do banco | Lifecycle: apagar objetos com mais de 14 dias. |

Tokens de API do R2 (S3):
- **`acordu-api-anexos`:** *Object Read & Write* só em `acordu-anexos` → secrets `API_R2_CHAVE_ACESSO` / `API_R2_CHAVE_SECRETA`.
- **`acordu-backup`:** *Object Read & Write* só em `acordu-backups` → secrets `R2_BACKUP_CHAVE_ACESSO` / `R2_BACKUP_CHAVE_SECRETA`.

Endpoint S3: `https://<id-da-conta>.r2.cloudflarestorage.com` (secret `R2_ENDPOINT`), região `auto`. Conferir no primeiro deploy que a URL pré-assinada de download funciona (teste manual de anexo).

### 3. Pages
Projeto **`acordu-web`** (upload direto pelo `wrangler pages deploy`, sem integração Git; o primeiro deploy o cria), domínio `acordu.app.br`.

Token de API do Cloudflare para o `deploy.yml` (secret `CLOUDFLARE_API_TOKEN`; o ID da conta vai em `CLOUDFLARE_ACCOUNT_ID`). O Cloudflare não restringe token a um script ou projeto, só a conta e zona; o mínimo é:
- conta: *Workers Scripts: Edit* e *Cloudflare Pages: Edit*;
- zona `acordu.app.br`: *Workers Routes: Edit* e *DNS: Edit* (o domínio personalizado `api.acordu.app.br` do Worker cria o registro).

Conferir no primeiro deploy; se o `wrangler` pedir outra permissão, acrescentá-la aqui.

### 4. Worker da borda
1. Script **`acordu-api-borda`** publicado pelo `deploy.yml` (`infra/cloudflare/api-borda`).
2. Rota/domínio personalizado **`api.acordu.app.br`**.
3. Variável `ORIGEM` = URL `*.run.app`; segredo `TOKEN_ORIGEM` com **o mesmo valor** do secret `API_TOKEN_ORIGEM`, gerado antes do primeiro deploy (o deploy o grava em `Proxy__TokenOrigem` da API): `pnpm exec wrangler secret put TOKEN_ORIGEM` em `infra/cloudflare/api-borda`, ou pelo painel. O `wrangler.toml` tem `keep_vars = true`: os deploys do `deploy.yml` preservam a `ORIGEM` definida no painel.
4. Conferir `curl https://api.acordu.app.br/api/health` e criar a variável do repositório `BORDA_ATIVA=true`: daí em diante o health do deploy passa pela borda.

### 5. Turnstile do cadastro (RNF-SEG-10, F9)
1. No painel do Cloudflare: **Turnstile** → Add widget. Nome `acordu-cadastro`, hostname **`acordu.app.br`**, modo *Managed*.
2. A chave do site (pública) vai na variável do repositório `TURNSTILE_CHAVE_SITE`; a chave secreta, no secret `API_TURNSTILE_SEGREDO` (vira `Turnstile__Segredo` da API). O `deploy.yml` recusa publicar sem as duas (confere antes da nova revisão).
3. Depois do primeiro deploy, criar uma conta de teste em `acordu.app.br/cadastro` e conferir que passa. A API confere o hostname que o Cloudflare devolve (o de `App__UrlPublica`); se todo cadastro voltar com "Confirme que você não é um robô", ver no Turnstile do painel o hostname registrado nas tentativas e se a `Referrer-Policy: same-origin` do SPA interfere. Segredo errado aparece no log da API como erro (EventId 312). Conferência negativa: `curl -X POST https://api.acordu.app.br/api/v1/auth/registro -H 'Content-Type: application/json' -d '{"nome":"x","email":"x@exemplo.com","senha":"senha-bem-comprida","nomeEmpresa":"x"}'` responde `400` com `errors.tokenTurnstile`.

---

## Parte D — Resend

O Acordu envia e-mail para os **clientes dos clientes**: reputação de domínio importa.
1. Adicionar `send.acordu.app.br` no Resend; criar no Cloudflare os registros indicados (SPF, DKIM, MX de retorno).
2. **DMARC:** `TXT _dmarc.acordu.app.br "v=DMARC1; p=none; rua=mailto:<seu-email>"` (subir para `quarantine` após algumas semanas).
3. Remetente `nao-responda@send.acordu.app.br`; chave de API exclusiva do Acordu → secret `API_RESEND_CHAVE`.

---

## Parte E — Monitoramento

- **UptimeRobot:** monitor HTTP em `https://api.acordu.app.br/api/health`, 5 min, alerta por e-mail.
- **Discord:** webhook num canal privado → secret `DISCORD_WEBHOOK` (deploy e backup avisam por ele; sem o secret, só pulam o aviso).
- Sem Sentry no F8 (spec do F8, decisão 3): erros de aplicação ficam nos logs do Cloud Run.

---

## Segredos do GitHub

Repositório → Settings → Secrets and variables → Actions. Só os nomes ficam aqui; os valores, nunca.

| Tipo | Nome | Conteúdo |
|---|---|---|
| Variável | `GCP_WIF_PROVIDER` | Nome completo do provider `acordu-repo` (A4) |
| Variável | `GCP_PROJECT_ID` | `acordu-prod` |
| Variável | `BORDA_ATIVA` | `true` depois de C4; o health do deploy passa a ir pela borda |
| Variável | `TURNSTILE_CHAVE_SITE` | Chave do site do widget `acordu-cadastro` (C5); vai para o build do SPA |
| Variável | `BACKUP_ATIVO` | `true` quando Neon e R2 estiverem prontos; liga o agendamento do `backup.yml` |
| Secret | `NEON_MIGRATOR` | String direta, `acordu_migrator` (B4) |
| Secret | `API_CONNECTION_STRING` | String pooled, `acordu_app` (B4) |
| Secret | `API_JWT_CHAVE` | 64 bytes aleatórios (`Jwt__ChaveAssinatura`) |
| Secret | `API_JOBS_TOKEN` | 32 bytes aleatórios (`Jobs__Token`; o mesmo no Scheduler) |
| Secret | `API_TOKEN_ORIGEM` | 32 bytes aleatórios; o mesmo valor vai no `TOKEN_ORIGEM` do Worker (C4) |
| Secret | `API_RESEND_CHAVE` | Chave do Resend (D3) |
| Secret | `API_TURNSTILE_SEGREDO` | Chave secreta do widget do Turnstile (C5) |
| Secret | `R2_ENDPOINT` | Endpoint S3 do R2 (C2) |
| Secret | `API_R2_CHAVE_ACESSO`, `API_R2_CHAVE_SECRETA` | Token `acordu-api-anexos` (C2) |
| Secret | `R2_BACKUP_CHAVE_ACESSO`, `R2_BACKUP_CHAVE_SECRETA` | Token `acordu-backup` (C2) |
| Secret | `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID` | Token do deploy e ID da conta (C3) |
| Secret | `DISCORD_WEBHOOK` | Webhook do canal de avisos (E) |

O `deploy.yml` recusa publicar se faltar algum obrigatório; sem `R2_ENDPOINT`, a API sobe sem anexos. Os valores aleatórios: `openssl rand -base64 48`.

---

## Restore (antes do primeiro cliente; F8-31)

1. Baixar o dump do dia com o token `acordu-backup` (as mesmas variáveis do `backup.yml`):
   ```
   AWS_ACCESS_KEY_ID=... AWS_SECRET_ACCESS_KEY=... AWS_DEFAULT_REGION=auto \
   AWS_REQUEST_CHECKSUM_CALCULATION=when_required AWS_RESPONSE_CHECKSUM_VALIDATION=when_required \
   aws s3 cp s3://acordu-backups/db/AAAA-MM-DD.dump . --endpoint-url <R2_ENDPOINT>
   ```
2. Subir o Postgres 17 local (`docker compose -f docker-compose.dev.yml up -d`, porta 5434). Com o cliente 17 da imagem, sem instalar nada:
   ```
   docker run --rm --network host -e PGPASSWORD=acordu_dev -v "$PWD:/d" postgres:17 \
     createdb -h localhost -p 5434 -U acordu acordu_restore
   docker run --rm --network host -e PGPASSWORD=acordu_dev -v "$PWD:/d" postgres:17 \
     pg_restore -h localhost -p 5434 -U acordu -d acordu_restore --no-owner --no-privileges /d/AAAA-MM-DD.dump
   ```
3. Subir a API com `ConnectionStrings__Acordu` apontando para `acordu_restore` e conferir login e dados de uma empresa.

---

## Ordem resumida

```
0.  MFA nas contas + nebrys-billing
A1. acordu-prod + APIs + alerta de orçamento
A2. Artifact Registry (acordu, us-east4) + limpeza
A3. Service accounts
A4. WIF (pool github, provider acordu-repo)
B.  Neon: projeto, banco, roles → secrets do GitHub
C1. Domínios → Cloudflare
C2. R2: buckets + tokens → secrets do GitHub
D.  Resend (SPF/DKIM/DMARC)
C3. Token do Cloudflare
C5. Widget do Turnstile → TURNSTILE_CHAVE_SITE e API_TURNSTILE_SEGREDO
→ Segredos do GitHub: todos os da tabela (gerar API_JWT_CHAVE, API_JOBS_TOKEN e API_TOKEN_ORIGEM), menos BORDA_ATIVA e BACKUP_ATIVO
→ primeiro deploy (cria o serviço Cloud Run, o Worker e o Pages)
C3/C4. Domínios do Pages e do Worker; ORIGEM e TOKEN_ORIGEM (= API_TOKEN_ORIGEM) → BORDA_ATIVA=true
A6. Cloud Scheduler (tick e diário) + alerta de falha
E.  UptimeRobot + Discord
→ cadastro de teste em acordu.app.br (Turnstile, confirmação do e-mail, convite)
→ BACKUP_ATIVO=true; primeiro backup
→ restore testado
```

---

## Checklist de "pronto"

- [ ] MFA com recuperação em todas as contas.
- [ ] `acordu-prod` na `nebrys-billing`; alerta de orçamento ativo.
- [ ] Artifact Registry com política de limpeza.
- [ ] Service accounts com papéis mínimos; nenhuma chave JSON; WIF restrito a `dansantxs/acordu` / `main`.
- [ ] Neon: Postgres 17, `aws-us-east-1`, autoscaling até 1 CU; `acordu_app` sem DDL; strings direta e pooled separadas.
- [ ] R2: buckets privados, lifecycle no de backups, tokens separados por bucket.
- [ ] Cloud Run com `max-instances=2`; `/api/health` respondendo pela borda.
- [ ] Requisição direta à URL `*.run.app` responde 404 (token de origem).
- [ ] Login e refresh funcionando de `acordu.app.br` para `api.acordu.app.br`.
- [ ] `acordu-tick` e `acordu-diario` no Scheduler, com alerta de falha.
- [ ] Resend verificado + DMARC.
- [ ] UptimeRobot ativo; webhook do Discord recebendo deploy e backup.
- [ ] Turnstile no cadastro: conta de teste criada; sem o token, cadastro recusado.
- [ ] Backup diário rodando e restore testado.

---

## Estado provisionado

_(preencher conforme executar)_

| Recurso | Valor |
|---|---|
| Conta de faturamento | `nebrys-billing` — criada (04/11) |
| Projeto Google | `acordu-prod` — criado, na `nebrys-billing`, alerta de orçamento de R$ 10 (04/11) |
| Artifact Registry | `us-east4/acordu` — criado em 04/11, com a política de limpeza |
| WIF | pool `github` / provider `acordu-repo` — criado (04/11); variáveis `GCP_WIF_PROVIDER` e `GCP_PROJECT_ID` no GitHub |
| Neon | projeto `acordu` (PG 17, `aws-us-east-1`) — criado (04/11), roles e secrets `NEON_MIGRATOR`/`API_CONNECTION_STRING`; conferir o autoscaling até 1 CU |
| R2 | `acordu-anexos`, `acordu-backups` — _pendente_ |
| Worker | `acordu-api-borda` — _pendente_ |
| Pages | `acordu-web` — _pendente_ |
| Domínios | `acordu.app.br`, `acordu.com.br` — _registrar_ |
| Resend | domínio verificado? _pendente_ |
| UptimeRobot | _pendente_ |

> Regra de segurança: IDs numéricos, chaves, senhas e connection strings **não** entram neste documento nem no repositório.
