---
description: Implementa o próximo PR da divisão de uma spec aprovada
argument-hint: <marco> [PR, ex.: F8b]
---

Implemente o próximo PR da spec do marco **$ARGUMENTS**.

1. Leia `CLAUDE.md`, `docs/processo.md`, a spec e o design (`docs/specs/<marco>*/`). Só siga se o status for `Aprovada` ou `Em implementação`; senão, pare e avise.
2. Escolha o PR indicado ou, sem indicação, o primeiro da divisão ainda não entregue. Confira na `main` o que já entrou.
3. Crie a branch a partir da `main` atualizada. Implemente só os cenários desse PR, como no processo §3.4, com os testes de cada cenário.
4. Preencha a coluna **Teste** da spec para os cenários entregues e a linha do PR no registro do design; mude o status para `Em implementação` (ou `Concluída` no último PR). Atualize no mesmo PR os documentos que mudaram (contrato, OpenAPI, ERS, convenções) e o `docs/estado.md`.
5. Rode os testes da área e o build do front, se tocou nele. Rode o subagente `revisor-spec` e corrija o que ele apontar.
6. Commits em Conventional Commits agrupados por camada. Abra o PR com o `/pr` (sem perguntar: o PR é a revisão).

Se um cenário se mostrar errado ou impossível durante a implementação, pare e proponha a mudança na spec antes de seguir.
