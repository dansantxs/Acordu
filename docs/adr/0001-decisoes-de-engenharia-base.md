# ADR 0001 — Decisões de engenharia base

- **Status:** Aceito
- **Data:** 30/09/2026 (reescrito em 10/10/2026 para ficar autocontido; hospedagem emendada em 26/10/2026 pelo ADR 0010)
- **Contexto do projeto:** Acordu (gestão de atendimento, execução e contratos, v1)

## Contexto

O Acordu é um SaaS multi-tenant pequeno, desenvolvido por uma pessoa, com custo de infraestrutura zero enquanto não houver cliente pagante. Ele roda em serviços gerenciados com plano gratuito (ADR 0010). Essas condições decidem a base técnica antes de qualquer funcionalidade: banco, ORM, isolamento entre tenants, documentação da API, hospedagem e identidade.

Este ADR registra essas decisões de base num lugar só. Decisões específicas de funcionalidade começam no ADR 0002.

## Decisão

| Tema | Decisão | Por quê |
|---|---|---|
| Banco | **PostgreSQL**, via Npgsql. | Roda nativo em ARM64 (o SQL Server não tem imagem ARM); gratuito, sem licença; portável entre hosts. |
| ORM | **EF Core**; consultas pesadas em SQL pela Infrastructure. **CQRS-lite** com serviços de aplicação simples, **sem MediatR**. | Padrão do ecossistema .NET, migrations maduras e *global query filters* para o multi-tenant. MediatR passou a ter licença comercial e não faz falta. |
| Multi-tenant | **Banco e schema compartilhados**, coluna `TenantId` em toda entidade de negócio, imposta por *global query filter*. | Cabe num banco gratuito pequeno e escala em número de tenants. Risco de vazamento mitigado por teste obrigatório de acesso cross-tenant. |
| Contrato da API | **OpenAPI nativo do .NET 10** como fonte da verdade, **Scalar** como UI; tipos TypeScript do front gerados do OpenAPI. | Um único spec alimenta documentação, exploração e tipos do frontend. |
| Hospedagem | **Vendor-neutro no código, gerenciado na operação:** API .NET em container (Cloud Run), PostgreSQL 17 (Neon), SPA estático e arquivos S3-compatíveis (Cloudflare). Nenhum SDK de provedor; configuração só por variável de ambiente. Complementos: Resend (e-mail), Sentry (erros), Discord (alertas de operação). | Custo zero sem servidor para administrar; trocar de provedor (inclusive para uma VM) é trocar variáveis e DNS. Detalhes e plano de escala: ADR 0010. |
| Identidade | **ASP.NET Core Identity**; access token **JWT curto** + **refresh token persistido e revogável**; autorização **RBAC granular** (papel = conjunto de permissões, verificada na Application). Transporte da sessão: ADR 0007. | Hash de senha, lockout e recuperação prontos e testados; logout real via refresh revogável; papéis como dados, não como código. |
| Multiusuário | `Usuario` **global**; relação com `Tenant` **N-N** por `Vinculo` (que carrega o papel); sessão com **tenant ativo**. | Uma pessoa pode ser membro de um prestador e contato de cliente em outro, com um login só. |

**Infraestrutura** (detalhes no ADR 0010):
- **Banco próprio** (`acordu`) com dois usuários: um dono do schema, só para migrations, e outro só de DML para a aplicação.
- **Repositório próprio**, com estrutura e convenções descritas em `docs/`.
- **Object storage** S3-compatível usado em runtime para anexos, além de backup.
- **Nada compartilhado com outros produtos.**

> Histórico: serverless (ADR 0005, 05/10), VM Oracle compartilhada (ADR 0006, 06/10) e serverless de novo, com o Acordu como único produto (ADR 0010, 26/10).

## Consequências

**Positivas**
- Base técnica estável e explicada num documento; decisões de funcionalidade não precisam reabrir banco, ORM ou hospedagem.
- Portabilidade: trocar de host não toca no Domain nem na Application.

**Negativas / riscos**
- **Limites dos planos gratuitos** (CPU do Cloud Run, CU-h e armazenamento do Neon): acompanhados mensalmente, com gatilhos de escala no ADR 0010.
- **Filtro global como única barreira** entre tenants: mitigado por teste obrigatório de isolamento nos níveis de integração e API, e pela regra de arquitetura que proíbe `IgnoreQueryFilters` fora da Infrastructure.

## Alternativas consideradas

- **SQL Server:** familiar, mas sem suporte a ARM64; obrigaria a sair do free tier.
- **NHibernate / Dapper como ORM principal:** NHibernate com ecossistema menos ativo; Dapper só como complemento pontual.
- **Schema ou banco por tenant:** isolamento maior, mas inviável no orçamento e caro de operar.
- **PaaS completo (Vercel + Supabase):** menos operação, mas não hospeda a API .NET e duplica o Postgres com lock-in.
