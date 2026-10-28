---
description: Escreve a spec de um marco a partir do template (ADR 0011)
argument-hint: <marco> <descrição curta>
---

Escreva a spec do marco **$ARGUMENTS**.

1. Leia `CLAUDE.md`, `docs/specs/_template.md` e os documentos que o marco toca (ERS, ADRs, contrato da API, modelo de domínio, segurança). Leia o código existente da área para saber o que já está pronto.
2. Crie `docs/specs/<marco>-<nome-curto>/spec.md` com status `Rascunho`:
   - objetivo em uma ou duas frases;
   - cenários de aceite numerados (dado / quando / então), um comportamento por cenário, cobrindo também os casos de erro, permissão e isolamento entre tenants e clientes quando se aplicam;
   - fora de escopo explícito;
   - decisões que dependem do Daniel, cada uma com opções e uma recomendação justificada;
   - impacto nos documentos;
   - divisão em PRs pequenos e independentes, cada um com os cenários que entrega.
3. Acrescente a linha do marco em `docs/specs/README.md`.
4. Não escreva código. Não invente requisito que não esteja na ERS: se faltar, liste em "Impacto nos documentos" como mudança proposta.
5. Termine mostrando ao Daniel só as decisões em aberto, com a recomendação, e pergunte se pode abrir o PR da spec.
