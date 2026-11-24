# Acordu

> Um produto **Nebrys**.

Gestão de **atendimento, tarefas e contratos** para prestadores de serviço, com **portal transparente para o cliente**. Todo pedido do cliente vira trabalho rastreado — e o cliente vê o status, o prazo e quanto do contrato foi consumido.

Construído desde o início como **multi-tenant**.

**Status:** v1 em produção em [acordu.app.br](https://acordu.app.br) desde 12/11/2026, com o lançamento controlado (F14) em andamento — ver [Status](#status).

---

## Sumário

- [Status](#status)
- [O que o Acordu faz](#o-que-o-acordu-faz)
- [Como funciona](#como-funciona)
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
| Backend do v1 | ✅ completo |
| Frontend (app interno e portal) | ✅ F1–F7 e marcos F8–F13 |
| Infraestrutura (Cloud Run + Neon + Cloudflare) | ✅ provisionada (ADR 0010) |
| CI/CD e deploy | ✅ no ar desde 12/11/2026 |
| Lançamento controlado (situação da empresa, cadastro por convite, área da Nebrys) | ⏳ F14 em andamento |

O que está em revisão e os próximos passos ficam em [`docs/estado.md`](docs/estado.md), atualizado a cada PR.

---

## O que o Acordu faz

- **Solicitações** com protocolo, status e **SLA em horas úteis** (calendário do prestador).
- **Tarefas, projetos e apontamento de horas**, ligados à solicitação e ao contrato.
- **Contratos** mensais, de banco de horas ou avulsos, com consumo calculado a partir das horas apontadas.
- **Tarefas recorrentes** geradas automaticamente (fechamento mensal, manutenção preventiva...).
- **Portal do cliente:** abrir e acompanhar solicitações, ver prazo e responsável, aprovar entregas e consultar o **extrato de horas do contrato** — só com o que o prestador marcou como visível.
- **Solução reutilizável:** cada solicitação resolvida registra a solução, e novas solicitações recebem sugestões de casos semelhantes já resolvidos.

---

## Como funciona

Da entrada da empresa ao fechamento da solicitação, com quem faz cada passo (clique para ampliar):

[![Fluxo de negócio do Acordu em BPMN](docs/fluxo-acordu.svg)](docs/fluxo-acordu.svg)

1. **Entrada:** a Nebrys convida a empresa; o Dono se cadastra e a empresa nasce em teste. No fim do teste ela fica só leitura até a Nebrys ativar, prorrogar ou cancelar.
2. **Configuração:** horário útil (base do SLA), equipe, clientes, contratos e o convite do contato para o portal.
3. **Atendimento:** a solicitação entra pelo portal ou pela equipe, ganha protocolo e prazos de SLA, vira tarefas e horas apontadas no contrato.
4. **Entrega:** o contato aprova (fecha) ou reprova (reabre); sem resposta, fecha sozinha depois de N dias.

O diagrama é BPMN 2.0 ([`docs/fluxo-acordu.bpmn`](docs/fluxo-acordu.bpmn), abre no [bpmn.io](https://demo.bpmn.io) e no Camunda Modeler). O `.bpmn` e o `.svg` são gerados por `scripts/gerar-fluxo-bpmn.py`, e a CI confere que estão em sincronia.

---

## Stack

| Camada | Tecnologia |
|---|---|
| Backend | .NET 10 (Web API, minimal APIs) |
| ORM | EF Core + Npgsql |
| Banco | PostgreSQL 17 (`pg_trgm`, `unaccent`) |
| Frontend | React + Vite (TypeScript, SPA com app interno e portal) |
| Arquivos | Object storage S3-compatível (Cloudflare R2) |
| E-mail | Resend |
| Hospedagem | API no Google Cloud Run (`us-east4`), Postgres no Neon (`aws-us-east-1`), SPA no Cloudflare Pages, borda da API num Cloudflare Worker — ADR 0010 |
| CI/CD | GitHub Actions → Artifact Registry → Cloud Run (WIF); SPA e Worker por `wrangler` |

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
├─ docker/                # Dockerfile da API (linux/amd64)
├─ infra/cloudflare/      # Worker da borda da API (api.acordu.app.br)
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
| `docs/adr/0001-decisoes-de-engenharia-base.md` | Banco, ORM, multi-tenant, OpenAPI, hospedagem, identidade e multiusuário. |
| `docs/adr/0002-portal-escopo-por-cliente.md` | Portal: contato como usuário com escopo por cliente. |
| `docs/adr/0003-sla-horas-uteis.md` | SLA em horas úteis, calendário por tenant, pausa. |
| `docs/adr/0004-tempo-em-minutos-e-consumo-read-model.md` | `Duracao` em minutos; consumo como read model. |
| `docs/adr/0005-hospedagem-serverless-gratuita.md` | Serverless (primeira versão) — histórico, retomado pelo ADR 0010. |
| `docs/adr/0006-retorno-oracle-always-free.md` | VM Oracle compartilhada — **revogado** em 26/10/2026. |
| `docs/adr/0007-transporte-da-sessao.md` | Access token em memória (`Bearer`); refresh só em cookie HttpOnly, com rotação. |
| `docs/adr/0008-trilha-de-auditoria.md` | Trilha de auditoria na unidade de trabalho, retenção de 12 meses. |
| `docs/adr/0009-tempo-real-por-sse.md` | Tempo real por SSE com sinais de mudança; polling no nível 0. |
| `docs/adr/0010-hospedagem-serverless-produto-unico.md` | Hospedagem atual: Cloud Run + Neon + Cloudflare (Pages, Worker, R2), jobs no Scheduler, plano de escala. |
| `docs/adr/0011-desenvolvimento-guiado-por-spec.md` | Spec versionada por marco — **substituído** pelo ADR 0013 em 11/11/2026. |
| `docs/adr/0012-avisos-de-operacao-pelo-discord.md` | Avisos de operação (erros, jobs, queda, deploy, sugestões) pelo Discord. |
| `docs/adr/0013-fabrica-agentica-semi-autonoma.md` | Fábrica agêntica: orquestrador, subagentes por papel, portões humanos. |
| `docs/adr/0014-area-da-nebrys-sessao-de-operacao.md` | Área da Nebrys: sessão de operação sem tenant. |
| `docs/adr/0015-empresa-somente-leitura-pela-guarda-de-gravacao.md` | Empresa só leitura pela guarda de gravação. |
| `docs/processo.md` | Como o Acordu é construído: etapas, papéis, portões, onde fica cada coisa. |
| `docs/estado.md` | O que está em revisão, marco atual e próximos passos. |
| `docs/fluxo-acordu.bpmn` | Fluxo de negócio em BPMN (e `.svg` para leitura), gerado por `scripts/gerar-fluxo-bpmn.py`. |
| `docs/specs/` | Spec e design dos marcos (a partir do F8) e templates. |
| `docs/estrategia-produto-acordu.md` | Visão, mercado, posicionamento, fases e monetização. |
| `docs/modelo-de-dominio-acordu.md` | Agregados, máquinas de estado, VOs, serviços, eventos. |
| `docs/contrato-api-acordu-v1.md` | Endpoints internos e do portal, formatos, erros. |
| `docs/read-side-acordu-v1.md` | Consumo, extrato, SLA, dashboard, semelhantes, índices. |
| `docs/seguranca-modelo-acesso-acordu-v1.md` | Identidade, RBAC, escopo do portal, links, anexos, auditoria. |
| `docs/deploy-ci-acordu-v1.md` | CI/CD, deploy, jobs e backup. |
| `docs/setup-infra-acordu.md` | Runbook de provisionamento (Google Cloud, Neon, Cloudflare, Resend). |
| `docs/estrutura-repositorio-acordu.md` | Estrutura do repositório e ordem de criação. |
| `docs/convencoes-codigo-acordu.md` | Convenções técnicas de código. |
| `docs/convencoes-commit-acordu.md` | Conventional Commits e escopos. |

Regra de higiene: **decisão de arquitetura vira ADR**; **convenção de código nova entra em `convencoes-codigo-acordu.md`**.

---

## Como rodar localmente

**Pré-requisitos:** .NET 10 SDK, Node 22 com pnpm (`corepack enable`), Docker.

```
# Postgres (porta 5434), emulador S3 e Mailpit (e-mails em http://localhost:8025)
docker compose -f docker-compose.dev.yml up -d

# schema do banco (na primeira vez e a cada migration nova que chegar na main)
dotnet tool restore
dotnet ef database update --project src/Acordu.Infrastructure --startup-project src/Acordu.Api

# backend
dotnet build
dotnet run --project src/Acordu.Api

# frontend (http://localhost:5173, com /api encaminhado para a API em http://localhost:5080)
cd apps/web && pnpm install && pnpm dev
```

Abra o SPA por `http://localhost:5173` (não por IP nem outro nome): o cookie de refresh é `Secure` (ADR 0007), e Chrome, Edge e Firefox só o aceitam sem HTTPS quando o endereço é `localhost`.

Os e-mails que a API manda no desenvolvimento (confirmação, senha, convites, avisos) chegam ao **Mailpit**: abra `http://localhost:8025` e clique nos links. Com `Email__ResendChave` definida, o envio vai pelo Resend.

Os anexos vão para o **emulador S3** do compose (porta 9090), já configurado no `appsettings.Development`. Ele não guarda nada entre reinícios: anexos antigos param de baixar quando o contêiner é recriado.

Configuração sensível em variáveis de ambiente / `.env` (nunca commitado).

---

## Testes

```
dotnet test                 # unit, integração (Testcontainers), funcional e arquitetura
cd apps/web && pnpm test    # Vitest
cd apps/web && pnpm e2e     # Playwright (fumaça): precisa do Postgres e da API rodando, como acima
```

O contrato OpenAPI fica versionado em `openapi/`. Mudou a API: `ACORDU_ATUALIZAR_OPENAPI=1 dotnet test --filter ContratoOpenApiTests` e `cd apps/web && pnpm api:tipos` (ou o workflow **OpenAPI**).

Obrigatórios: isolamento **entre tenants** e **entre clientes** do mesmo tenant, e nenhum dado interno nas respostas do portal.

---

## Deploy

Imagem **amd64** no GitHub Actions → **Artifact Registry** → nova revisão no **Cloud Run**, autenticado por Workload Identity Federation, com migrations no endpoint direto do Neon antes da revisão. SPA no Cloudflare Pages e borda da API num Worker (`api.acordu.app.br`). Jobs agendados são endpoints internos idempotentes (`/internal/jobs/*`, com token), disparados pelo Cloud Scheduler: `tick` a cada 15 min no horário comercial e `diario` uma vez por dia. Backup diário com `pg_dump` pelo Actions para o R2. Ver `docs/adr/0010-hospedagem-serverless-produto-unico.md`, `docs/deploy-ci-acordu-v1.md` e `docs/setup-infra-acordu.md`.

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

Todos os direitos reservados — projeto proprietário da **Nebrys** enquanto privado. Uma licença poderá ser definida caso o projeto seja aberto no futuro.

---

*Acordu é um produto da Nebrys.*
