# Acordu — Estrutura do Repositório e Organização do Código

| | |
|---|---|
| **Versão** | 0.1 |
| **Última atualização** | 21/11/2026 |
| **Stack** | .NET 10 · EF Core + Npgsql · PostgreSQL 17 · React + Vite (TS) · Cloud Run + Neon + Cloudflare (ADR 0010) |
| **Base** | ADR 0001 (decisões de engenharia base); projetos com prefixo `Acordu.*`. |

---

## 1. Decisões de topo

- **Monorepo** (backend, web, infra, docs).
- **Central Package Management** (`Directory.Packages.props`) e **SDK fixado** (`global.json`).
- **DDD pragmático**, um bounded context ("Atendimento e Execução de Serviços"), 4 camadas.
- **Domínio por tipo**, namespace espelha a pasta. Convenções em `convencoes-codigo-acordu.md`.
- **(Acordu)** Portal do cliente com **namespace e pasta próprios** em cada camada que o toca (ADR 0002).
- **(Acordu)** Projeto de **testes de arquitetura** para as regras que não podem depender de revisão humana.

---

## 2. Árvore do repositório

```
acordu/
├─ CLAUDE.md                          # regras do projeto para o Claude Code (ADR 0013)
├─ .claude/
│  ├─ commands/                        # /roadmap, /marco (orquestrador), /spec, /design, /plano, /tarefas, /pr, /deploy, /triagem
│  ├─ agents/                          # analista, arquiteto, desenvolvedor, testador, revisor-spec
│  └─ settings.json                    # negações da fábrica (processo.md §8)
├─ .githooks/
│  └─ pre-push                         # opcional: ci-local.sh --rapido (git config core.hooksPath .githooks)
├─ .github/
│  └─ workflows/
│     ├─ ci.yml                        # build + testes a cada push/PR
│     ├─ migrations.yml                # gera migrations sob demanda
│     ├─ deploy.yml                    # build amd64 → Artifact Registry → migrations → Cloud Run; Worker e SPA por wrangler
│     └─ backup.yml                    # pg_dump diário → R2
├─ docs/
│  ├─ fluxo-acordu.bpmn                # fluxo de negócio em BPMN, gerado por scripts/gerar-fluxo-bpmn.py
│  ├─ fluxo-acordu.svg                 # a mesma figura para o README, gerada junto
│  ├─ ers/
│  │  └─ ERS-acordu-v1.md
│  ├─ adr/
│  │  ├─ 0001-decisoes-de-engenharia-base.md
│  │  ├─ 0002-portal-escopo-por-cliente.md
│  │  ├─ 0003-sla-horas-uteis.md
│  │  ├─ 0004-tempo-em-minutos-e-consumo-read-model.md
│  │  ├─ 0005-hospedagem-serverless-gratuita.md   # histórico, retomado pelo 0010
│  │  ├─ 0006-retorno-oracle-always-free.md       # revogado
│  │  ├─ 0007-transporte-da-sessao.md
│  │  ├─ 0008-trilha-de-auditoria.md
│  │  ├─ 0009-tempo-real-por-sse.md
│  │  ├─ 0010-hospedagem-serverless-produto-unico.md
│  │  ├─ 0011-desenvolvimento-guiado-por-spec.md   # substituído pelo 0013
│  │  ├─ 0012-avisos-de-operacao-pelo-discord.md
│  │  └─ 0013-fabrica-agentica-semi-autonoma.md
│  ├─ specs/                       # spec.md + design.md por marco (processo.md) + _template/
│  ├─ processo.md                  # como o Acordu é construído (ADR 0013)
│  ├─ estado.md                    # em revisão, marco atual, próximos passos
│  ├─ convencoes-pr-acordu.md
│  ├─ estrategia-produto-acordu.md
│  ├─ modelo-de-dominio-acordu.md
│  ├─ contrato-api-acordu-v1.md
│  ├─ read-side-acordu-v1.md
│  ├─ seguranca-modelo-acesso-acordu-v1.md
│  ├─ deploy-ci-acordu-v1.md
│  ├─ setup-infra-acordu.md
│  ├─ estrutura-repositorio-acordu.md
│  ├─ convencoes-codigo-acordu.md
│  └─ convencoes-commit-acordu.md
├─ src/
│  ├─ Acordu.Domain/                   # NÚCLEO — zero dependências externas
│  │  ├─ Common/                       # EntidadeBase (Id, TenantId, eventos), RaizComCliente (ClienteId), IEventoDominio
│  │  ├─ Entities/                     # Cliente, Contato, Contrato, AjusteSaldo, Solicitacao, Interacao,
│  │  │                                # Tarefa, Projeto, Apontamento, Cronometro, RecorrenciaTarefa,
│  │  │                                # AcessoPortal, CalendarioUtil, Categoria
│  │  ├─ Enums/                        # StatusSolicitacao, StatusExterno, Prioridade, TipoContrato...
│  │  ├─ ValueObjects/                 # Duracao, DuracaoAssinada, Periodo, PoliticaSla, RelogioSla,
│  │  │                                # Solucao, ModeloTarefa, RegraRecorrencia, JanelaDiaria
│  │  ├─ Services/                     # CalculadoraSla, GeradorDeDatas
│  │  ├─ Events/                       # SolicitacaoAberta, InteracaoRegistrada, ApontamentoRegistrado...
│  │  └─ Exceptions/                   # DominioException e derivadas
│  ├─ Acordu.Application/              # CASOS DE USO — depende só do Domain
│  │  ├─ Abstractions/                 # ITenantContext, IUnitOfWork, repositórios, IArmazenamentoArquivos,
│  │  │                                # IEnviadorEmail, IGeradorProtocolo
│  │  ├─ Clientes/
│  │  ├─ Contratos/
│  │  ├─ Solicitacoes/                 # comandos/queries + handlers (CQRS-lite)
│  │  ├─ Tarefas/
│  │  ├─ Projetos/
│  │  ├─ Apontamentos/
│  │  ├─ Recorrencias/
│  │  ├─ Dashboard/
│  │  ├─ Notificacoes/
│  │  ├─ Configuracoes/
│  │  ├─ Empresas/                     # situação da empresa: prazos e vencimento do teste (F14)
│  │  ├─ Nebrys/                       # área da Nebrys: sessão de operação, consultas e DTOs próprios, sem tipo de negócio (ADR 0014)
│  │  ├─ Portal/                       # casos de uso e DTOs EXCLUSIVOS do portal (ADR 0002)
│  │  │  ├─ Solicitacoes/
│  │  │  └─ Extrato/
│  │  └─ Common/                       # validação, paginação, erros, comportamento transversal
│  ├─ Acordu.Infrastructure/           # IMPLEMENTAÇÕES — depende de Application + Domain
│  │  ├─ Persistence/
│  │  │  ├─ AcorduDbContext.cs
│  │  │  ├─ Configurations/            # IEntityTypeConfiguration por entidade
│  │  │  ├─ Repositories/
│  │  │  ├─ Consultas/                 # SQL do read side (helper com TenantId/ClienteId obrigatórios)
│  │  │  ├─ Nebrys/                    # consultas da área da Nebrys (ponto revisado de IgnoreQueryFilters, ADR 0014)
│  │  │  └─ Migrations/
│  │  ├─ MultiTenancy/                 # filtros globais por TenantId e ClienteId
│  │  ├─ Armazenamento/                # object storage S3-compatível (R2) + URLs assinadas
│  │  ├─ Email/                        # Resend + outbox
│  │  ├─ Agendamento/                  # jobs idempotentes: recorrências, fechamento automático, alertas de SLA
│  │  ├─ Operacao/                     # avisos ao Discord, ocorrências de erro (fora dos tenants, ADR 0012)
│  │  ├─ Seguranca/                    # Identity, JWT, tokens de link
│  │  └─ DependencyInjection.cs
│  ├─ Acordu.Api/                      # COMPOSITION ROOT
│  │  ├─ Endpoints/                    # minimal API por feature
│  │  │  └─ Portal/                    # /api/v1/portal/*
│  │  ├─ Middleware/                   # contexto de tenant/cliente, Problem Details
│  │  ├─ Program.cs
│  │  └─ appsettings*.json
│  └─ Acordu.Worker/                   # (ADIADO) — jobs são endpoints internos da Api, disparados pelo Cloud Scheduler
├─ apps/
│  └─ web/                             # React + Vite + TypeScript (SPA)
│     └─ src/
│        ├─ app/                       # rotas, providers
│        ├─ features/                  # app interno: solicitacoes, tarefas, projetos, apontamentos,
│        │                             # contratos, clientes, recorrencias, dashboard, configuracoes;
│        │                             # legal: termos e privacidade (textos/*.md versionados, F14f)
│        ├─ portal/                    # portal do cliente: layout, rotas e telas próprias
│        └─ shared/                    # api client, tipos gerados, formatadores, componentes
├─ tests/
│  ├─ Acordu.Domain.UnitTests/
│  ├─ Acordu.Application.UnitTests/
│  ├─ Acordu.Infrastructure.IntegrationTests/   # Testcontainers (Postgres real)
│  ├─ Acordu.Api.FunctionalTests/                # WebApplicationFactory
│  └─ Acordu.ArchitectureTests/                  # NetArchTest: camadas, relógio, filtros, DTOs do portal
├─ docker/
│  └─ api.Dockerfile                   # linux/amd64, aspnet chiseled extra (ICU + tzdata), ReadyToRun
├─ infra/
│  └─ cloudflare/
│     ├─ api-borda/                    # Worker de api.acordu.app.br: proxy para o Cloud Run + token de origem
│     └─ vigia/                        # Worker da vigia: health a cada 5 min, queda e volta em #alertas (ADR 0012)
├─ scripts/
│  ├─ conferir-specs.mjs               # estrutura das specs na CI (processo.md), com conferir-specs.test.mjs
│  ├─ conferir-fabrica.test.mjs        # regras do .claude/settings.json e modelos dos agentes
│  ├─ gerar-fluxo-bpmn.py              # gera docs/fluxo-acordu.bpmn e .svg; --conferir na CI (processo.md §1)
│  ├─ rotulos.sh                       # rótulos do backlog (processo.md §7), com rotulos.test.sh
│  └─ ci-local.sh                      # o que a CI rodaria na branch (deploy-ci §3.1), com ci-local.test.sh
├─ docker-compose.dev.yml              # dev local: Postgres 17 na porta 5434 (object storage local entra no marco de anexos)
├─ Acordu.sln
├─ Directory.Build.props
├─ Directory.Packages.props
├─ global.json
├─ .editorconfig
├─ .gitignore
├─ .dockerignore
└─ README.md
```

---

## 3. Camadas e regra de dependência

```
Domain          zero dependências
   ▲
Application      → Domain
   ▲
Infrastructure   → Application, Domain
   ▲
Api / Worker     → Application, Infrastructure
```

| Camada | Responsabilidade | Conhece |
|---|---|---|
| **Domain** | Agregados, `Duracao`, máquinas de estado, `CalculadoraSla`, eventos. | Nada. |
| **Application** | Casos de uso internos e do portal, autorização por permissão, read side. | Domain. |
| **Infrastructure** | EF Core, filtros globais, SQL de consulta, object storage, e-mail, agendador, Identity. | Application, Domain. |
| **Api** | Endpoints, autenticação, middleware de contexto, DI. | Application, Infrastructure. |

---

## 4. Onde vivem os dois isolamentos (ADR 0002)

1. **Api:** middleware lê do token `TenantId`, papel, `ClienteId`, `EscopoPortal` e, em sessão de link, `SolicitacaoId`.
2. **Application:** tudo vira `ITenantContext`. Casos de uso do portal aplicam `EscopoPortal` e o escopo da sessão de link.
3. **Infrastructure:** `AcorduDbContext` aplica filtro global por `TenantId` sempre e por `ClienteId` quando o contexto é de Cliente. O helper de SQL exige os mesmos parâmetros.

Testes obrigatórios nos níveis de integração **e** API: cross-tenant (RNF-SEG-01) e cross-cliente (RNF-SEG-02), além de "nenhum item interno no portal" (RNF-SEG-03).

---

## 5. Projetos de teste

| Projeto | Foco | Ferramentas |
|---|---|---|
| `Domain.UnitTests` | `Duracao`, máquinas de estado, `CalculadoraSla`, `GeradorDeDatas`, invariantes. | xUnit + AwesomeAssertions |
| `Application.UnitTests` | Casos de uso com fakes; regras entre agregados (primeira resposta, retomada, limiares). | xUnit + AwesomeAssertions + `FakeTimeProvider` |
| `Infrastructure.IntegrationTests` | Migrations, filtros globais, SQL de consumo/semelhantes, protocolo sequencial, idempotência do agendador. | Testcontainers (Postgres) |
| `Api.FunctionalTests` | Endpoints, autorização, isolamento ponta a ponta, portal sem campos internos. | WebApplicationFactory |
| `ArchitectureTests` | Regras de camadas e proibições (ver convenções §5). | NetArchTest |

---

## 6. Frontend

- `apps/web`: uma SPA com **duas áreas**: app interno (`features/`) e portal (`portal/`, rota `/portal`, layout mobile-first).
- **(F14m)** `src/nebrys`: área da Nebrys (rota `/nebrys`, rotas em `src/routes/nebrys`, ADR 0014), carregada sob demanda, com cliente próprio (`nebrys/api.ts`) e a sessão de operação em `shared/sessao/sessaoOperacao.ts`. Não importa `features/` nem é importada por elas (convenções §4).
- Tipos gerados do OpenAPI em `shared/api/interno.d.ts` e `shared/api/portal.d.ts`, a partir de `openapi/*.json` na raiz (contrato versionado, convenções §4); o portal consome só os tipos das rotas `/portal`. A área da Nebrys tem o seu `openapi/nebrys.json` (F14k), com os tipos em `shared/api/nebrys.d.ts` (F14m), usados só por `src/nebrys`.
- **(F1)** pnpm, `pnpm dev` (proxy de `/api` para a API em `http://localhost:5080`), `pnpm test`, `pnpm build`. Rotas por arquivo em `src/routes`.
- **(F14f)** `features/legal`: os textos legais em Markdown (`textos/termos.md`, `textos/privacidade.md`) com a versão na constante `versoes.ts` (convenções §4), e as páginas públicas `/termos` e `/privacidade` (rotas `src/routes/termos.tsx` e `privacidade.tsx`, fora das guardas de sessão).
- Separar em dois apps só se o bundle do portal ficar pesado para o cliente final — medir antes.

---

## 7. Infra e deploy

- Nada é compartilhado com outros produtos (ADR 0010). A API vai como imagem para o Cloud Run; o SPA, como estático para o Cloudflare Pages; a borda da API, como Worker.
- O SPA chama a API em `VITE_URL_API` (`https://api.acordu.app.br` em produção; vazio em dev, com o proxy do Vite).
- Jobs agendados ficam em `Infrastructure/Agendamento` e são expostos pela Api em `/internal/jobs/*`; o disparo é o Cloud Scheduler.
- Detalhes: `deploy-ci-acordu-v1.md` e `setup-infra-acordu.md`.

---

## 8. Documentação

- `docs/ers/`, `docs/adr/` e os demais em `docs/`.
- **Estratégia de produto e cronograma** também ficam em `docs/`. Contêm estratégia de negócio: revisar (ou remover do histórico) antes de qualquer abertura pública do repositório.

> **Regra de manutenção:** decisão de arquitetura → ADR; convenção de código → `convencoes-codigo-acordu.md`, no mesmo turno em que é decidida.

---

## 9. Ordem de criação

1. **Esqueleto:** arquivos de raiz, `Acordu.sln`, projetos vazios compilando, `ArchitectureTests` já com as regras, `ci.yml`.
2. **Domain:** `Duracao`, `Periodo`, `CalendarioUtil` + `CalculadoraSla` (maior risco, primeiro), `Solicitacao` e máquina de estados, `Contrato`, `Tarefa`, `Apontamento`, `RecorrenciaTarefa` + `GeradorDeDatas`. Cada um com testes.
3. **Infrastructure base:** `AcorduDbContext`, migrations, filtros por tenant **e por cliente**, testes de isolamento (Testcontainers).
4. **Api + auth:** Identity/JWT (ADR 0001), contexto de tenant/cliente, endpoints de clientes, contratos e solicitações, testes funcionais.
5. **Esqueleto andante no ar:** criar solicitação interna → tarefa → apontamento → ver consumo; deploy no Cloud Run.
6. **Portal:** convite de contato, rotas `/portal`, extrato, aprovação, link sem senha.
7. **Restante do v1:** recorrências + agendador, notificações por e-mail (outbox), dashboard, semelhantes.
8. **Hardening:** `deploy.yml` completo, backup, alertas no Discord.

Regra de ouro: cada camada nasce com seus testes, de dentro para fora; os dois isolamentos são testados assim que a Infrastructure existe.
