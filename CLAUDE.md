# Acordu — instruções para o Claude

Acordu (Nebrys): gestão de atendimento, tarefas e contratos para pequenos prestadores de serviço, com portal transparente para o cliente. Produto único da Nebrys; nada é compartilhado com outro produto.

## Regra inegociável

O Acordu é escrito do zero. **Nada do ASMTasks** (sistema da empregadora do Daniel) é reaproveitado: código, schema, telas, textos ou dados. Nunca peça, leia ou cite o ASMTasks como referência; na dúvida sobre a origem de algo, pare e pergunte.

## Como trabalhar

O processo inteiro (etapas, papéis, portões, onde fica cada coisa, formato de spec e design) está em **`docs/processo.md`** (ADR 0013). Leia antes de começar qualquer marco. O essencial:

- O Claude implementa; o Daniel decide nos portões (processo §4).
- Comandos: `/roadmap` (escolher o marco), `/marco` (orquestrador: conduz o marco com os subagentes), `/deploy`, `/triagem` (produção e sugestões → issue), e para uma etapa à mão `/spec`, `/design`, `/plano`, `/tarefas`, `/pr`; formato de commit e PR em `docs/convencoes-commit-acordu.md` e `docs/convencoes-pr-acordu.md`.
- Comece pelo `docs/estado.md`: o que está em revisão e os próximos passos.

## Onde está cada decisão

| Assunto | Documento |
|---|---|
| Requisitos | `docs/ers/ERS-acordu-v1.md` |
| Domínio | `docs/modelo-de-dominio-acordu.md` |
| API | `docs/contrato-api-acordu-v1.md` + `openapi/*.json` |
| Consultas | `docs/read-side-acordu-v1.md` |
| Segurança | `docs/seguranca-modelo-acesso-acordu-v1.md` |
| Arquitetura | `docs/adr/` (0001–0016) |
| Código e estrutura | `docs/convencoes-codigo-acordu.md`, `docs/estrutura-repositorio-acordu.md` |
| Deploy e infra | `docs/deploy-ci-acordu-v1.md`, `docs/setup-infra-acordu.md` (ADR 0010) |
| Specs dos marcos | `docs/specs/` |
| Fluxo do sistema (BPMN) | `docs/fluxo-acordu.bpmn` e `.svg` (editar `scripts/gerar-fluxo-bpmn.py` e rodar) |
| Processo (etapas, portões, onde fica cada coisa) | `docs/processo.md` (ADR 0013) |
| Estado do projeto | `docs/estado.md` |
| Commits e PRs | `docs/convencoes-commit-acordu.md`, `docs/convencoes-pr-acordu.md` |

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
scripts/ci-local.sh [--rapido|--tudo]                # o que a CI rodaria na branch (pre-push: git config core.hooksPath .githooks)
ACORDU_ATUALIZAR_OPENAPI=1 dotnet test --filter ContratoOpenApiTests && cd apps/web && pnpm api:tipos   # mudou a API
```

## Estilo

Respostas e textos em português, concisos, com pouca formatação.
