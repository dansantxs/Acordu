---
description: Confere uma spec contra ERS, ADRs e contrato e aponta lacunas
argument-hint: <marco>
---

Revise a spec do marco **$ARGUMENTS** (`docs/specs/$ARGUMENTS*/spec.md`) antes da aprovação.

Confira e liste, sem corrigir nada ainda:
1. Cenários que contradizem a ERS, um ADR, o contrato da API, o modelo de domínio ou a segurança (cite o trecho).
2. Requisitos da ERS que o marco toca e que nenhum cenário cobre.
3. Cenários ambíguos ou que não dá para testar (sem resultado observável).
4. Casos esquecidos: permissão negada, isolamento entre tenants e entre clientes, versão (`If-Match`), dados inválidos, portal sem item interno.
5. PRs da divisão que dependem um do outro (o fluxo não empilha PRs) ou grandes demais.
6. Decisões tomadas na spec que deveriam virar ADR ou convenção.

Responda em lista curta, do mais grave ao menos grave, e pergunte quais pontos ajustar.
