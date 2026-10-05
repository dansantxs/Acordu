# Acordu — Setup de Infraestrutura (nível 0)

| | |
|---|---|
| **Versão** | 0.2 |
| **Data** | 05/10/2026 |
| **Objetivo** | Provisionar o nível 0 do ADR 0005: Google Cloud (Cloud Run, Artifact Registry, Cloud Storage, Scheduler), Neon, Cloudflare e Resend. |
| **Natureza** | Runbook operacional (ordem de execução) + registro do estado provisionado (ver fim). |

> Substitui o runbook da Oracle Cloud (versão 0.1), descartado quando o Saldu perdeu a conta Oracle. O histórico está no git.

> **Aviso de recência:** free tiers mudam sem aviso (a Oracle mudou o Always Free em jun/2026). Antes de provisionar, confirme os limites vigentes do Cloud Run, Cloud Scheduler, Cloud Storage, Artifact Registry e do plano Free do Neon.

---

## 0. Nomenclatura

Recurso **exclusivo** do Acordu leva o nome do produto; o que é da **empresa** leva nome de função ou da Utile. No nível 3 (VM compartilhada) volta a regra de nomes por função para a infra compartilhada.

| Recurso | Nome |
|---|---|
| Conta de faturamento (compartilhada com o Saldu) | `utile-billing` |
| Projeto Google | `acordu-prod` |
| Serviço Cloud Run | `acordu-api` (`us-east4`) |
| Repositório do Artifact Registry | `acordu` (`us-east4`, Docker) |
| Buckets | `acordu-anexos`, `acordu-backups` |
| Service accounts | `acordu-api`, `acordu-deployer`, `acordu-backup` |
| Pool de Workload Identity | `github` (provider `acordu-repo`) |
| Job do Cloud Scheduler | `acordu-tick` |
| Projeto Neon | `acordu` (banco `acordu`; roles `acordu_migrator`, `acordu_app`) |
| Projeto Cloudflare Pages | `acordu-web` |

---

## Parte A — Google Cloud

### 1. Projeto e faturamento
1. Criar o projeto **`acordu-prod`** e vinculá-lo à conta de faturamento **`utile-billing`**.
2. Ativar APIs: Cloud Run, Artifact Registry, Cloud Storage, Cloud Scheduler, IAM Credentials, Security Token Service.
3. **Alerta de orçamento** na `utile-billing`, escopo `acordu-prod`, valor baixo (ex.: R$ 10) com alertas em 50/90/100%. Alerta não bloqueia gasto.
4. Lembrete: a cota grátis do Cloud Run e os **3 jobs gratuitos do Cloud Scheduler** são **da conta de faturamento**, divididos com o Saldu.

### 2. Artifact Registry
- Repositório Docker **`acordu`** em `us-east4` (mesma região do Cloud Run: sem tráfego entre regiões no pull).
- **Política de limpeza:** manter as 5 versões mais recentes; apagar imagens sem tag com mais de 7 dias. Rodar primeiro em *dry run*.

### 3. Cloud Storage
Dois buckets **privados** (acesso uniforme, prevenção de acesso público ligada), numa região do free tier do Cloud Storage (`us-east1`, a mais próxima de `us-east4`):

| Bucket | Uso | Configuração |
|---|---|---|
| `acordu-anexos` | Anexos das solicitações (runtime) | **Versionamento ligado**; lifecycle: excluir versões não atuais após 30 dias. |
| `acordu-backups` | Dumps diários do banco | Lifecycle: excluir objetos com mais de 14 dias. |

> A cota gratuita de armazenamento também é da conta de faturamento (somada ao Saldu).

### 4. Service accounts e acesso
| Service account | Papéis | Uso |
|---|---|---|
| `acordu-api` | `roles/storage.objectAdmin` **só no bucket** `acordu-anexos` | Identidade do Cloud Run; dona da **chave HMAC** usada pela API S3. |
| `acordu-deployer` | `roles/run.admin`, `roles/artifactregistry.writer` (no repositório `acordu`), `roles/iam.serviceAccountUser` em `acordu-api` | Deploy pelo GitHub Actions. |
| `acordu-backup` | `roles/storage.objectCreator` **só no bucket** `acordu-backups` | Upload do `pg_dump` pelo Actions. |

**Chave HMAC** (Cloud Storage → Configurações → Interoperabilidade) para `acordu-api` → access key + secret, guardadas como secrets do GitHub (vão para variáveis do Cloud Run no deploy). Endpoint S3: `https://storage.googleapis.com`, *path-style*. Validar na implementação que a URL pré-assinada SigV4 funciona nesse endpoint; se não, registrar a alternativa em ADR.

**Nenhuma chave JSON de service account** é criada.

### 5. Workload Identity Federation
1. Pool **`github`** com provider OIDC **`acordu-repo`** (`https://token.actions.githubusercontent.com`).
2. Condição de atributo: `assertion.repository == 'dansantxs/acordu' && assertion.ref == 'refs/heads/main'`.
3. Permitir que o principal do provider use `acordu-deployer` e `acordu-backup` (`roles/iam.workloadIdentityUser`).
4. No repositório: variáveis `GCP_WIF_PROVIDER` e `GCP_PROJECT_ID` (não são segredos).

### 6. Cloud Run
Criado pelo primeiro deploy (`deploy-ci-acordu-v1.md` §4), com os parâmetros do §2.1 de lá: `us-east4`, 1 vCPU / 512 MiB, `min-instances=0`, `max-instances=2`, startup CPU boost, acesso público, service account `acordu-api`.

### 7. Cloud Scheduler
Job **`acordu-tick`** (região `us-east4`), a cada 5 min, `POST https://api.acordu.app.br/internal/jobs/tick` com cabeçalho `X-Job-Token`. Retry com backoff; tentativa máxima curta (o próximo tick cobre).

---

## Parte B — Neon

1. Projeto **`acordu`** (separado do Saldu), **Postgres 17**, região **`aws-us-east-1`**, plano Free.
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
4. Anotar as duas connection strings:
   - **direta** (host sem `-pooler`) com `acordu_migrator` → secret do Actions (migrations e `pg_dump`);
   - **pooled** (host com `-pooler`) com `acordu_app` → variável da API.
   Ambas com `SSL Mode=Require`.
5. Manter o *autosuspend* padrão do Free (não manter o banco acordado).

---

## Parte C — Cloudflare e domínios

1. Registrar **`acordu.app.br`** e **`acordu.com.br`** no registro.br e delegar para o Cloudflare.
2. **Pages:** projeto `acordu-web` ligado ao repositório (diretório `apps/web`, saída `dist`), domínio `acordu.app.br`; `www` redireciona para o apex.
3. **API:** `api.acordu.app.br` no Cloud Run por mapeamento de domínio (registro DNS indicado pelo Google, **proxy do Cloudflare desligado** para o certificado do Google ser emitido). Se o mapeamento não estiver disponível em `us-east4`, usar Pages Function como proxy de `/api/*` e registrar em ADR.
4. `acordu.com.br`: regra de redirecionamento para `acordu.app.br`.

---

## Parte D — Resend

O Acordu envia e-mail para os **clientes dos clientes** — reputação de domínio importa.
1. Adicionar `acordu.app.br` no Resend; criar no Cloudflare os registros indicados (SPF, DKIM, MX de retorno em `send.acordu.app.br`).
2. **DMARC:** `TXT _dmarc.acordu.app.br "v=DMARC1; p=none; rua=mailto:<seu-email>"` (subir para `quarantine` após algumas semanas).
3. Remetente `notificacoes@acordu.app.br`; chave de API **exclusiva do Acordu**.

---

## Parte E — Monitoramento

- **UptimeRobot:** monitor HTTP em `https://api.acordu.app.br/api/health`, intervalo de 5 min, alerta por e-mail/Discord.
- Sentry (projeto `acordu`) e webhook do Discord.

---

## Ordem resumida

```
A1. acordu-prod + utile-billing + APIs + alerta de orçamento
A2. Artifact Registry (acordu, us-east4) + limpeza
A3. Buckets acordu-anexos / acordu-backups (privados, lifecycle)
A4. Service accounts + HMAC de acordu-api
A5. WIF (pool github, provider acordu-repo)
B.  Neon: projeto, banco, roles, connection strings → secrets do GitHub
C.  Domínios → Cloudflare → Pages → mapeamento api.acordu.app.br
D.  Resend (SPF/DKIM/DMARC)
→ primeiro deploy (cria o serviço Cloud Run)
A7. Cloud Scheduler acordu-tick
E.  UptimeRobot + Sentry + Discord
```

---

## Checklist de "pronto"

- [ ] `acordu-prod` na `utile-billing`; alerta de orçamento ativo.
- [ ] Artifact Registry com política de limpeza.
- [ ] Buckets privados, versionamento e lifecycle configurados.
- [ ] Service accounts com papéis mínimos; HMAC só de `acordu-api`; nenhuma chave JSON.
- [ ] WIF restrito a `dansantxs/acordu` / `main`.
- [ ] Neon: Postgres 17, `aws-us-east-1`; `acordu_app` sem DDL; strings direta e pooled separadas.
- [ ] Cloud Run com `max-instances=2` e `/api/health` respondendo.
- [ ] `acordu-tick` no Scheduler (conferir que a conta não passou de 3 jobs somando o Saldu).
- [ ] Domínios no Cloudflare; Pages e `api.acordu.app.br` com HTTPS.
- [ ] Resend verificado + DMARC.
- [ ] UptimeRobot ativo.
- [ ] Backup diário rodando e restore testado.

---

## Estado provisionado

_(preencher conforme executar)_

| Recurso | Valor |
|---|---|
| Projeto Google | `acordu-prod` — _pendente_ |
| Conta de faturamento | `utile-billing` — _pendente_ |
| Artifact Registry | `us-east4/acordu` — _pendente_ |
| Buckets | `acordu-anexos`, `acordu-backups` — _pendente_ |
| WIF | pool `github` / provider `acordu-repo` — _pendente_ |
| Neon | projeto `acordu` (PG 17, `aws-us-east-1`) — _pendente_ |
| Domínios | `acordu.app.br`, `acordu.com.br` — _registrar_ |
| Resend | domínio verificado? _pendente_ |
| UptimeRobot | _pendente_ |

> Regra de segurança: IDs de projeto numéricos, chaves, senhas e connection strings **não** entram neste documento nem no repositório.
