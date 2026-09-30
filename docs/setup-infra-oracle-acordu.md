# Acordu — Setup de Infraestrutura na Oracle Cloud

| | |
|---|---|
| **Versão** | 0.1 |
| **Data** | 30/09/2026 |
| **Objetivo** | Preparar na OCI tudo o que o Acordu precisa **além** do que o Saldu já provisionou. |
| **Natureza** | Runbook operacional (ordem de execução) + registro do estado provisionado (ver fim). |

> **Premissa (ADR 0001):** o Acordu roda **na mesma VM** do Saldu. A conta, a região, a rede, a VM, o SO e o Docker já existem e **não** são refeitos aqui — o runbook do Saldu continua sendo a referência para eles. Ver §9 para a alternativa de VM separada.

> **Aviso de recência (herdado):** a Oracle mudou os limites do Always Free sem aviso em jun/2026. Confirme limites vigentes (compute **e** object storage) antes de provisionar.

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
| Recursos só do Saldu (buckets, IAM) | prefixo `saldu-` | `saldu` |
| Recursos só do Acordu (buckets, IAM) | prefixo `acordu-` | `acordu` |
| Stack compartilhada na VM | `/opt/infra` (Caddy + Postgres) | — |
| Stacks dos produtos na VM | `/opt/saldu`, `/opt/acordu` | — |

O sufixo `-01` reserva espaço para uma segunda VM (§9).

**Migração dos nomes antigos** (`pondus`, `pondus-vcn`, `pondus-vps`, `pondus-ip`):
- Compartimento, VM e IP reservado: renomear pelo console (sem impacto).
- VCN: **não** é renomeável; trocar o nome exige recriar a VCN **e a VM**. Fazer agora (nada em produção) num horário com tempo para insistir se faltar capacidade Ampere em PHX, **ou** manter `pondus-vcn` como exceção documentada até a próxima recriação da VM — nunca recriar só por causa do nome com produção no ar.

## 0.1 O que já existe e o Acordu reaproveita

| Recurso | Situação |
|---|---|
| Tenancy, MFA, alerta de billing | Reaproveitado. |
| Região home (`us-phoenix-1`) | Reaproveitada (imutável). |
| `infra-vcn`, subnet pública, IGW, security list 80/443/22 | Reaproveitadas — o Acordu não abre porta nova. |
| `vps-arm-01` (Ampere 2 OCPU / 12 GB, Ubuntu 24.04, iptables 80/443, Docker) | Reaproveitada. |
| `vps-arm-01-ip` | **Associar à VM antes do DNS do Acordu** (pendência que passa a ser bloqueante). |

---

## Parte A — Repositório

1. Repositório `acordu` no GitHub (privado).
2. Estrutura conforme `estrutura-repositorio-acordu.md`; documentos em `docs/`.
3. Estratégia de produto e cronograma **fora** do repositório (Projeto do Claude).
4. Primeiro commit + push.

---

## Parte B — Oracle Cloud (só o que é novo)

### 1. Compartimento
- Criar o compartimento **`acordu`** para os recursos **exclusivos** do Acordu (buckets e políticas).
- A VM, a rede e o IP ficam no compartimento `infra` (compartilhado). O Saldu segue o mesmo padrão, com o compartimento `saldu` para os recursos dele.

### 2. Object Storage
Criar dois buckets no compartimento `acordu`, ambos **privados** (sem acesso público) e com criptografia gerenciada pela Oracle:

| Bucket | Uso | Configuração |
|---|---|---|
| `acordu-anexos` | Anexos das solicitações (runtime) | **Versionamento ligado**; lifecycle: excluir versões não atuais após 30 dias. |
| `acordu-backups` | Dumps diários do banco | Lifecycle: excluir objetos com mais de 14 dias. |

Anotar o **namespace** do Object Storage da tenancy (aparece nos detalhes do bucket) — entra no endpoint S3.

> **Cota:** os 20 GB gratuitos de Object Storage são da **tenancy inteira** (backups do Saldu + backups do Acordu + anexos). Criar alerta de uso (via Monitoring/Budgets) em ~15 GB.

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

> Por que um usuário dedicado: se a chave vazar, o estrago fica limitado aos buckets do Acordu; o Saldu e a tenancy não são afetados. Revogar = apagar a Customer Secret Key.

### 4. Banco de dados (na VM, no Postgres compartilhado)
Via `docker exec` no container do Postgres da stack de infra (ver `deploy-ci-acordu-v1.md` §2):

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
Conferir: `acordu_app` **não** consegue `\c saldu`; o usuário do Saldu não consegue `\c acordu`.

Extensões (`pg_trgm`, `unaccent`) são criadas pela primeira migration; se a imagem exigir superusuário para isso, criar uma vez aqui com o usuário `postgres`.

### 5. Pastas e usuário de deploy (na VM)
- `/opt/acordu/` com `docker-compose.yml` e `.env` (`chmod 600`, dono `deploy`).
- Mesmo usuário `deploy` do Saldu, mas **chave SSH própria do repositório Acordu** em `~deploy/.ssh/authorized_keys`, com comentário `acordu-deploy` (revogável sem afetar o Saldu).

### 6. DNS
- Registrar **`acordu.app.br`** e **`acordu.com.br`** no registro.br.
- Delegar para o **Cloudflare** (ADR 0005 herdado) e criar:
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
Na VM, o `docker login ghcr.io` já feito para o Saldu serve se o token tiver `read:packages` para as duas imagens; senão, gerar um token que cubra ambas.

---

## Ordem resumida

```
A.  Repo acordu (estrutura + docs + push)
B0. Pré-requisito: nomes neutros aplicados (§0), stack /opt/infra (Caddy + Postgres) criada e Saldu migrado para ela
B1. Compartimento acordu
B2. Buckets acordu-anexos (versionado) e acordu-backups (lifecycle 14d) + alerta de cota
B3. Usuário acordu-app + grupo + política + Customer Secret Key
B4. Banco acordu + roles acordu_migrator/acordu_app
B5. /opt/acordu + chave SSH de deploy própria
B6. vps-arm-01-ip associado → domínios → Cloudflare → DNS
B7. Resend: domínio verificado (SPF/DKIM/DMARC)
B8. GHCR com acesso às imagens do Acordu
→ pronto para o primeiro deploy
```

---

## Checklist de "pronto"

- [ ] Nomes neutros aplicados (compartimento `infra`, `vps-arm-01`, `vps-arm-01-ip`; VCN `infra-vcn` ou exceção documentada).
- [ ] Stack `/opt/infra` no ar; Saldu funcionando depois da migração.
- [ ] `vps-arm-01-ip` associado à VM.
- [ ] Compartimento `acordu` criado.
- [ ] Buckets privados criados, versionamento e lifecycle configurados.
- [ ] Alerta de uso do Object Storage (~15 GB).
- [ ] `acordu-app` com Customer Secret Key; política limitada aos dois buckets.
- [ ] Banco `acordu` e roles criados; isolamento do banco `saldu` conferido.
- [ ] `/opt/acordu/.env` com permissões 600.
- [ ] Chave SSH de deploy do Acordu instalada.
- [ ] `acordu.app.br` e `acordu.com.br` registrados e no Cloudflare.
- [ ] Domínio verificado no Resend + DMARC publicado.
- [ ] `docker pull` das imagens do Acordu funcionando na VM.

---

## 9. Alternativa registrada: VM separada

O Always Free dá **uma cota** de Ampere (hoje ~2 OCPU / 12 GB) para a tenancy; ela pode ser dividida em duas VMs (ex.: 1 OCPU / 6 GB cada).

| | Mesma VM (escolhida) | Duas VMs |
|---|---|---|
| Isolamento | Por container e limites | Total (SO, disco, rede) |
| RAM útil | 12 GB compartilhados, um Postgres | 6 GB cada, dois Postgres |
| Operação | Uma VM para cuidar | Duas VMs, dois backups de SO, dois iptables |
| Custo | Zero | Zero, mas cada VM fica apertada |

**Gatilho de revisão:** se um produto passar a disputar recursos com o outro (medido pelo `docker stats` e pelos healthchecks), ou se um deles precisar sair do free tier.

---

## Estado provisionado

_(preencher conforme executar)_

| Recurso | Valor |
|---|---|
| Compartimento compartilhado | `infra` (ex-`pondus`) — _renomear_ |
| VCN | `infra-vcn` — _recriar ou manter `pondus-vcn` como exceção_ |
| VM / IP | `vps-arm-01` / `vps-arm-01-ip` — _renomear_ |
| Compartimento do Acordu | `acordu` — _pendente_ |
| Buckets | `acordu-anexos`, `acordu-backups` — _pendente_ |
| Namespace Object Storage | _preencher_ |
| Usuário IAM | `acordu-app` — _pendente_ |
| Banco | `acordu` — _pendente_ |
| Domínios | `acordu.app.br`, `acordu.com.br` — _registrar_ |
| Resend | domínio verificado? _pendente_ |

> Regra de segurança (herdada): OCIDs, chaves e senhas **não** entram neste documento nem no repositório.
