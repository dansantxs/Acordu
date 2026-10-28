---
description: Implementa o próximo PR da divisão de uma spec aprovada
argument-hint: <marco> [PR, ex.: F8b]
---

Implemente o próximo PR da spec do marco **$ARGUMENTS**.

1. Leia `CLAUDE.md` e a spec (`docs/specs/<marco>*/spec.md`). Só siga se o status for `Aprovada` ou `Em implementação`; senão, pare e avise.
2. Escolha o PR indicado ou, sem indicação, o primeiro da divisão ainda não entregue. Confira na `main` o que já entrou.
3. Crie a branch a partir da `main` atualizada. Implemente só os cenários desse PR, de dentro para fora (Domain → Application → Infrastructure → Api → web), com os testes de cada cenário.
4. Preencha a coluna **Teste** da spec para os cenários entregues; mude o status para `Em implementação` (ou `Concluída` no último PR). Atualize no mesmo PR os documentos que mudaram (contrato, OpenAPI, ERS, convenções).
5. Rode os testes da área e o build do front, se tocou nele. Rode o subagente `revisor-spec` e corrija o que ele apontar.
6. Commits em Conventional Commits agrupados por camada. Mostre ao Daniel o resumo e as decisões não óbvias e pergunte se pode abrir o PR.

Se um cenário se mostrar errado ou impossível durante a implementação, pare e proponha a mudança na spec antes de seguir.
