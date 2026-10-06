# ADR 0001 — Herdar as decisões de engenharia do Saldu

- **Status:** Aceito
- **Data:** 30/09/2026
- **Contexto do projeto:** Acordu (gestão de atendimento, execução e contratos, v1)

## Contexto

O Acordu é irmão do **Saldu** (ex-Pondus), que já tem um conjunto de decisões de arquitetura registradas e justificadas: banco, ORM, multi-tenancy, documentação da API, hospedagem, autenticação e multiusuário. Os dois produtos têm o mesmo dono, o mesmo servidor e o mesmo perfil (SaaS multi-tenant pequeno, dev solo, free tier ARM).

Repetir cada ADR no repositório do Acordu duplicaria texto que vai divergir com o tempo; ignorá-los deixaria o repositório sem o "porquê" das escolhas.

## Decisão

O Acordu **adota por referência** as seguintes decisões do Saldu, com as mesmas justificativas:

| ADR do Saldu | Decisão herdada |
|---|---|
| 0001 | PostgreSQL (via Npgsql), por rodar em ARM64 e ser vendor-neutro. |
| 0002 | EF Core como ORM; CQRS-lite sem MediatR. |
| 0003 | Multi-tenant com banco e schema compartilhados, `TenantId` + *global query filter*. |
| 0004 | OpenAPI nativo do .NET 10 + Scalar; tipos TS gerados do OpenAPI. |
| 0005 | Núcleo self-hosted (VM + Compose + Postgres + Caddy) + complementares (Cloudflare, Resend, Sentry, Discord). |
| 0007 | ASP.NET Core Identity + JWT curto + refresh revogável + RBAC granular. |
| 0008 | Multiusuário com `Vinculo` N-N e tenant ativo na sessão. |

**Não herdada:** ADR 0006 do Saldu (ingestão de extratos) — específica do domínio financeiro.

**Adaptações próprias do Acordu:**
- **Banco separado** (`acordu`) na **mesma instância** PostgreSQL do Saldu; usuários de banco distintos, sem permissão cruzada.
- **Repositório separado**, mesma estrutura, convenções de código e de commit, prefixo `Acordu.*`.
- **Mesma VM**; o Caddy roteia por domínio. Um compose por produto, compartilhando a rede do Caddy e o serviço Postgres.
- **Object storage** (OCI, API S3-compatível) passa a ser usado em runtime para anexos, não só para backup.

> Hospedagem, nomes da infra compartilhada e plano de escala: **ADR 0006**. O ADR 0005 (serverless) foi revogado em 06/10/2026 e fica como plano B.

Decisões novas e específicas do Acordu começam no ADR 0002 deste repositório.

## Consequências

**Positivas**
- Consistência entre os produtos: aprender um é saber o outro.
- Nenhuma decisão reaberta sem motivo; o repositório continua explicando o "porquê".

**Negativas / riscos**
- **Acoplamento de infraestrutura:** um pico de uso de um produto afeta o outro (CPU, RAM, conexões do Postgres). Mitigação: limites de recursos por container no compose; gatilho de revisão se um produto passar a disputar recursos com o outro.
- **Divergência futura:** se o Saldu revisar um ADR herdado, o Acordu precisa decidir se acompanha. Regra: revisão de ADR herdado exige registro aqui (aceitar ou divergir).
