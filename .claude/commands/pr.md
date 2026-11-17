---
description: Abre o PR da branch atual no formato da convenção de PR
argument-hint: [rascunho]
---

Abra o PR da branch atual contra a `main` seguindo `docs/convencoes-pr-acordu.md`. Argumento: **$ARGUMENTS** (`rascunho` abre como draft).

1. Leia `docs/convencoes-pr-acordu.md`. Confira que não está na `main` (`git branch --show-current`) e que não há nada sem commit (`git status`).
2. Junte o material:
   - `git fetch origin main`, `git log --oneline origin/main..HEAD` e `git diff --stat origin/main...HEAD`;
   - a spec e o design do marco em `docs/specs/<marco>/` (cenários que o PR entrega, decisões D, registro); bug: a issue (`gh issue view N`);
   - o parecer do `revisor-spec` e as ressalvas, se já rodou nesta sessão. Se não rodou, rode-o agora e aplique os achados antes de seguir.
3. Confira antes de escrever:
   - commits em Conventional Commits, agrupados por camada (`docs/convencoes-commit-acordu.md`);
   - coluna Teste da spec preenchida para os cenários entregues e linha no registro do design;
   - `docs/estado.md` atualizado (em revisão: este PR);
   - testes da área e, se tocou no front, `pnpm lint`, `pnpm format:check` e `pnpm build` saíram 0. Se algo falhar, pare e corrija; não abra o PR.
4. Escreva o título e a descrição num arquivo temporário (`/tmp/pr-<branch>.md`) exatamente no formato da convenção. Nada inventado: cada item tem de estar no diff, na spec, no design ou no parecer do revisor.
5. `git push -u origin <branch>` e abra o PR sem perguntar (o PR é a revisão):
   `gh pr create --base main --title "<título>" --body-file /tmp/pr-<branch>.md` (com `--draft` se pedido). Se o `gh pr create` falhar, use `gh api repos/{owner}/{repo}/pulls -f title=… -f head=… -f base=main -F body=@arquivo`.
6. Espere a CI (`gh pr checks --watch`; sem GraphQL, `gh api repos/{owner}/{repo}/commits/<branch>/check-runs` até o `CI ok` terminar). Falhou: leia as annotations, corrija, faça commit e push, e repita. Em rascunho, E2E e imagem ficam pulados (deploy-ci §3): diga que a CI verde é parcial e que a fumaça roda quando o Daniel marcar o PR como pronto (a sessão na nuvem não marca: a API REST não converte rascunho). Termine com o link do PR, o resultado da CI e, se houver, o que está em "Para o Daniel decidir".
