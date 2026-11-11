---
description: Implementa o próximo PR da divisão de uma spec aprovada
argument-hint: <marco> [PR, ex.: F8b]
---

Implemente o próximo PR da spec do marco **$ARGUMENTS**.

1. Leia `CLAUDE.md`, `docs/processo.md`, a spec e o design (`docs/specs/<marco>*/`). Só siga se o status for `Aprovada` ou `Em implementação`; senão, pare e avise.
2. Escolha o PR indicado ou, sem indicação, o primeiro da divisão ainda não entregue. Confira na `main` o que já entrou.
3. Crie a branch a partir da `main` atualizada. Acione o agente `desenvolvedor` com o handoff do processo §5 para os cenários desse PR e confira o retorno no disco como no processo §5 (foto antes e depois; rode você os testes de TESTES).
4. Acione o `testador` e depois o `revisor-spec`; devolva ao `desenvolvedor` o que eles reprovarem. Para conduzir o marco inteiro com estado e limites, use o `/marco`.
5. Confira a coluna **Teste** da spec (o testador preenche) para os cenários entregues e preencha a linha do PR no registro do design; mude o status para `Em implementação` (ou `Concluída` no último PR). Atualize no mesmo PR os documentos que mudaram (contrato, OpenAPI, ERS, convenções) e o `docs/estado.md`.
6. Commits em Conventional Commits agrupados por camada. Abra o PR com o `/pr` (sem perguntar: o PR é a revisão).

Se um cenário se mostrar errado ou impossível durante a implementação, pare e proponha a mudança na spec antes de seguir.
