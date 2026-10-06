# Acordu

> Um produto **Utile Systems**.

Gestão de **atendimento, tarefas e contratos** para prestadores de serviço, com **portal transparente para o cliente**. Todo pedido do cliente vira trabalho rastreado — e o cliente vê o status, o prazo e quanto do contrato foi consumido.

Construído desde o início como **multi-tenant**, com a mesma engenharia do **Saldu**.

**Status:** planejamento concluído; código do v1 a iniciar — ver [Status](#status).

---

## Sumário

- [Status](#status)
- [O que o Acordu faz](#o-que-o-acordu-faz)
- [Stack](#stack)
- [Arquitetura em resumo](#arquitetura-em-resumo)
- [Estrutura do repositório](#estrutura-do-repositório)
- [Documentação](#documentação)
- [Como rodar localmente](#como-rodar-localmente)
- [Testes](#testes)
- [Deploy](#deploy)
- [Convenções](#convenções)
- [Roadmap](#roadmap)
- [Licença](#licença)

---

## Status

| Área | Estado |
|---|---|
| Planejamento (ERS, modelo, API, read side, ADRs, segurança) | ✅ concluído |
| Infraestrutura (compartilhada com o Saldu) | ⏳ passos próprios do Acordu pendentes |
| Código do v1 | ⏳ a iniciar |
| CI/CD e deploy | ⏳ planejado |

---

## O que o Acordu faz

- **Solicitações** com protocolo, status e **SLA em horas úteis** (calendário do prestador).
- **Tarefas, projetos e apontamento de horas**, ligados à solicitação e ao contrato.
- **Contratos** mensais, de banco de horas ou avulsos, com consumo calculado a partir das horas apontadas.
- **Tarefas recorrentes** geradas automaticamente (fechamento mensal, manutenção preventiva...).
- **Portal do cliente:** abrir e acompanhar solicitações, ver prazo e responsável, aprovar entregas e consultar o **extrato de horas do contrato** — só com o que o prestador marcou como visível.
- **Solução reutilizável:** cada solicitação resolvida registra a solução, e novas solicitações recebem sugestões de casos semelhantes já resolvidos.

---

## Stack

| Camada | Tecnologia |
|---|---|
| Backend | .NET 10 (Web API, minimal APIs) |
| ORM | EF Core + Npgsql |
| Banco | PostgreSQL 17 (`pg_trgm`, `unaccent`) |
| Frontend | React + Vite (TypeScript, SPA com app interno e portal) |
| Arquivos | Object storage S3-compatível (OCI) |
| E-mail | Resend |
| Infra | Docker Compose + Caddy |
| Hospedagem | Oracle Cloud Always Free (Ampere ARM, `us-phoenix-1`), VM compartilhada com o Saldu — ADR 0006 |
| CI/CD | GitHub Actions → GHCR → VM |

---

## Arquitetura em resumo

**DDD pragmático em quatro camadas**, dependências sempre para dentro:

```
Domain          zero dependências
   ▲
Application      → Domain
   ▲
Infrastructure   → Application, Domain
   ▲
Api / Worker     → Application, Infrastructure
```

**Dois isolamentos:** entre prestadores (`TenantId`, filtro global do EF Core) e entre os clientes de um mesmo prestador (`ClienteId`, segundo filtro global, ativo para o portal). O portal tem casos de uso e DTOs próprios, sem campos internos.

**Tempo é `Duracao` em minutos inteiros**; consumo de contrato é calculado a partir dos apontamentos, nunca guardado.

Detalhes e o porquê das decisões em [`docs/`](#documentação).

---

## Estrutura do repositório

```
acordu/
├─ .github/workflows/     # CI e deploy
├─ docs/                  # planejamento (ver índice abaixo)
├─ src/
│  ├─ Acordu.Domain/          # agregados, Duracao, máquinas de estado, CalculadoraSla
│  ├─ Acordu.Application/     # casos de uso internos + Portal/, read side
│  ├─ Acordu.Infrastructure/  # EF Core, filtros, SQL de consulta, storage, e-mail, agendador
│  ├─ Acordu.Api/             # endpoints, auth, middleware
│  └─ Acordu.Worker/          # (adiado) — jobs são endpoints internos da Api
├─ apps/web/              # SPA: features/ (interno) e portal/
├─ tests/                 # unit, integração, funcional e arquitetura
├─ docker/                # Dockerfiles + bloco de site do Caddy
├─ docker-compose.yml     # prod: acordu-api + acordu-web na rede infra_net
├─ docker-compose.dev.yml # Postgres 17 local
├─ Acordu.sln
└─ README.md
```

**Como se guiar:**
- Regra de negócio → `src/Acordu.Domain`.
- Caso de uso → `src/Acordu.Application` (portal em `Portal/`).
- Banco, storage, e-mail, jobs → `src/Acordu.Infrastructure`.
- Endpoint HTTP → `src/Acordu.Api`.
- Tela → `apps/web/src/features` ou `apps/web/src/portal`.
- Por que foi feito assim → `docs/adr`.

---

## Documentação

| Documento | Conteúdo |
|---|---|
| `docs/ers/ERS-acordu-v1.md` | Especificação de requisitos (ISO/IEC/IEEE 29148). |
| `docs/adr/0001-herdar-decisoes-do-saldu.md` | Decisões de engenharia herdadas do Saldu. |
| `docs/adr/0002-portal-escopo-por-cliente.md` | Portal: contato como usuário com escopo por cliente. |
| `docs/adr/0003-sla-horas-uteis.md` | SLA em horas úteis, calendário por tenant, pausa. |
| `docs/adr/0004-tempo-em-minutos-e-consumo-read-model.md` | `Duracao` em minutos; consumo como read model. |
| `docs/adr/0005-hospedagem-serverless-gratuita.md` | Serverless (Cloud Run + Neon + Cloudflare Pages) — **revogado**, plano B. |
| `docs/adr/0006-retorno-oracle-always-free.md` | VM Oracle compartilhada, plano de escala (Magalu Cloud SP) e regras de portabilidade. |
| `docs/estrategia-produto-acordu.md` | Visão, mercado, posicionamento, fases e monetização. |
| `docs/modelo-de-dominio-acordu.md` | Agregados, máquinas de estado, VOs, serviços, eventos. |
| `docs/contrato-api-acordu-v1.md` | Endpoints internos e do portal, formatos, erros. |
| `docs/read-side-acordu-v1.md` | Consumo, extrato, SLA, dashboard, semelhantes, índices. |
| `docs/seguranca-modelo-acesso-acordu-v1.md` | Identidade, RBAC, escopo do portal, links, anexos, auditoria. |
| `docs/deploy-ci-acordu-v1.md` | CI/CD e deploy na VM compartilhada. |
| `docs/setup-infra-oracle-acordu.md` | Runbook na Oracle Cloud (o que é próprio do Acordu). |
| `docs/estrutura-repositorio-acordu.md` | Estrutura do repositório e ordem de criação. |
| `docs/convencoes-codigo-acordu.md` | Convenções técnicas de código. |
| `docs/convencoes-commit-acordu.md` | Conventional Commits e escopos. |

Regra de higiene: **decisão de arquitetura vira ADR**; **convenção de código nova entra em `convencoes-codigo-acordu.md`**.

---

## Como rodar localmente

> Os passos se ativam conforme o código nasce.

**Pré-requisitos:** .NET 10 SDK, Node LTS, Docker.

```
# Postgres local (porta 5434; o Saldu usa 5433)
docker compose -f docker-compose.dev.yml up -d

# backend
dotnet build
dotnet run --project src/Acordu.Api

# frontend
cd apps/web && npm install && npm run dev
```

Configuração sensível em variáveis de ambiente / `.env` (nunca commitado).

---

## Testes

```
dotnet test                 # unit, integração (Testcontainers), funcional e arquitetura
cd apps/web && npm test     # Vitest
```

Obrigatórios: isolamento **entre tenants** e **entre clientes** do mesmo tenant, e nenhum dado interno nas respostas do portal.

---

## Deploy

Imagens **ARM64** no GitHub Actions → **GHCR** → `docker compose pull && up -d` na VM, com migrations como etapa explícita. Caddy e PostgreSQL 17 ficam numa stack de infraestrutura compartilhada com o Saldu (`/opt/infra`, rede `infra_net`); o Acordu é inquilino em `/opt/acordu`. Jobs agendados (recorrências, fechamento automático, alertas de SLA) são endpoints internos idempotentes (`/internal/jobs/*`, com token), disparados por timer na VM (ADR 0006). Ver `docs/adr/0006-retorno-oracle-always-free.md`, `docs/deploy-ci-acordu-v1.md` e `docs/setup-infra-oracle-acordu.md`.

---

## Convenções

- **Camadas:** o `Domain` nunca referencia infra/HTTP nem lê o relógio.
- **Tempo:** sempre `Duracao` em minutos (nunca `float`/`double`).
- **Portal:** DTOs próprios; item novo nasce interno.
- **Commits:** Conventional Commits (`docs/convencoes-commit-acordu.md`).
- **Branches:** `main` sempre verde; trabalho em branches + PR revisado.
- **Decisões:** registrar em `docs/adr`.

---

## Roadmap

**v1:** clientes e contatos, contratos com SLA, solicitações, tarefas, projetos, apontamentos, tarefas recorrentes, portal do cliente, solução e sugestão de semelhantes, notificações por e-mail, dashboard, multi-tenant com RBAC.

**Depois:** solicitações por e-mail e WhatsApp, base de conhecimento publicada no portal, projeção do consumo do contrato, relatório mensal em PDF, faturamento, IA assistiva (sempre com revisão humana), app mobile.

---

## Licença

Todos os direitos reservados — projeto proprietário da **Utile Systems** enquanto privado. Uma licença poderá ser definida caso o projeto seja aberto no futuro.

---

*Acordu é um produto da Utile Systems.*
