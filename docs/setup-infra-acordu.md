# Acordu — Setup de Infraestrutura (nível 0)

| | |
|---|---|
| **Versão** | 0.3 |
| **Data** | 26/10/2026 |
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
- **Política de limpeza:** manter as 5 versões mais recentes; apagar imagens sem tag com mais de 7 dias. Rodar primeiro em *dry run*.

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
4. No repositório: variáveis `GCP_WIF_PROVIDER` e `GCP_PROJECT_ID`.

### 5. Cloud Run
Criado pelo primeiro deploy (`deploy-ci-acordu-v1.md` §4), com os parâmetros do §2.2 de lá. Anotar a URL `*.run.app` para a variável `ORIGEM` do Worker.

### 6. Cloud Scheduler (depois do primeiro deploy e do Worker)
Fuso `America/Sao_Paulo`, região `us-east4`, cabeçalho `X-Job-Token`, retry com backoff curto:
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
4. Connection strings, ambas com `SSL Mode=Require`:
   - **direta** (host sem `-pooler`) com `acordu_migrator` → secret do Actions (migrations e `pg_dump`);
   - **pooled** (host com `-pooler`) com `acordu_app` → variável da API.

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
- **`acordu-api-anexos`:** *Object Read & Write* só em `acordu-anexos` → variáveis `Armazenamento__ChaveAcesso` / `__ChaveSecreta` da API.
- **`acordu-backup`:** *Object Read & Write* só em `acordu-backups` → secret do Actions.

Endpoint S3: `https://<id-da-conta>.r2.cloudflarestorage.com`, região `auto`. Conferir no primeiro deploy que a URL pré-assinada de download funciona (teste manual de anexo).

### 3. Pages
Projeto **`acordu-web`** (upload direto pelo `wrangler pages deploy`, sem integração Git), domínio `acordu.app.br`. Token do Cloudflare restrito a Pages desse projeto e Workers do script `acordu-api-borda`.

### 4. Worker da borda
1. Script **`acordu-api-borda`** publicado pelo `deploy.yml` (`infra/cloudflare/api-borda`).
2. Rota/domínio personalizado **`api.acordu.app.br`**.
3. Variável `ORIGEM` = URL `*.run.app`; segredo `TOKEN_ORIGEM` (gerar 32 bytes aleatórios; o mesmo valor vai para `Proxy__TokenOrigem` da API). O `wrangler.toml` tem `keep_vars = true`: os deploys do `deploy.yml` preservam a `ORIGEM` definida no painel.

---

## Parte D — Resend

O Acordu envia e-mail para os **clientes dos clientes**: reputação de domínio importa.
1. Adicionar `send.acordu.app.br` no Resend; criar no Cloudflare os registros indicados (SPF, DKIM, MX de retorno).
2. **DMARC:** `TXT _dmarc.acordu.app.br "v=DMARC1; p=none; rua=mailto:<seu-email>"` (subir para `quarantine` após algumas semanas).
3. Remetente `nao-responda@send.acordu.app.br`; chave de API exclusiva do Acordu.

---

## Parte E — Monitoramento

- **UptimeRobot:** monitor HTTP em `https://api.acordu.app.br/api/health`, 5 min, alerta por e-mail.
- Sentry (projeto `acordu`) e webhook do Discord.

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
→ primeiro deploy (cria o serviço Cloud Run, o Worker e o Pages)
C3/C4. Domínios do Pages e do Worker; ORIGEM e TOKEN_ORIGEM
A6. Cloud Scheduler (tick e diário) + alerta de falha
E.  UptimeRobot + Sentry + Discord
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
- [ ] UptimeRobot ativo.
- [ ] Backup diário rodando e restore testado.

---

## Estado provisionado

_(preencher conforme executar)_

| Recurso | Valor |
|---|---|
| Conta de faturamento | `nebrys-billing` — _pendente_ |
| Projeto Google | `acordu-prod` — _pendente_ |
| Artifact Registry | `us-east4/acordu` — _pendente_ |
| WIF | pool `github` / provider `acordu-repo` — _pendente_ |
| Neon | projeto `acordu` (PG 17, `aws-us-east-1`) — _pendente_ |
| R2 | `acordu-anexos`, `acordu-backups` — _pendente_ |
| Worker | `acordu-api-borda` — _pendente_ |
| Pages | `acordu-web` — _pendente_ |
| Domínios | `acordu.app.br`, `acordu.com.br` — _registrar_ |
| Resend | domínio verificado? _pendente_ |
| UptimeRobot | _pendente_ |

> Regra de segurança: IDs numéricos, chaves, senhas e connection strings **não** entram neste documento nem no repositório.
