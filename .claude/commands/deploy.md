---
description: Etapa 6 - confere a main, mostra o que entra e dispara o deploy só com autorização (portão G4)
argument-hint: [ref, padrão main]
---

Etapa 6 da fábrica (`docs/processo.md` §3.6, portão G4 no §4). Ref: **$ARGUMENTS** (padrão `main`; outro valor, pare e pergunte: o deploy é só da `main`).

1. Leia `docs/deploy-ci-acordu-v1.md` (§ do deploy e o checklist) e `docs/estado.md`.
2. **CI da `main`:** `git fetch origin main` e confira o `CI ok` do commit `origin/main` (`gh api "repos/{owner}/{repo}/commits/<sha>/check-runs?check_name=CI%20ok"`). Sem `CI ok` com `success` (falhou, pendente ou inexistente): **recuse**, diga o que encontrou (liste as checks com `?per_page=100`) e pare. Na `main` a CI sempre roda tudo, então `CI ok` inexistente quer dizer que ela ainda não rodou ou foi cancelada.
3. **O que entra:** o último deploy com sucesso (`gh run list --workflow deploy.yml --status success --limit 1 --json headSha,createdAt`, ou `gh api "repos/{owner}/{repo}/actions/workflows/deploy.yml/runs?status=success&per_page=1"`). Mostre `git log --oneline <headSha>..origin/main` agrupado por PR (sem deploy anterior com sucesso, é o primeiro: liste os PRs desde o começo resumidos por marco, todas as migrations e o checklist do primeiro deploy do `deploy-ci` inteiro), e destaque migrations novas (`src/Acordu.Infrastructure/**/Migrations/`), variáveis ou secrets novos citados no `deploy.yml` e nos registros dos designs, e conferências marcadas "no primeiro deploy" ou "conferido no deploy" nas specs.
4. Pergunte: "Pode fazer o deploy de `<sha curto>`?" e **pare**. Só siga com um "pode" (ou equivalente) do Daniel nesta conversa; cite: `Portão G4: "…"`.
5. Dispare: `gh workflow run deploy.yml --ref main` (a permissão pede confirmação de novo; é esperado). Acompanhe (`gh run watch` ou `gh api .../actions/runs/<id>`).
6. **Sucesso:** rode as conferências pendentes que dá para fazer daqui (health pela borda, cabeçalhos do SPA, o que o checklist pede) e liste ao Daniel as que precisam dele. Registre no design de cada marco tocado a linha "conferido no deploy de DD/MM" e, se mudou, a coluna Teste; isso vai num PR `docs:` pelo `/pr`.
7. **Falha:** o workflow faz rollback sozinho. Leia o job que falhou (annotations), relate em poucas linhas e abra a issue com o `/triagem` (rótulos `bug` e `producao`). Não tente outro deploy sem novo G4.
