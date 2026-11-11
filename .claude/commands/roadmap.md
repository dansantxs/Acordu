---
description: Etapa 1 - propõe o próximo marco a partir das issues e do estado (portão G1)
argument-hint: [foco opcional]
---

Etapa 1 da fábrica (`docs/processo.md` §3.1, portão G1 no §4). Foco pedido: **$ARGUMENTS**.

1. Leia `CLAUDE.md`, `docs/processo.md`, `docs/estado.md` e `docs/estrategia-produto-acordu.md`.
2. Liste as issues abertas: `gh issue list --state open --limit 100 --json number,title,labels,createdAt` (sem GraphQL: `gh api "repos/{owner}/{repo}/issues?state=open&per_page=100"`, ignorando os itens com `pull_request`). Leia o corpo das que pesarem na escolha.
3. Proponha **até três** candidatos a próximo marco. Para cada um, em poucas linhas:
   - objetivo em uma frase e de onde vem (issues, próximos passos do estado, estratégia);
   - tamanho estimado em PRs;
   - dependências (provisionamento, outro marco, decisão pendente) e riscos;
   - a sua recomendação, com o porquê.
   Bugs de produção (`bug` + `producao`) abertos vêm antes de marco novo; diga isso se houver.
4. **Não crie nada** até o Daniel escolher. Pare e pergunte qual seguir.
5. Escolhido (cite o trecho da mensagem: `Portão G1: "…"`):
   - se já existe a issue, ponha o rótulo `marco` (`gh issue edit N --add-label marco`);
   - senão, crie: `gh issue create --title "<marco>: <título>" --label marco --body "<objetivo, origem, issues relacionadas>"`.
   - Termine com o número da issue e o próximo passo: `/marco <marco> <issue>`.
