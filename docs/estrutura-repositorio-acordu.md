# Acordu — Estrutura do Repositório e Organização do Código

| | |
|---|---|
| **Versão** | 0.1 |
| **Última atualização** | 06/10/2026 |
| **Stack** | .NET 10 · EF Core + Npgsql · PostgreSQL 17 · React + Vite (TS) · Docker Compose + Caddy (VM Oracle compartilhada, ADR 0006) |
| **Base** | Mesma organização do Saldu (ADR 0001), com prefixo `Acordu.*`. |

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
├─ .github/
│  └─ workflows/
│     ├─ ci.yml                        # build + testes a cada push/PR
│     ├─ migrations.yml                # gera migrations sob demanda
│     └─ deploy.yml                    # build ARM64 → GHCR → migrations → deploy na VM por SSH
├─ docs/
│  ├─ ers/
│  │  └─ ERS-acordu-v1.md
│  ├─ adr/
│  │  ├─ 0001-herdar-decisoes-do-saldu.md
│  │  ├─ 0002-portal-escopo-por-cliente.md
│  │  ├─ 0003-sla-horas-uteis.md
│  │  ├─ 0004-tempo-em-minutos-e-consumo-read-model.md
│  │  ├─ 0005-hospedagem-serverless-gratuita.md   # revogado — plano B
│  │  ├─ 0006-retorno-oracle-always-free.md
│  │  └─ 0007-transporte-da-sessao.md
│  ├─ estrategia-produto-acordu.md
│  ├─ modelo-de-dominio-acordu.md
│  ├─ contrato-api-acordu-v1.md
│  ├─ read-side-acordu-v1.md
│  ├─ seguranca-modelo-acesso-acordu-v1.md
│  ├─ deploy-ci-acordu-v1.md
│  ├─ setup-infra-oracle-acordu.md
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
│  │  │  └─ Migrations/
│  │  ├─ MultiTenancy/                 # filtros globais por TenantId e ClienteId
│  │  ├─ Armazenamento/                # object storage S3-compatível (OCI) + URLs assinadas
│  │  ├─ Email/                        # Resend + outbox
│  │  ├─ Agendamento/                  # jobs idempotentes: recorrências, fechamento automático, alertas de SLA
│  │  ├─ Seguranca/                    # Identity, JWT, tokens de link
│  │  └─ DependencyInjection.cs
│  ├─ Acordu.Api/                      # COMPOSITION ROOT
│  │  ├─ Endpoints/                    # minimal API por feature
│  │  │  └─ Portal/                    # /api/v1/portal/*
│  │  ├─ Middleware/                   # contexto de tenant/cliente, Problem Details
│  │  ├─ Program.cs
│  │  └─ appsettings*.json
│  └─ Acordu.Worker/                   # (ADIADO) — jobs são endpoints internos da Api, disparados por timer na VM
├─ apps/
│  └─ web/                             # React + Vite + TypeScript (SPA)
│     └─ src/
│        ├─ app/                       # rotas, providers
│        ├─ features/                  # app interno: solicitacoes, tarefas, projetos, apontamentos,
│        │                             # contratos, clientes, recorrencias, dashboard, configuracoes
│        ├─ portal/                    # portal do cliente: layout, rotas e telas próprias
│        └─ shared/                    # api client, tipos gerados, formatadores, componentes
├─ tests/
│  ├─ Acordu.Domain.UnitTests/
│  ├─ Acordu.Application.UnitTests/
│  ├─ Acordu.Infrastructure.IntegrationTests/   # Testcontainers (Postgres real)
│  ├─ Acordu.Api.FunctionalTests/                # WebApplicationFactory
│  └─ Acordu.ArchitectureTests/                  # NetArchTest: camadas, relógio, filtros, DTOs do portal
├─ docker/
│  ├─ api.Dockerfile
│  ├─ web.Dockerfile                   # build do SPA + servidor estático interno
│  └─ Caddyfile.acordu                 # bloco de site do Acordu, incluído no Caddy compartilhado
├─ docker-compose.yml                  # prod: acordu-api + acordu-web na rede externa infra_net (Caddy e Postgres compartilhados)
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
- Tipos gerados do OpenAPI em `shared/api/types.ts`; o portal consome só os tipos das rotas `/portal`.
- Separar em dois apps só se o bundle do portal ficar pesado para o cliente final — medir antes.

---

## 7. Infra e deploy

- A VM, o Postgres e o Caddy de borda são **compartilhados com o Saldu** (ADR 0001). Este repositório só entrega as imagens do Acordu e o bloco de site do Caddy.
- `docker-compose.yml` sobe `acordu-api` e `acordu-web` em `/opt/acordu`, na rede externa `infra_net` da stack de infraestrutura (`/opt/infra`). ADR 0006.
- Jobs agendados ficam em `Infrastructure/Agendamento` e são expostos pela Api em `/internal/jobs/*`; o disparo é um timer na VM.
- Detalhes: `deploy-ci-acordu-v1.md` e `setup-infra-oracle-acordu.md`.

---

## 8. Documentação

- `docs/ers/`, `docs/adr/` e os demais em `docs/`, como no Saldu.
- **Estratégia de produto e cronograma** também ficam em `docs/`. Contêm estratégia de negócio: revisar (ou remover do histórico) antes de qualquer abertura pública do repositório.

> **Regra de manutenção:** decisão de arquitetura → ADR; convenção de código → `convencoes-codigo-acordu.md`, no mesmo turno em que é decidida.

---

## 9. Ordem de criação

1. **Esqueleto:** arquivos de raiz, `Acordu.sln`, projetos vazios compilando, `ArchitectureTests` já com as regras, `ci.yml`.
2. **Domain:** `Duracao`, `Periodo`, `CalendarioUtil` + `CalculadoraSla` (maior risco, primeiro), `Solicitacao` e máquina de estados, `Contrato`, `Tarefa`, `Apontamento`, `RecorrenciaTarefa` + `GeradorDeDatas`. Cada um com testes.
3. **Infrastructure base:** `AcorduDbContext`, migrations, filtros por tenant **e por cliente**, testes de isolamento (Testcontainers).
4. **Api + auth:** Identity/JWT (herdado), contexto de tenant/cliente, endpoints de clientes, contratos e solicitações, testes funcionais.
5. **Esqueleto andante no ar:** criar solicitação interna → tarefa → apontamento → ver consumo; deploy na VM.
6. **Portal:** convite de contato, rotas `/portal`, extrato, aprovação, link sem senha.
7. **Restante do v1:** recorrências + agendador, notificações por e-mail (outbox), dashboard, semelhantes.
8. **Hardening:** `deploy.yml` completo, backup, alertas no Discord.

Regra de ouro (igual ao Saldu): cada camada nasce com seus testes, de dentro para fora; os dois isolamentos são testados assim que a Infrastructure existe.
