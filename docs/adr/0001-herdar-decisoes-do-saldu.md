# ADR 0001 — Herdar as decisões de engenharia do Saldu

- **Status:** Aceito
- **Data:** 30/09/2026
- **Contexto do projeto:** Acordu (gestão de atendimento, execução e contratos, v1)

## Contexto

O Acordu é irmão do **Saldu** (ex-Pondus), que já tem um conjunto de decisões de arquitetura registradas e justificadas: banco, ORM, multi-tenancy, documentação da API, hospedagem, autenticação e multiusuário. Os dois produtos têm o mesmo dono e o mesmo perfil (SaaS multi-tenant pequeno, dev solo, custo zero até ter usuário pagante).

Repetir cada ADR no repositório do Acordu duplicaria texto que vai divergir com o tempo; ignorá-los deixaria o repositório sem o "porquê" das escolhas.

## Decisão

O Acordu **adota por referência** as seguintes decisões do Saldu, com as mesmas justificativas:

| ADR do Saldu | Decisão herdada |
|---|---|
| 0001 | PostgreSQL (via Npgsql), por ser vendor-neutro e ter oferta gerenciada gratuita e self-hosted. |
| 0002 | EF Core como ORM; CQRS-lite sem MediatR. |
| 0003 | Multi-tenant com banco e schema compartilhados, `TenantId` + *global query filter*. |
| 0004 | OpenAPI nativo do .NET 10 + Scalar; tipos TS gerados do OpenAPI. |
| 0005 | ~~Núcleo self-hosted (VM + Compose + Postgres + Caddy)~~ — **substituído pelo ADR 0005 do Acordu nos níveis 0–2**. Continuam herdados os complementares (Cloudflare, Resend, Sentry, Discord). |
| 0007 | ASP.NET Core Identity + JWT curto + refresh revogável + RBAC granular. |
| 0008 | Multiusuário com `Vinculo` N-N e tenant ativo na sessão. |

**Não herdada:** ADR 0006 do Saldu (ingestão de extratos) — específica do domínio financeiro.

**Adaptações próprias do Acordu:**
- **Repositório separado**, mesma estrutura, convenções de código e de commit, prefixo `Acordu.*`.
- **Object storage** (API S3-compatível) passa a ser usado em runtime para anexos, não só para backup.
- ~~Banco separado na mesma instância PostgreSQL do Saldu; mesma VM, com Caddy roteando por domínio e um compose por produto.~~ **Substituído pelo ADR 0005** (05/10/2026): o Saldu perdeu a conta Oracle Cloud e, nos níveis 0–2, cada produto tem projeto Google e banco Neon próprios. A infraestrutura compartilhada (equivalente ao ADR 0009 do Saldu) volta só no nível 3.

Decisões novas e específicas do Acordu começam no ADR 0002 deste repositório.

## Consequências

**Positivas**
- Consistência entre os produtos: aprender um é saber o outro.
- Nenhuma decisão reaberta sem motivo; o repositório continua explicando o "porquê".

**Negativas / riscos**
- **Acoplamento de infraestrutura:** nos níveis 0–2 (ADR 0005) se resume à cota gratuita do Cloud Run e do Cloud Scheduler, que é da conta de faturamento e dividida com o Saldu. No nível 3 volta o compartilhamento de VM e Postgres, com limites de recursos por container.
- **Divergência futura:** se o Saldu revisar um ADR herdado, o Acordu precisa decidir se acompanha. Regra: revisão de ADR herdado exige registro aqui (aceitar ou divergir).
