# Acordu — Estratégia de Deploy e CI/CD (v1)

| | |
|---|---|
| **Versão** | 0.3 |
| **Data** | 06/10/2026 |
| **Alvo** | Nível 0 do ADR 0006: VM compartilhada `vps-arm-01` (Oracle Cloud Always Free, tenancy `dansantos`, `us-phoenix-1`, Ampere A1, ARM, Ubuntu 24.04), no compartimento `infra`. |
| **Base** | ADR 0006 (princípios, CI, GHCR, migrations, backup, TLS). Aqui o que é **próprio** do Acordu e o que muda por **compartilhar** a VM. Provisionamento em `setup-infra-oracle-acordu.md`. |

> A versão 0.2 (05/10/2026) descrevia a hospedagem serverless do ADR 0005, revogado em 06/10/2026 e mantido como plano B; ela está no histórico do git (commit `a635190`).

---

## 1. Princípios

ARM64 com cross-build; vendor-neutro (Compose + Postgres + Caddy); Compose, não Kubernetes; orçamento de 2 OCPU / 12 GB — dividido entre os produtos da VM. Regras do ADR 0006 §5: nenhum SDK de provedor no código, configuração só por variável de ambiente, migrations aditivas, PostgreSQL 17 em todos os ambientes, backup diário com restore testado.

---

## 2. Topologia compartilhada

Com mais de um produto na mesma VM, o **Caddy de borda** e o **PostgreSQL** deixam de pertencer a um produto e passam para uma **stack de infraestrutura** própria. Cada produto tem seu compose e se conecta à rede compartilhada `infra_net` (externa, criada pela stack de infra). O Acordu é **inquilino**: não altera `/opt/infra`, só adiciona o bloco de site e recarrega o Caddy.

```
Internet ── 80/443
┌──────────────────────── vps-arm-01 (Ampere ARM) ──────────────────────┐
│ /opt/infra   (stack de infraestrutura)                                 │
│   caddy     ── TLS + roteamento por domínio  (rede: infra_net)         │
│   postgres  ── PostgreSQL 17; um banco por produto (rede: infra_net) │
│                                                                        │
│ /opt/<prod>  <prod>-web, <prod>-api          (rede: infra_net)         │
│ /opt/acordu  acordu-web, acordu-api          (rede: infra_net)         │
└────────────────────────────────────────────────────────────────────────┘
```

| Serviço | Imagem | Exposição | Papel |
|---|---|---|---|
| `caddy` (infra) | `caddy:alpine` | 80/443 | TLS automático; importa um bloco de site por produto. |
| `postgres` (infra) | `postgres:17` | interna | Um banco e usuários próprios por produto. Mesma versão de dev, testes e qualquer destino de migração (ADR 0006). |
| `acordu-web` | `acordu-web` | interna | Servidor estático do SPA (Caddy `file_server`). |
| `acordu-api` | `acordu-api` (.NET 10) | interna | API. Jobs (recorrências, fechamento automático, alertas de SLA, varredura da outbox) são endpoints `/internal/jobs/*` disparados por timer na VM (§5.1). |

**Pré-requisito:** a stack `/opt/infra` (Caddy de borda + PostgreSQL) precisa existir antes do primeiro deploy do Acordu. Produto que hoje embute o próprio Caddy ou Postgres migra para ela **uma vez**, com backup antes.

### 2.1 Roteamento (bloco `docker/Caddyfile.acordu`, importado pelo Caddy de infra)
```
acordu.app.br {
    encode zstd gzip
    handle /api/* {
        reverse_proxy acordu-api:8080
    }
    handle /internal/* {
        respond 404
    }
    handle {
        reverse_proxy acordu-web:80
    }
    header {
        Strict-Transport-Security "max-age=31536000; includeSubDomains"
        X-Content-Type-Options nosniff
        Referrer-Policy same-origin
    }
}
www.acordu.app.br {
    redir https://acordu.app.br{uri} permanent
}
```
`/internal/*` não sai pela borda: os jobs são chamados de dentro da `infra_net`. O Caddy de infra tem `import /etc/caddy/sites/*.caddy`; o deploy do Acordu copia o bloco para `sites/acordu.caddy` e faz `caddy reload` (sem derrubar os outros sites).

### 2.2 Limites de recursos
Cada serviço de produto tem `mem_limit` e `cpus` no compose, para um produto não derrubar o outro. Ponto de partida:

| Serviço | Memória | CPU |
|---|---|---|
| postgres (infra) | 3 GB | 1.0 |
| caddy (infra) | 256 MB | 0.25 |
| acordu-api (e cada API de outro produto) | 1,5 GB | 0.75 |
| acordu-web (e cada web de outro produto) | 64 MB | 0.1 |

Revisar com a medição real (o `docker stats` do primeiro mês).

---

## 3. CI (`ci.yml`) — a cada push/PR

Pipeline:
- **Backend:** restore → build → todos os testes (incluindo `ArchitectureTests`); integração com Testcontainers (`postgres:17`, mesma versão da VM). **(Acordu)** O teste do object storage usa o emulador Adobe S3Mock (Testcontainers) no runner — as imagens públicas do MinIO deixaram de ser distribuídas; os testes funcionais usam um armazenamento em memória.
- **Frontend:** install → lint → Vitest → build.
- Merge em `main` só com tudo verde e PR revisado.

---

## 4. Build e publicação

Buildx + QEMU `linux/arm64`, tag por SHA + `latest`, push no GHCR. Imagens: `ghcr.io/<usuario>/acordu-api` e `acordu-web`.

---

## 5. Deploy (`deploy.yml`)

1. SSH na VM com a **chave de deploy do Acordu** (uma chave por repositório, revogável sozinha).
2. `cd /opt/acordu && docker compose pull`.
3. **Migrations:** migration bundle do EF Core rodando contra o banco `acordu` com o usuário `acordu_migrator` (dono do schema); só depois `docker compose up -d`. Migrations são **aditivas** (a versão anterior da API funciona com o schema novo), então o rollback do passo 5 não exige desfazer migration.
4. Atualizar `sites/acordu.caddy` se mudou e `docker exec infra-caddy-1 caddy reload`.
5. Healthcheck `GET /api/health` via Caddy; falhou → redeploy da tag SHA anterior.
6. Discord: falha sempre; sucesso com a tag.

### 5.1 Jobs agendados

Os jobs já são endpoints internos idempotentes (`POST /internal/jobs/<nome>`, cabeçalho `X-Job-Token` comparado em tempo constante, 404 sem token válido), o que mantém o mesmo código no nível 0 e no plano B (ADR 0005).

- Na VM, um **timer do systemd** (`acordu-jobs.timer`) chama `POST http://acordu-api:8080/internal/jobs/tick` a cada 5 minutos, a partir de um container efêmero na `infra_net` (`docker run --rm --network infra_net curlimages/curl ...`), com o token lido do `.env`. Jobs diários (`fechamento-automatico`, `expurgo-auditoria` e `expurgo-notificacoes`) usam um segundo timer.
- O tick entrega o que a outbox da requisição não conseguiu entregar e roda os alertas de SLA; os diários cuidam de recorrências e fechamento automático.
- Implementados: `POST /internal/jobs/tick` (geração das tarefas recorrentes, outbox e alertas de SLA; responde `{ tarefasRecorrentes, outboxProcessadas, alertasSla, falhas }`) e `POST /internal/jobs/fechamento-automatico` (diário; responde `{ tenants, fechadas, falhas }`). Jobs por tenant rodam cada tenant num escopo próprio; falha num tenant é logada, não para os demais e entra em `falhas` (o timer só alerta em resposta não-2xx).
- Resposta não-2xx ou falha do timer → Discord (`OnFailure=` do serviço).

---

## 6. Banco de dados

- Banco `acordu` na instância compartilhada (`postgres:17` em `/opt/infra`).
- Dois usuários: `acordu_migrator` (DDL, só no deploy) e `acordu_app` (DML, usado pela API). Nenhum dos dois tem `CONNECT` em bancos de outros produtos, e vice-versa.
- `REVOKE ALL ON DATABASE acordu FROM PUBLIC`.
- Pool: `Maximum Pool Size=20` na connection string (o Postgres compartilhado fica com `max_connections` 100).
- Extensões `pg_trgm` e `unaccent` criadas pela migration (o `acordu_migrator` precisa de permissão para `CREATE EXTENSION` nesse banco — concedida uma vez no setup).

---

## 7. Segredos

- **GitHub Actions (repositório acordu):** token GHCR, chave SSH de deploy do Acordu, host/usuário, webhook do Discord.
- **VM (`/opt/acordu/.env`, fora do git, `chmod 600`):** connection strings (`acordu_app` e `acordu_migrator`), chave de assinatura do JWT **própria** (nunca compartilhada com outro produto), chave de API do Resend (`Email__ResendChave`; remetente em `Email__Remetente`, padrão `nao-responda@send.acordu.app.br`, e nome padrão em `Email__NomeRemetente`; sem a chave, a API só registra os e-mails em log), endereço público do front (`App__UrlPublica`, base dos links dos e-mails e do feed ICS da agenda, que passa pelo `/api` do Caddy), limites opcionais (`RateLimit__Auth__PorMinuto` 10, `RateLimit__Portal__PorMinuto` 10, `RateLimit__Agenda__PorMinuto` 30), `Proxy__ConfiarEncaminhamento=true` (IP real do cliente pelo `X-Forwarded-For` do Caddy, para auditoria e rate limit; ADR 0008), object storage (`Armazenamento__Endpoint` = `https://{namespace}.compat.objectstorage.{regiao}.oraclecloud.com`, `Armazenamento__Regiao`, `Armazenamento__Bucket` padrão `acordu-anexos`, `Armazenamento__ChaveAcesso`/`Armazenamento__ChaveSecreta` da Customer Secret Key restrita aos buckets do Acordu; sem endpoint, anexos ficam indisponíveis), DSN do Sentry, webhook do Discord.

---

## 8. Backup e restore

- **Banco:** `pg_dump -Fc acordu` diário → bucket `acordu-backups`; retenção de 14 dias por *lifecycle policy* do bucket.
- **Anexos:** o bucket `acordu-anexos` tem **versionamento** ligado e regra de lifecycle que apaga versões antigas após 30 dias — protege contra exclusão acidental sem duplicar tudo.
- **Restore testado** ao menos uma vez antes de ter o primeiro cliente real: restaurar o dump num banco `acordu_restore` e subir a API contra ele.
- Discord: falha sempre; heartbeat diário de sucesso (um por produto).

---

## 9. Monitoramento

Base: healthchecks, uptime externo, Sentry, Discord. **(Acordu)** O `/api/health` não consulta o banco; a saúde dos jobs vem do timer (falha → Discord, §5.1) e do heartbeat diário.

---

## 10. Checklist de riscos

- [ ] Nomes por função da infra aplicados (`vps-arm-01`, `infra`, `infra-vcn`, `infra_net`; ver `setup-infra-oracle-acordu.md` §0).
- [ ] Stack `/opt/infra` criada e produtos existentes migrados para ela (com backup antes).
- [ ] Banco `acordu` isolado dos bancos de outros produtos (usuários e `REVOKE` conferidos).
- [ ] Limites de memória/CPU aplicados e medidos.
- [ ] Bloco do Caddy do Acordu recarrega sem afetar os outros sites; `/internal/*` responde 404 pela borda.
- [ ] Timer dos jobs ativo; endpoints respondem 404 sem token e são idempotentes.
- [ ] Postgres 17 em dev, testes e VM.
- [ ] Imagem da API com `tzdata` e ICU (o cálculo de SLA resolve fusos IANA como `America/Sao_Paulo`; imagens Alpine não trazem por padrão).
- [ ] Chave JWT e chave SSH de deploy próprias do Acordu.
- [ ] Buckets privados; Customer Secret Key restrita aos buckets do Acordu.
- [ ] Backup do banco + versionamento dos anexos + restore testado.
- [ ] Domínio de envio verificado no Resend (SPF/DKIM) antes do primeiro e-mail a cliente.
- [ ] Cota de 20 GB de object storage do Always Free acompanhada (é da tenancy toda: backups de todos os produtos + anexos).
