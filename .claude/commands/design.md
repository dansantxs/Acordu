---
description: Escreve o design de um marco a partir da spec (decisões, impacto, divisão em PRs)
argument-hint: <marco>
---

Escreva o design do marco **$ARGUMENTS** seguindo `docs/processo.md` §3.3.

1. Leia `CLAUDE.md`, `docs/processo.md`, a spec (`docs/specs/$ARGUMENTS*/spec.md`), `docs/specs/_template/design.md`, os ADRs que se aplicam, o contrato da API, o modelo de domínio, a segurança e o código existente da área.
2. Crie `docs/specs/<pasta do marco>/design.md` a partir do template, pelas regras do processo §3.3 e a regra de promoção do §1 (D que vale além do marco vira ADR ou convenção no mesmo PR).
3. Ajuste os títulos das tabelas da spec para nomear o PR de cada grupo de cenários (processo §3.2).
4. Não escreva código. Rode `node scripts/conferir-specs.mjs`.
5. Rode o `/plano` sobre spec e design e corrija o que ele apontar. Termine mostrando ao Daniel só as decisões em aberto, com a recomendação.
