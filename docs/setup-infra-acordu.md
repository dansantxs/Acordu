# Acordu — Setup de Infraestrutura na Oracle Cloud

| | |
|---|---|
| **Versão** | 0.3 |
| **Data** | 06/10/2026 |
| **Objetivo** | Preparar na OCI tudo o que o Acordu precisa **além** do que a infraestrutura compartilhada já provê. |
| **Natureza** | Runbook operacional (ordem de execução) + registro do estado provisionado (ver fim). |

> **Premissa (ADRs 0001 e 0006):** o Acordu roda **na VM compartilhada**, como inquilino da infra compartilhada (nível 0 do ADR 0006). Tenancy **`dansantos`**, região home **`us-phoenix-1`**, conta recuperada em 06/10/2026 com a VM Ampere existente. A conta, a região, a rede, a VM, o SO e o Docker já existem e **não** são refeitos aqui — estão descritos no ADR 0006 e na §0.1. Ver §9 para a alternativa de VM separada e o ADR 0006 para os níveis de escala e o plano B.

> A versão 0.2 deste runbook (`setup-infra-acordu.md`, Google Cloud + Neon + Cloudflare Pages) pertence ao ADR 0005, revogado e mantido como plano B; está no histórico do git (commit `a635190`).

> **Aviso de recência:** a Oracle mudou os limites do Always Free sem aviso em jun/2026. Confirme limites vigentes (compute **e** object storage) antes de provisionar.

---

## 0. Nomenclatura da infraestrutura

Infra **compartilhada** recebe nome de **função**; recurso **exclusivo** de um produto recebe o nome do produto. Assim um rebatismo de produto nunca obriga a renomear (ou recriar) infraestrutura.

| Recurso | Nome | Compartimento |
|---|---|---|
| Compartimento da infra compartilhada | `infra` | raiz da tenancy |
| VCN | `infra-vcn` (CIDR `10.0.0.0/16`) | `infra` |
| Subnet pública | `infra-subnet-publica` (`10.0.0.0/24`) | `infra` |
| VM Ampere | `vps-arm-01` | `infra` |
| IP público reservado | `vps-arm-01-ip` | `infra` |
| Recursos só do Acordu (buckets, IAM) | prefixo `acordu-` | `acordu` |
| Stack compartilhada na VM | `/opt/infra` (Caddy + PostgreSQL 17) | — |
| Rede Docker compartilhada | `infra_net` (externa, criada pela stack de infra) | — |
| Stacks dos produtos na VM | `/opt/<produto>` (ex.: `/opt/acordu`) | — |

O sufixo `-01` reserva espaço para uma segunda VM (§9).

**Migração dos nomes antigos** (recursos criados antes com nome de produto):
- Compartimento, VM e IP reservado: renomear pelo console (sem impacto).
- VCN: **não** é renomeável; trocar o nome exige recriar a VCN **e a VM**. Fazer agora (nada em produção) num horário com tempo para insistir se faltar capacidade Ampere em PHX, **ou** manter a VCN antiga como exceção documentada até a próxima recriação da VM — nunca recriar só por causa do nome com produção no ar.

## 0.1 O que já existe e o Acordu reaproveita

| Recurso | Situação |
|---|---|
| Tenancy `dansantos`, alerta de billing | Reaproveitado. |
| MFA | **Revisar depois da recuperação:** MFA ativo, **códigos de recuperação** gerados e guardados fora da Oracle, **segundo fator** cadastrado (ADR 0006 §5). |
| Região home (`us-phoenix-1`) | Reaproveitada (imutável). |
| `infra-vcn`, subnet pública, IGW, security list 80/443/22 | Reaproveitadas — o Acordu não abre porta nova. |
| `vps-arm-01` (Ampere 2 OCPU / 12 GB, Ubuntu 24.04, iptables 80/443, Docker) | Reaproveitada. |
| `vps-arm-01-ip` | **Associar à VM antes do DNS do Acordu** (pendência que passa a ser bloqueante). |

---

## Parte A — Repositório

1. Repositório `acordu` no GitHub (privado).
2. Estrutura conforme `estrutura-repositorio-acordu.md`; documentos em `docs/`.
3. Estratégia de produto e cronograma em `docs/` (repositório privado; revisar antes de abrir o código).
4. Primeiro commit + push.

---

## Parte B — Oracle Cloud (só o que é novo)

### 1. Compartimento
- Criar o compartimento **`acordu`** para os recursos **exclusivos** do Acordu (buckets e políticas).
- A VM, a rede e o IP ficam no compartimento `infra` (compartilhado). Cada produto segue o mesmo padrão, com compartimento próprio para os seus recursos.

### 2. Object Storage
Criar dois buckets no compartimento `acordu`, ambos **privados** (sem acesso público) e com criptografia gerenciada pela Oracle:

| Bucket | Uso | Configuração |
|---|---|---|
| `acordu-anexos` | Anexos das solicitações (runtime) | **Versionamento ligado**; lifecycle: excluir versões não atuais após 30 dias. |
| `acordu-backups` | Dumps diários do banco | Lifecycle: excluir objetos com mais de 14 dias. |

Anotar o **namespace** do Object Storage da tenancy (aparece nos detalhes do bucket) — entra no endpoint S3.

> **Cota:** os 20 GB gratuitos de Object Storage são da **tenancy inteira** (backups de todos os produtos + anexos do Acordu). Criar alerta de uso (via Monitoring/Budgets) em ~15 GB.

### 3. Identidade de aplicação (acesso da API aos buckets)
A API acessa o object storage pela **API compatível com S3**, que usa *Customer Secret Keys*.

1. Criar o usuário IAM **`acordu-app`** (sem console, sem senha).
2. Criar o grupo **`acordu-app-group`** e adicionar o usuário.
3. Política no compartimento `acordu`:
   ```
   Allow group acordu-app-group to read buckets in compartment acordu
   Allow group acordu-app-group to manage objects in compartment acordu
     where any { target.bucket.name = 'acordu-anexos', target.bucket.name = 'acordu-backups' }
   ```
4. Gerar uma **Customer Secret Key** para `acordu-app` → access key + secret. Guardar no `.env` da VM (nunca no repositório).
5. Endpoint S3: `https://<namespace>.compat.objectstorage.us-phoenix-1.oraclecloud.com`, região `us-phoenix-1`, *path-style*.
6. Validar na implementação que a URL pré-assinada (SigV4) funciona no endpoint compatível; se não, usar *Pre-Authenticated Requests* da OCI com expiração curta (registrar a escolha em ADR).

> Por que um usuário dedicado: se a chave vazar, o estrago fica limitado aos buckets do Acordu; os outros produtos e a tenancy não são afetados. Revogar = apagar a Customer Secret Key.

### 4. Banco de dados (na VM, no Postgres compartilhado)
Via `docker exec` no container do Postgres 17 da stack de infra (ver `deploy-ci-acordu-v1.md` §2):

```sql
CREATE ROLE acordu_migrator LOGIN PASSWORD '<gerar>';
CREATE ROLE acordu_app      LOGIN PASSWORD '<gerar>';
CREATE DATABASE acordu OWNER acordu_migrator;
REVOKE ALL ON DATABASE acordu FROM PUBLIC;
GRANT CONNECT ON DATABASE acordu TO acordu_app;
\c acordu
ALTER DEFAULT PRIVILEGES FOR ROLE acordu_migrator IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO acordu_app;
ALTER DEFAULT PRIVILEGES FOR ROLE acordu_migrator IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO acordu_app;
GRANT USAGE ON SCHEMA public TO acordu_app;
```
Conferir: `acordu_app` **não** consegue conectar em bancos de outros produtos, e os usuários deles não conseguem `\c acordu`.

Extensões (`pg_trgm`, `unaccent`) são criadas pela primeira migration; se a imagem exigir superusuário para isso, criar uma vez aqui com o usuário `postgres`.

### 5. Pastas e usuário de deploy (na VM)
- `/opt/acordu/` com `docker-compose.yml` (serviços na rede externa `infra_net`) e `.env` (`chmod 600`, dono `deploy`).
- Timer dos jobs (`acordu-jobs.timer`), conforme `deploy-ci-acordu-v1.md` §5.1; `JOBS__TOKEN` no `.env`.
- Usuário `deploy` compartilhado da VM, mas **chave SSH própria do repositório Acordu** em `~deploy/.ssh/authorized_keys`, com comentário `acordu-deploy` (revogável sem afetar outros produtos).

### 6. DNS
- Registrar **`acordu.app.br`** e **`acordu.com.br`** no registro.br.
- Delegar para o **Cloudflare** (serviço complementar do ADR 0001; só DNS) e criar:
  - `A acordu.app.br` → `vps-arm-01-ip` (proxy Cloudflare **desligado** até o Caddy emitir o primeiro certificado; depois ligar, com SSL "Full (strict)");
  - `CNAME www` → `acordu.app.br`;
  - `acordu.com.br`: redirecionamento para `acordu.app.br` (regra do Cloudflare).
- Aguardar propagação antes do primeiro deploy (o Caddy precisa do DNS para o Let's Encrypt).

### 7. E-mail transacional (Resend)
O Acordu envia e-mail para os **clientes dos clientes** — reputação de domínio importa.
1. Adicionar o domínio `acordu.app.br` no Resend.
2. Criar no Cloudflare os registros que o Resend indicar (SPF, DKIM e MX de retorno em subdomínio, ex.: `send.acordu.app.br`).
3. Publicar **DMARC**: `TXT _dmarc.acordu.app.br "v=DMARC1; p=none; rua=mailto:<seu-email>"` (subir para `quarantine` após algumas semanas sem problema).
4. Remetente padrão: `notificacoes@acordu.app.br`; `Reply-To` do prestador quando fizer sentido.
5. Chave de API do Resend **exclusiva do Acordu** no `.env`.

### 8. GHCR
Na VM, o `docker login ghcr.io` já existente serve se o token tiver `read:packages` para as duas imagens; senão, gerar um token que cubra ambas.

---

## Ordem resumida

```
A.  Repo acordu (estrutura + docs + push)
B0. Pré-requisito: MFA revisado (códigos de recuperação + segundo fator), nomes por função aplicados (§0), stack /opt/infra (Caddy + Postgres) criada e produtos existentes migrados para ela
B1. Compartimento acordu
B2. Buckets acordu-anexos (versionado) e acordu-backups (lifecycle 14d) + alerta de cota
B3. Usuário acordu-app + grupo + política + Customer Secret Key
B4. Banco acordu + roles acordu_migrator/acordu_app
B5. /opt/acordu + chave SSH de deploy própria + timer dos jobs
B6. vps-arm-01-ip associado → domínios → Cloudflare → DNS
B7. Resend: domínio verificado (SPF/DKIM/DMARC)
B8. GHCR com acesso às imagens do Acordu
→ pronto para o primeiro deploy
```

---

## Checklist de "pronto"

- [ ] MFA da tenancy com códigos de recuperação guardados fora da Oracle e segundo fator cadastrado.
- [ ] Nomes por função aplicados (compartimento `infra`, `vps-arm-01`, `vps-arm-01-ip`; VCN `infra-vcn` ou exceção documentada).
- [ ] Stack `/opt/infra` (Caddy + PostgreSQL 17, rede `infra_net`) no ar; produtos existentes funcionando depois da migração.
- [ ] `vps-arm-01-ip` associado à VM.
- [ ] Compartimento `acordu` criado.
- [ ] Buckets privados criados, versionamento e lifecycle configurados.
- [ ] Alerta de uso do Object Storage (~15 GB).
- [ ] `acordu-app` com Customer Secret Key; política limitada aos dois buckets.
- [ ] Banco `acordu` e roles criados; isolamento em relação aos bancos de outros produtos conferido.
- [ ] `/opt/acordu/.env` com permissões 600.
- [ ] Chave SSH de deploy do Acordu instalada.
- [ ] `acordu.app.br` e `acordu.com.br` registrados e no Cloudflare.
- [ ] Domínio verificado no Resend + DMARC publicado.
- [ ] `docker pull` das imagens do Acordu funcionando na VM.
- [ ] Timer dos jobs ativo e falha indo para o Discord.
- [ ] Restore do backup testado (`deploy-ci-acordu-v1.md` §8).

---

## 9. Alternativa registrada: VM separada

O Always Free dá **uma cota** de Ampere (hoje ~2 OCPU / 12 GB) para a tenancy; ela pode ser dividida em duas VMs (ex.: 1 OCPU / 6 GB cada).

| | Mesma VM (escolhida) | Duas VMs |
|---|---|---|
| Isolamento | Por container e limites | Total (SO, disco, rede) |
| RAM útil | 12 GB compartilhados, um Postgres | 6 GB cada, dois Postgres |
| Operação | Uma VM para cuidar | Duas VMs, dois backups de SO, dois iptables |
| Custo | Zero | Zero, mas cada VM fica apertada |

**Gatilho de revisão:** se um produto passar a disputar recursos com o outro (medido pelo `docker stats` e pelos healthchecks), ou se um deles precisar sair do free tier. Os níveis seguintes (VM na Magalu Cloud em São Paulo; VM maior ou VMs por produto) e o plano B estão no ADR 0006.

---

## Estado provisionado

_(preencher conforme executar)_

| Recurso | Valor |
|---|---|
| Tenancy / região | `dansantos` / `us-phoenix-1` — recuperada em 06/10/2026 |
| Compartimento compartilhado | `infra` — _renomear o compartimento antigo_ |
| VCN | `infra-vcn` — _recriar ou manter a VCN antiga como exceção_ |
| VM / IP | `vps-arm-01` / `vps-arm-01-ip` — _renomear_ |
| Compartimento do Acordu | `acordu` — _pendente_ |
| Buckets | `acordu-anexos`, `acordu-backups` — _pendente_ |
| Namespace Object Storage | _preencher_ |
| Usuário IAM | `acordu-app` — _pendente_ |
| Banco | `acordu` — _pendente_ |
| Domínios | `acordu.app.br`, `acordu.com.br` — _registrar_ |
| Resend | domínio verificado? _pendente_ |

> Regra de segurança: OCIDs, chaves e senhas **não** entram neste documento nem no repositório.
