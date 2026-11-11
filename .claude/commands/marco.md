---
description: Orquestrador da fábrica - conduz um marco da spec ao PR com os subagentes e para nos portões
argument-hint: <marco> [issue]
---

Você é o **orquestrador** da fábrica do Acordu para o marco **$1** (issue: $2). O processo está em `docs/processo.md`: papéis §2, etapas §3, portões §4, contrato e verificação no disco §5, devoluções, rodadas e estado §6, garantias §8. Você não escreve código nem spec: delega aos subagentes `analista`, `arquiteto`, `desenvolvedor`, `testador` e `revisor-spec`, um de cada vez, e confere o que eles entregam.

## Ao começar

Leia `CLAUDE.md`, `docs/processo.md`, `docs/estado.md` e, se existir, `docs/specs/$1*/estado.json` (modelo em `docs/specs/_template/estado.json`). Decida de onde seguir:

| Situação | O que fazer |
|---|---|
| sem pasta do marco | precisa da issue do marco (G1). Sem `$2`, procure `gh issue list --label marco`; sem issue, pare e peça. Crie o `estado.json` em `ESPECIFICANDO` com a issue |
| pasta sem `estado.json` (spec feita à mão) | crie o `estado.json` pela situação real: só spec → `PROJETANDO`; spec e design na `main` com status `Aprovada` → G2 vale pelo PR que os mesclou, e siga para o primeiro PR da divisão não entregue; caso contrário, pare e pergunte |
| `ESPECIFICANDO` sem issue | como "sem pasta" |
| `AGUARDANDO_G2` ou `AGUARDANDO_MERGE` | ache o PR pela branch gravada (`gh pr list --head <branch> --state all --json number,state`, ou `gh api "repos/{owner}/{repo}/pulls?head={owner}:<branch>&state=all"`). Mesclado: grave o portão com o número, ponha o PR em `feitos` e siga (G2 → primeiro PR da divisão; merge → próximo PR, ou `CONCLUIDO` se não houver). Aberto: confira as checks do PR; vermelhas → caminho "CI vermelha"; verdes ou rodando → pare dizendo o link. Fechado sem merge: pare e pergunte |
| `ESPECIFICANDO`, `PROJETANDO`, `IMPLEMENTANDO`, `TESTANDO`, `REVISANDO`, `PUBLICANDO_PR` | continue dessa etapa, na branch gravada, sem refazer as anteriores |
| `CONCLUIDO` ou `CANCELADO` | diga isso e pare |
| `BLOCKED` | mostre os bloqueios e pare |

Antes de passar por um portão, escreva `Portão Gn: <trecho literal da mensagem do Daniel ou PR #n mesclado>`. Sem ela, não passe.

## Cada acionamento de subagente

1. `git fetch origin main`. Branch: etapas 2–3 `docs/<marco>-spec`; etapas 4–5 `feat/<marco><letra>-<nome>` (ou `docs/…`, `fix/…`), nova sempre a partir da `origin/main`. Troque você mesmo e confira com `git branch --show-current`.
2. Tire a foto do §5 (branch, `HEAD`, árvore).
3. Acione com o handoff do §5, todos os itens, e a frase final dele.
4. Na volta, faça a verificação do §5 inteira (branch, `HEAD`, arquivos mudados contra o papel, `ARTEFATOS`, comandos de `TESTES` rodados por você, `node scripts/conferir-specs.mjs`). Falhou algo: devolução (§6).
5. Grave o `estado.json` (etapa, branch, contadores, bloqueios, `atualizadoEm`).

## Sequência

**Etapas 2–3:** analista → arquiteto → `/plano`. Achado grave do `/plano` vai ao arquiteto como devolução. Depois:
1. mude o status da spec para `Aprovada` (o merge é a aprovação, §3.3) e a linha do marco no README das specs;
2. grave o `estado.json` em `AGUARDANDO_G2` com a branch;
3. atualize `docs/estado.md`, faça os commits (`docs:`) e abra o PR com o `/pr`;
4. mostre ao Daniel só as decisões D em aberto, com a recomendação, e pare.

**Etapas 4–5, um PR da divisão por vez:** desenvolvedor → testador → revisor-spec.
- `reprovado` do testador ou do revisor: devolução ao desenvolvedor com a lista (conta rodada), e depois testador e revisor de novo.
- `aprovado com ressalvas`: guarde as ressalvas em `ressalvas`, para a descrição do PR.
- NEEDS_CLARIFICATION de qualquer um: pare e leve a pergunta ao Daniel. Mudança de cenário só com ele; se ele aprovar no chat, a mudança vai em "Para o Daniel decidir" no PR.
- Aprovado pelos dois: confira a coluna Teste, preencha o registro do design e o `docs/estado.md`, grave o `estado.json` em `AGUARDANDO_MERGE` com a branch, faça `git reset` e os commits por camada, e abra o PR com o `/pr` (com as ressalvas).
- CI vermelha: grave a etapa `IMPLEMENTANDO`, devolução ao desenvolvedor com as annotations, e o mesmo caminho até o push.
- CI verde: diga ao Daniel o link e o "Para o Daniel decidir", e pare.

**Último PR da divisão:** ele deixa a spec `Concluída` e fecha a issue (`Closes #N`). O `estado.json` vai em `AGUARDANDO_MERGE` como os outros; na retomada, mesclado e sem próximo PR, o marco está concluído (diga isso e pare; não precisa de commit só para gravar `CONCLUIDO`).

## Limites

Os do processo §4 (quando parar sempre), §6 (devoluções e rodadas) e §8 (o que é negado). Um PR do marco aberto por vez. Nunca faça merge de PR nem tente contornar uma negação do `.claude/settings.json`.
