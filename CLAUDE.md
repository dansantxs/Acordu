# Acordu — instruções para o Claude

Acordu (Nebrys): gestão de atendimento, tarefas e contratos para pequenos prestadores de serviço, com portal transparente para o cliente. Produto único da Nebrys; nada é compartilhado com outro produto.

## Regra inegociável

O Acordu é escrito do zero. **Nada do ASMTasks** (sistema da empregadora do Daniel) é reaproveitado: código, schema, telas, textos ou dados. Nunca peça, leia ou cite o ASMTasks como referência; na dúvida sobre a origem de algo, pare e pergunte.

## Papéis e fluxo

- O Claude implementa; o Daniel revisa e decide.
- **Desenvolvimento guiado por spec (ADR 0011):** todo marco começa por `docs/specs/<marco>/spec.md`. O PR da spec é a aprovação do plano; sem spec aprovada, não há código do marco. Comandos: `/spec`, `/plano`, `/tarefas`.
- Decisão que depende do Daniel: mostre opções e recomendação e espere. Tarefa simples: execute direto.
- **PRs pequenos contra a `main`, sem empilhar**, sempre com testes e CI verde. Descrição explica as decisões não óbvias e cita os cenários da spec que entrega.
- Antes de abrir um PR: rode os testes da área e o subagente `revisor-spec`.
- Decisão de arquitetura nova → ADR em `docs/adr/`. Convenção nova → `docs/convencoes-codigo-acordu.md`. No mesmo PR.
- Commits em Conventional Commits (`docs/convencoes-commit-acordu.md`), agrupados por camada.

## Onde está cada decisão

| Assunto | Documento |
|---|---|
| Requisitos | `docs/ers/ERS-acordu-v1.md` |
| Domínio | `docs/modelo-de-dominio-acordu.md` |
| API | `docs/contrato-api-acordu-v1.md` + `openapi/*.json` |
| Consultas | `docs/read-side-acordu-v1.md` |
| Segurança | `docs/seguranca-modelo-acesso-acordu-v1.md` |
| Arquitetura | `docs/adr/` (0001–0011) |
| Código e estrutura | `docs/convencoes-codigo-acordu.md`, `docs/estrutura-repositorio-acordu.md` |
| Deploy e infra | `docs/deploy-ci-acordu-v1.md`, `docs/setup-infra-acordu.md` (ADR 0010) |
| Specs dos marcos | `docs/specs/` |

Se uma tarefa exigir mudar um desses documentos, diga qual e por quê, e mude no mesmo PR.

## Engenharia (resumo; detalhes nos ADRs)

- .NET 10, EF Core + Npgsql, PostgreSQL 17, React + Vite (TS). DDD pragmático em quatro camadas (Domain, Application, Infrastructure, Api); CQRS-lite sem MediatR.
- Multi-tenant por `TenantId` e isolamento por `ClienteId` no portal, ambos por global query filter (ADR 0002). Testes de isolamento obrigatórios.
- Tempo sempre em minutos inteiros (`Duracao`); SLA em horas úteis; o Domain não lê o relógio (testes de arquitetura).
- Hospedagem: Cloud Run + Neon + Cloudflare, sem SDK de provedor, configuração só por variável de ambiente (ADR 0010).
- Testes com xUnit + AwesomeAssertions (nunca FluentAssertions 8+).

## Comandos locais

```
docker compose -f docker-compose.dev.yml up -d      # Postgres 5434, S3 9090, Mailpit 8025
dotnet ef database update --project src/Acordu.Infrastructure --startup-project src/Acordu.Api
dotnet test                                          # unit, integração, funcional, arquitetura
cd apps/web && pnpm test && pnpm lint && pnpm build
ACORDU_ATUALIZAR_OPENAPI=1 dotnet test --filter ContratoOpenApiTests && cd apps/web && pnpm api:tipos   # mudou a API
```

## Estilo

Respostas e textos em português, concisos, com pouca formatação.
