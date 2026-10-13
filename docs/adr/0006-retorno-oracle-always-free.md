# ADR 0006 — Retorno à Oracle Always Free, com plano de escala

- **Status:** Aceito
- **Data:** 06/10/2026
- **Contexto do projeto:** Acordu (v1)
- **Revoga:** [ADR 0005](0005-hospedagem-serverless-gratuita.md) (hospedagem serverless), que passa a ser o **plano B**.
- **Confirma:** a hospedagem self-hosted e a infraestrutura compartilhada do ADR 0001.

## Contexto

Em 05/10/2026 a conta Oracle Cloud estava inacessível e a hospedagem foi trocada para serverless (ADR 0005). Em 06/10/2026 a conta foi recuperada: tenancy **`dansantos`**, região home **`us-phoenix-1`**, com a **VM Ampere já existente**. Nada do ADR 0005 chegou a ser provisionado.

Restrições que continuam valendo:
- **Custo zero** enquanto não houver usuário pagante.
- **Portabilidade:** perder um provedor não pode exigir reescrever código. O episódio de 05/10 mostrou que isso acontece sem aviso.
- **Dev solo:** a operação precisa ser mecânica (runbook, backup testado, deploy automatizado).

A VM Ampere (2 OCPU / 12 GB) comporta os produtos que a dividem, sem cold start e sem os limites rígidos de computação e armazenamento dos bancos serverless gratuitos. A capacidade Ampere é disputada, e recriar a VM pode não ser possível de imediato. Por isso o plano B existe.

## Decisão

### 1. Nível 0 — VM compartilhada na Oracle (Phoenix)

O Acordu e os outros produtos na **mesma VM Ampere**, com **nomes por função** na infra compartilhada (um rebatismo de produto nunca obriga a renomear infraestrutura):

| Recurso | Nome |
|---|---|
| VM Ampere (Ubuntu 24.04, ARM64) | `vps-arm-01` (IP público reservado `vps-arm-01-ip`) |
| Compartimento da infra compartilhada | `infra` |
| VCN | `infra-vcn` |
| Rede Docker compartilhada | `infra_net` |
| Stack compartilhada na VM | `/opt/infra`: **Caddy** (TLS + roteamento por domínio) e **PostgreSQL 17** |
| Stack do Acordu na VM | `/opt/acordu`: `acordu-api` e `acordu-web`, ligados à `infra_net` |
| Banco do Acordu | `acordu`, com o usuário de aplicação **`acordu_app`** (só DML) e `acordu_migrator` (dono do schema, usado só nas migrations) |
| Recursos exclusivos do Acordu na OCI | compartimento `acordu`, prefixo `acordu-` (buckets `acordu-anexos` e `acordu-backups`) |

- O Acordu é **inquilino** da infra: entrega as imagens e o bloco de site do Caddy, e não altera `/opt/infra` no deploy, a não ser recarregando o Caddy.
- Usuários de banco de outros produtos não acessam o banco `acordu`, e `acordu_app` não acessa os deles.
- Cada container de produto tem `mem_limit` e `cpus`, para um produto não derrubar o outro.
- Deploy: imagens **ARM64** no GitHub Actions → **GHCR** → VM por **SSH** (`docker compose pull && up -d`), com migrations antes do `up`.
- Arquivos: OCI Object Storage pela API S3-compatível.
- Jobs: os endpoints `/internal/jobs/*` já implementados (token `X-Job-Token`, idempotentes) são **disparados por um timer na própria VM**. Assim o mesmo código serve ao nível 0 e ao plano B.

### 2. Nível 1 — VM no Brasil (Magalu Cloud, São Paulo)

**Gatilhos (qualquer um):** usuários pagantes, cliente que exija dados no Brasil, ou VM Oracle saturada (CPU, memória ou conexões do Postgres disputadas entre os produtos).

- VM na **Magalu Cloud em São Paulo**, ~**R$ 62/mês** (2 vCPU / 4 GB), com o **mesmo modelo** do nível 0: `/opt/infra` com Caddy + Postgres 17, `/opt/<produto>`, `infra_net`, nomes por função.
- **Postgres gerenciado da Magalu é opcional**, decidido na hora da migração (custo × operação de backup).
- **Migração:** `pg_dump` → `pg_restore` no Postgres de destino, cópia dos objetos (`rclone`) para um object storage S3-compatível, troca das variáveis de ambiente e do **DNS**. Nenhuma mudança de código.

### 3. Nível 2 — mais capacidade

**VM maior** ou **VMs separadas por produto** (cada produto com o próprio `/opt/infra`), quando um produto passar a disputar recursos com o outro ou o volume justificar. A migração é a mesma do nível 1.

### 4. Plano B — serverless (ADR 0005)

Se a VM for perdida **e** não houver capacidade Ampere para recriá-la: executar o ADR 0005 (Cloud Run + Neon + Cloudflare Pages), conferindo antes os limites vigentes dos free tiers. Se a perda vier junto com um dos gatilhos do nível 1, ir direto ao nível 1.

### 5. Regras que valem em todos os níveis

- **Nenhum SDK de provedor no código.** Banco por connection string Npgsql; arquivos por API S3-compatível; e-mail pelo Resend via HTTP. *(Nota de 13/10/2026, marco 10a: a API S3 é falada pelo `AWSSDK.S3` usado só como cliente do protocolo, com endpoint configurável — funciona igual com OCI, MinIO ou outro S3; nenhum SDK da OCI entra.)*
- **Toda configuração por variável de ambiente** (connection string, endpoint e credenciais S3, chave JWT, chave do Resend, token dos jobs, CORS). Trocar de provedor é trocar variáveis.
- **Migrations aditivas:** a versão anterior da API continua funcionando com o schema novo (remoção e renomeação em duas etapas). Isso permite deploy e rollback sem janela.
- **PostgreSQL 17 em todos os ambientes:** dev (`docker-compose.dev.yml`), testes (Testcontainers `postgres:17`), VM e qualquer destino de migração. Dump e restore entre versões iguais.
- **Backup diário** do banco (`pg_dump -Fc`) para object storage fora da VM, com retenção por lifecycle e **restore testado** antes do primeiro cliente real e periodicamente depois disso.
- **MFA** em todas as contas de provedor (Oracle, GitHub, Cloudflare, Resend, Magalu), com **códigos de recuperação guardados fora do provedor** e um **segundo fator cadastrado**. A conta inacessível de 05/10 é o risco que isso cobre.

## Alternativas consideradas

- **Manter o serverless (ADR 0005):** custo zero também, mas com cold start, limites rígidos do Neon Free e cota gratuita dividida entre produtos. Com a VM de volta, não compensa. Fica como plano B.
- **Ir direto ao nível 1 (Magalu):** custo fixo sem usuário pagante. Fica como destino.
- **VMs separadas por produto desde já:** a cota Always Free é por tenancy, e dividi-la não traz ganho no volume atual.

## Consequências

**Positivas**
- Custo zero, sem cold start e sem limites rígidos de banco no nível 0.
- Infra que já existe e já foi testada; nomes por função sobrevivem a rebatismos.
- Caminho de escala com gatilhos explícitos e migração mecânica (dump/restore + DNS), e um plano B documentado para a perda da VM.

**Negativas / trade-offs**
- **Acoplamento entre produtos** na mesma VM e no mesmo Postgres (mitigado por limites por container e usuários de banco distintos).
- **Servidor para administrar** (SO, patches, Docker, backup). O runbook e o deploy automatizado reduzem, mas não eliminam, esse trabalho.
- **Dados nos EUA no nível 0:** a transferência internacional (LGPD, art. 33) precisa constar na política de privacidade; cliente que exija dados no Brasil antecipa o nível 1.
- **Dependência da conta Oracle:** a VM é ponto único de falha. Mitigação: backups fora da VM, MFA com recuperação e o plano B.
