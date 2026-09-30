# Acordu — Estratégia de Deploy e CI/CD (v1)

| | |
|---|---|
| **Versão** | 0.1 |
| **Data** | 30/09/2026 |
| **Alvo** | A mesma VM Oracle Cloud Always Free do Saldu (Ampere A1, ARM, Ubuntu 24.04). |
| **Base** | Estratégia de deploy do Saldu (princípios, CI, GHCR, migrations, backup, TLS). Aqui só o que é **próprio** do Acordu ou muda por **compartilhar** a VM. |

---

## 1. Princípios (herdados)

ARM64 com cross-build; vendor-neutro (Compose + Postgres + Caddy); Compose, não Kubernetes; orçamento de 2 OCPU / 12 GB — agora dividido entre dois produtos.

---

## 2. Topologia compartilhada

Com dois produtos na mesma VM, o **Caddy de borda** e o **PostgreSQL** deixam de pertencer a um produto e passam para uma **stack de infraestrutura** própria. Cada produto tem seu compose e se conecta às redes dela.

```
Internet ── 80/443
┌─────────────────────────── VM Ampere (ARM) ───────────────────────────┐
│ /opt/infra   (stack de infraestrutura)                                 │
│   caddy     ── TLS + roteamento por domínio  (rede: borda)             │
│   postgres  ── bancos: saldu, acordu         (rede: dados)             │
│                                                                        │
│ /opt/saldu   saldu-web, saldu-api            (redes: borda, dados)     │
│ /opt/acordu  acordu-web, acordu-api          (redes: borda, dados)     │
└────────────────────────────────────────────────────────────────────────┘
```

| Serviço | Imagem | Exposição | Papel |
|---|---|---|---|
| `caddy` (infra) | `caddy:alpine` | 80/443 | TLS automático; importa um bloco de site por produto. |
| `postgres` (infra) | `postgres:alpine` | interna | Um banco e um usuário por produto. |
| `acordu-web` | `acordu-web` | interna | Servidor estático do SPA (Caddy `file_server`). |
| `acordu-api` | `acordu-api` (.NET 10) | interna | API + HostedServices (agendador, outbox de e-mail, alertas de SLA). |

**Impacto no Saldu (a propagar no repositório dele):** hoje o `web` do Saldu é o Caddy de borda. Com a stack de infra, ele vira um `saldu-web` estático interno, igual ao do Acordu, e o Postgres sai do compose do Saldu. Fazer essa migração **uma vez**, antes do primeiro deploy do Acordu, com backup do banco do Saldu antes.

### 2.1 Roteamento (bloco `docker/Caddyfile.acordu`, importado pelo Caddy de infra)
```
acordu.app.br {
    encode zstd gzip
    handle /api/* {
        reverse_proxy acordu-api:8080
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
O Caddy de infra tem `import /etc/caddy/sites/*.caddy`; o deploy do Acordu copia o bloco para `sites/acordu.caddy` e faz `caddy reload` (sem derrubar o Saldu).

### 2.2 Limites de recursos
Cada serviço de produto tem `mem_limit` e `cpus` no compose, para um produto não derrubar o outro. Ponto de partida:

| Serviço | Memória | CPU |
|---|---|---|
| postgres (infra) | 3 GB | 1.0 |
| caddy (infra) | 256 MB | 0.25 |
| saldu-api / acordu-api | 1,5 GB cada | 0.75 cada |
| saldu-web / acordu-web | 64 MB cada | 0.1 cada |

Revisar com a medição real (o `docker stats` do primeiro mês).

---

## 3. CI (`ci.yml`) — a cada push/PR

Igual ao Saldu:
- **Backend:** restore → build → todos os testes (incluindo `ArchitectureTests`); integração com Testcontainers (Postgres). **(Acordu)** O teste do object storage usa um container MinIO no runner.
- **Frontend:** install → lint → Vitest → build.
- Merge em `main` só com tudo verde e PR revisado.

---

## 4. Build e publicação

Igual ao Saldu: Buildx + QEMU `linux/arm64`, tag por SHA + `latest`, push no GHCR. Imagens: `ghcr.io/<usuario>/acordu-api` e `acordu-web`.

---

## 5. Deploy (`deploy.yml`)

1. SSH na VM com a **chave de deploy do Acordu** (uma chave por repositório, revogável sozinha).
2. `cd /opt/acordu && docker compose pull`.
3. **Migrations:** migration bundle do EF Core rodando contra o banco `acordu` com o usuário `acordu_migrator` (dono do schema); só depois `docker compose up -d`.
4. Atualizar `sites/acordu.caddy` se mudou e `docker exec infra-caddy-1 caddy reload`.
5. Healthcheck `GET /api/health` via Caddy; falhou → redeploy da tag SHA anterior.
6. Discord: falha sempre; sucesso com a tag.

---

## 6. Banco de dados

- Banco `acordu` na instância compartilhada.
- Dois usuários: `acordu_migrator` (DDL, só no deploy) e `acordu_app` (DML, usado pela API). Nenhum dos dois tem `CONNECT` no banco `saldu`, e vice-versa.
- `REVOKE ALL ON DATABASE acordu FROM PUBLIC`.
- Pool: `Maximum Pool Size=20` na connection string (o Postgres compartilhado fica com `max_connections` 100).
- Extensões `pg_trgm` e `unaccent` criadas pela migration (o `acordu_migrator` precisa de permissão para `CREATE EXTENSION` nesse banco — concedida uma vez no setup).

---

## 7. Segredos

- **GitHub Actions (repositório acordu):** token GHCR, chave SSH de deploy do Acordu, host/usuário, webhook do Discord.
- **VM (`/opt/acordu/.env`, fora do git, `chmod 600`):** connection strings (`acordu_app` e `acordu_migrator`), chave de assinatura do JWT **própria** (não reutilizar a do Saldu), chave de API do Resend, credenciais do object storage (Customer Secret Key restrita aos buckets do Acordu), DSN do Sentry, webhook do Discord.

---

## 8. Backup e restore

- **Banco:** `pg_dump -Fc acordu` diário → bucket `acordu-backups`; retenção de 14 dias por *lifecycle policy* do bucket.
- **Anexos:** o bucket `acordu-anexos` tem **versionamento** ligado e regra de lifecycle que apaga versões antigas após 30 dias — protege contra exclusão acidental sem duplicar tudo.
- **Restore testado** ao menos uma vez antes de ter o primeiro cliente real: restaurar o dump num banco `acordu_restore` e subir a API contra ele.
- Discord: falha sempre; heartbeat diário de sucesso (um por produto).

---

## 9. Monitoramento

Herdado (healthchecks, uptime externo, Sentry, Discord). **(Acordu)** Healthcheck extra: idade do último ciclo do agendador (se o HostedService parou, o `/api/health` responde degradado).

---

## 10. Checklist de riscos

- [ ] Stack de infra criada e Saldu migrado para ela (com backup antes).
- [ ] Banco `acordu` isolado do `saldu` (usuários e `REVOKE` conferidos).
- [ ] Limites de memória/CPU aplicados e medidos.
- [ ] Bloco do Caddy do Acordu recarrega sem afetar o Saldu.
- [ ] Chave JWT e chave SSH de deploy próprias do Acordu.
- [ ] Buckets privados; Customer Secret Key restrita aos buckets do Acordu.
- [ ] Backup do banco + versionamento dos anexos + restore testado.
- [ ] Domínio de envio verificado no Resend (SPF/DKIM) antes do primeiro e-mail a cliente.
- [ ] Cota de 20 GB de object storage do Always Free acompanhada (é da tenancy toda: backups dos dois produtos + anexos).
