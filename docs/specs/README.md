# Specs por marco

Cada marco do Acordu, a partir do F8, começa por uma spec nesta pasta (ADR 0011).

- Uma pasta por marco: `docs/specs/<marco>-<nome-curto>/spec.md` (ex.: `F8-deploy`).
- Comece por `_template.md` ou pelo comando `/spec <marco>` do Claude Code.
- O PR da spec é a aprovação do plano. Os PRs de código citam os cenários que entregam.
- A spec é viva: mudou o comportamento, a spec muda no mesmo PR.

| Marco | Spec | Status |
|---|---|---|
| F8 | [Deploy no nível 0](F8-deploy/spec.md) | Em implementação |
| F9 | [Proteção do cadastro](F9-protecao-cadastro/spec.md) | Concluída (código); F9-46/47 conferidos no primeiro deploy |
| F10 | [Preenchimento do cliente pelo CNPJ](F10-cnpj-cliente/spec.md) | Concluída |
| F11 | [Fluxo sem retrabalho entre solicitação, tarefas e projeto](F11-fluxo-sem-retrabalho/spec.md) | Concluída |
| F12 | [Cronômetro sem retrabalho, notificação do navegador e app instalável](F12-cronometro-notificacoes-app/spec.md) | Rascunho |
