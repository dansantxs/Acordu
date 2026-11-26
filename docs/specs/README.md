# Specs por marco

Uma pasta por marco: `docs/specs/<marco>-<nome-curto>/`, com `spec.md` (o quê) e `design.md` (como). Templates em `_template/`. Como escrever, aprovar e manter: [`docs/processo.md`](../processo.md) §3. A CI confere a estrutura (`scripts/conferir-specs.mjs`).

| Marco | Spec | Design | Status |
|---|---|---|---|
| F8 | [Deploy no nível 0](F8-deploy/spec.md) | — | Em implementação |
| F9 | [Proteção do cadastro](F9-protecao-cadastro/spec.md) | — | Concluída (código); F9-46/47 conferidos no primeiro deploy |
| F10 | [Preenchimento do cliente pelo CNPJ](F10-cnpj-cliente/spec.md) | — | Concluída |
| F11 | [Fluxo sem retrabalho entre solicitação, tarefas e projeto](F11-fluxo-sem-retrabalho/spec.md) | — | Concluída |
| F12 | [Cronômetro sem retrabalho, notificação do navegador e app instalável](F12-cronometro-notificacoes-app/spec.md) | — | Concluída |
| F13 | [Monitoramento pelo Discord e "Sugerir melhoria"](F13-monitoramento-sugestoes/spec.md) | — | Concluída |
| P1 | [Fábrica agêntica semi-autônoma](P1-fabrica-agentica/spec.md) | [design](P1-fabrica-agentica/design.md) | Concluída |
| F14 | [Situação da empresa e cadastro controlado](F14-lancamento-controlado/spec.md) | [design](F14-lancamento-controlado/design.md) | Concluída |
| F15 | [Omnichannel A: solicitação por e-mail e situação pelo canal de origem](F15-omnichannel-email/spec.md) | [design](F15-omnichannel-email/design.md) | Aprovada |
