---
description: Confere spec e design de um marco contra ERS, ADRs e contrato e aponta lacunas
argument-hint: <marco>
---

Revise a spec e o design do marco **$ARGUMENTS** (`docs/specs/$ARGUMENTS*/spec.md` e `design.md`) antes do G2 (`docs/processo.md` §4).

Confira e liste, sem corrigir nada ainda:
1. Cenários ou decisões que contradizem a ERS, um ADR, o contrato da API, o modelo de domínio ou a segurança (cite o trecho).
2. Requisitos da ERS que o marco toca e que nenhum cenário cobre.
3. Cenários ambíguos ou que não dá para testar (sem resultado observável).
4. Casos esquecidos: permissão negada, isolamento entre tenants e entre clientes, versão (`If-Match`), dados inválidos, portal sem item interno.
5. PRs da divisão que dependem um do outro (o fluxo não empilha PRs), grandes demais ou com cenário que não está em nenhum PR.
6. Decisões do design que deveriam virar ADR ou convenção (valem além do marco).
7. Mistura entre os arquivos: decisão ou divisão escrita na spec, cenário no design (o nome do PR no título das tabelas da spec é permitido, processo §3.2).

Responda em lista curta, do mais grave ao menos grave, e pergunte quais pontos ajustar.
