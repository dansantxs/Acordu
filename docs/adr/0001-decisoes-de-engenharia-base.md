# ADR 0001 — Decisões de engenharia base

- **Status:** Aceito
- **Data:** 30/09/2026 (reescrito em 10/10/2026 para ficar autocontido)
- **Contexto do projeto:** Acordu (gestão de atendimento, execução e contratos, v1)

## Contexto

O Acordu é um SaaS multi-tenant pequeno, desenvolvido por uma pessoa, com custo de infraestrutura zero enquanto não houver cliente pagante. Ele divide uma VM ARM (Oracle Cloud Always Free) com outros produtos da Nebrys. Essas condições decidem a base técnica antes de qualquer funcionalidade: banco, ORM, isolamento entre tenants, documentação da API, hospedagem e identidade.

Este ADR registra essas decisões de base num lugar só. Decisões específicas de funcionalidade começam no ADR 0002.

## Decisão

| Tema | Decisão | Por quê |
|---|---|---|
| Banco | **PostgreSQL**, via Npgsql. | Roda nativo em ARM64 (o SQL Server não tem imagem ARM); gratuito, sem licença; portável entre hosts. |
| ORM | **EF Core**; consultas pesadas em SQL pela Infrastructure. **CQRS-lite** com serviços de aplicação simples, **sem MediatR**. | Padrão do ecossistema .NET, migrations maduras e *global query filters* para o multi-tenant. MediatR passou a ter licença comercial e não faz falta. |
| Multi-tenant | **Banco e schema compartilhados**, coluna `TenantId` em toda entidade de negócio, imposta por *global query filter*. | Cabe no orçamento de 12 GB e escala em número de tenants. Risco de vazamento mitigado por teste obrigatório de acesso cross-tenant. |
| Contrato da API | **OpenAPI nativo do .NET 10** como fonte da verdade, **Scalar** como UI; tipos TypeScript do front gerados do OpenAPI. | Um único spec alimenta documentação, exploração e tipos do frontend. |
| Hospedagem | Núcleo **self-hosted e vendor-neutro**: VM + Docker Compose + PostgreSQL + Caddy (TLS automático). **Serviços gerenciados só como complemento:** Cloudflare (DNS), Resend (e-mail), Sentry (erros), Discord (alertas de operação). | Custo zero e portabilidade; os complementares preenchem lacunas sem prender o núcleo. Detalhes e plano de escala: ADR 0006. |
| Identidade | **ASP.NET Core Identity**; access token **JWT curto** + **refresh token persistido e revogável**; autorização **RBAC granular** (papel = conjunto de permissões, verificada na Application). Transporte da sessão: ADR 0007. | Hash de senha, lockout e recuperação prontos e testados; logout real via refresh revogável; papéis como dados, não como código. |
| Multiusuário | `Usuario` **global**; relação com `Tenant` **N-N** por `Vinculo` (que carrega o papel); sessão com **tenant ativo**. | Uma pessoa pode ser membro de um prestador e contato de cliente em outro, com um login só. |

**Adaptações de infraestrutura** (detalhes no ADR 0006):
- **Banco próprio** (`acordu`) na instância PostgreSQL compartilhada da VM; usuários de banco exclusivos, sem permissão em bancos de outros produtos.
- **Repositório próprio**, com estrutura e convenções descritas em `docs/`.
- **Mesma VM** de outros produtos; o Caddy de borda roteia por domínio. Um compose por produto, ligado à rede compartilhada.
- **Object storage** (OCI, API S3-compatível) usado em runtime para anexos, além de backup.

> O ADR 0005 (serverless) foi revogado em 06/10/2026 e fica como plano B.

## Consequências

**Positivas**
- Base técnica estável e explicada num documento; decisões de funcionalidade não precisam reabrir banco, ORM ou hospedagem.
- Portabilidade: trocar de host não toca no Domain nem na Application.

**Negativas / riscos**
- **Acoplamento de infraestrutura:** um pico de uso de outro produto na mesma VM afeta o Acordu (CPU, RAM, conexões do Postgres). Mitigação: limites de recursos por container; gatilho de revisão se os produtos passarem a disputar recursos.
- **Filtro global como única barreira** entre tenants: mitigado por teste obrigatório de isolamento nos níveis de integração e API, e pela regra de arquitetura que proíbe `IgnoreQueryFilters` fora da Infrastructure.

## Alternativas consideradas

- **SQL Server:** familiar, mas sem suporte a ARM64; obrigaria a sair do free tier.
- **NHibernate / Dapper como ORM principal:** NHibernate com ecossistema menos ativo; Dapper só como complemento pontual.
- **Schema ou banco por tenant:** isolamento maior, mas inviável no orçamento e caro de operar.
- **PaaS completo (Vercel + Supabase):** menos operação, mas não hospeda a API .NET e duplica o Postgres com lock-in.
