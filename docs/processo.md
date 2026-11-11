# Acordu — Processo de desenvolvimento (fábrica agêntica semi-autônoma)

Fonte única de **como** o Acordu é construído. O **porquê** está no ADR 0013. `CLAUDE.md`, o README das specs, as convenções e os agentes do Claude Code apontam para cá e não repetem estas regras.

Um **orquestrador** conduz o trabalho e aciona **subagentes**, um por papel e um de cada vez. O Daniel decide nos **portões**. Entre um portão e outro, o orquestrador segue sem perguntar.

> Itens marcados com (P1b) ou (P1c) entram nesses PRs do marco P1. Até lá vale o fluxo com comandos: `/spec`, `/design`, `/plano`, `/tarefas`, `/pr` e `revisor-spec`.

## 1. Onde fica cada coisa

Cada informação tem um lugar só. Na dúvida, siga esta tabela.

| O quê | Onde | Muda quando |
|---|---|---|
| Ideia, bug, sugestão, erro de produção | GitHub Issues, com rótulo (§7) | chega algo novo (etapas 1 e 7) |
| Requisito do produto (vale para sempre) | `docs/ers/ERS-acordu-v1.md` | uma spec cria ou muda um RF/RNF |
| **O quê** um marco entrega: objetivo, cenários, fora de escopo | `docs/specs/<marco>/spec.md` | etapa 2; viva até o fim do marco |
| **Como** o marco é feito: decisões D, impacto nos documentos, divisão em PRs, registro | `docs/specs/<marco>/design.md` | etapa 3; o registro cresce a cada PR |
| Onde o trabalho do marco parou (máquina) | `docs/specs/<marco>/estado.json` (P1b) | só o orquestrador escreve, a cada passo |
| Anotações de trabalho dos agentes | `docs/specs/<marco>/notas/` (P1b) | durante o marco; nunca na raiz |
| Decisão de arquitetura que vale **além** do marco | `docs/adr/NNNN-*.md` | o design promove uma D a ADR |
| Regra de código que vale para todo o repositório | `docs/convencoes-codigo-acordu.md` | um PR cria a regra |
| Formato de commit e de PR | `docs/convencoes-commit-acordu.md`, `docs/convencoes-pr-acordu.md` | raramente |
| Interface da API | contrato + `openapi/*.json` | a API muda |
| Visão geral: em revisão, marco atual, próximos passos | `docs/estado.md` | a cada PR |
| Histórico do que já foi feito | git, PRs e o registro dos designs | sozinho |

Regra de promoção: uma decisão D que só faz sentido dentro do marco fica no `design.md`. Se outro marco vai ter de segui-la, ela vira ADR ou convenção no mesmo PR, e o design só aponta para ela.

Specs dos marcos F8 a F13 continuam num arquivo só (`spec.md` com tudo): o formato em dois arquivos vale a partir do P1.

## 2. Papéis

| Papel | Arquivo | Faz | Não faz |
|---|---|---|---|
| **Orquestrador** | `/marco` (P1b) | conduz as etapas, monta o handoff, troca de branch, confere no disco, roda os testes, escreve o `estado.json`, faz commit e push, abre o PR | escreve código ou spec; faz merge; dispara deploy sem o G4 |
| Analista | agente `analista` (P1b) | `spec.md` e o RF/RNF novo na ERS | design, código |
| Arquiteto | agente `arquiteto` (P1b) | `design.md`, rascunho de ADR, revisão contra ADRs | código |
| Desenvolvedor | agente `desenvolvedor` (P1b) | código e testes de unidade de um PR da divisão, documentos afetados | mudar cenários; editar testes de aceite do testador |
| Testador (QA) | agente `testador` (P1b) | testes que verificam o "então" de cada cenário, isolamento, `If-Match`, casos de borda; coluna Teste | código de produção |
| Revisor | agente `revisor-spec` | revisa a branch contra spec, design, ADRs e convenções | editar arquivos |

Subagentes nunca acionam outros subagentes e nunca fazem commit, push, merge, branch nem usam o `gh`. Só o orquestrador fala com o Daniel.

Comandos fora do fluxo de um marco: `/roadmap` (etapa 1), `/deploy` (etapa 6) e `/triagem` (etapa 7), todos no P1c; `/pr`, que o orquestrador usa e que também serve para trabalho avulso.

## 3. Etapas

| # | Etapa | Quem | Sai | Avança quando (conferido no disco) | Portão |
|---|---|---|---|---|---|
| 1 | Planejamento | `/roadmap` | até três candidatos a marco | — | **G1** — o Daniel escolhe (issue `marco`) |
| 2 | Requisitos | analista | `spec.md`, ERS | cenários numerados sem repetição; `conferir-specs` sai 0 | — |
| 3 | Design | arquiteto; `/plano` confere | `design.md`, ADR se preciso | toda D com opções e recomendação; divisão em PRs com cenários; `/plano` sem achado grave | **G2** — merge do PR de spec e design, com a escolha de cada D |
| 4 | Desenvolvimento | desenvolvedor | código, testes, documentos | testes da área e build saem 0 (o orquestrador roda) | — |
| 5 | Testes e revisão | testador, depois revisor | testes que faltavam, parecer | cenários do PR com teste; revisor sem achado que bloqueia; testes saem 0 | — |
| 5b | PR e CI | orquestrador (`/pr`) | PR no formato da convenção | CI verde | **G3** — o Daniel faz o merge |
| 6 | Implantação | `/deploy` | deploy, conferências, aviso no Discord | `CI ok` na `main` | **G4** — o Daniel autoriza cada deploy |
| 7 | Manutenção | `/triagem` | issue com rótulo; bug com cenário de regressão | — | prioridade decidida no G1 |

Depois do G3, o orquestrador segue para o próximo PR da divisão (volta à etapa 4). O último PR do marco marca a spec `Concluída` e fecha a issue do marco.

### 3.1 Planejamento
`/roadmap` lê as issues abertas, o `docs/estado.md` e `docs/estrategia-produto-acordu.md` e propõe até três candidatos, com tamanho em PRs, dependências e riscos. Não cria nada: o Daniel escolhe, e o comando abre ou rotula a issue do marco.

### 3.2 Requisitos
Template `docs/specs/_template/spec.md`. Cenários **dado / quando / então**, numerados `<marco>-NN`, em tabelas com a coluna Id; o título de cada tabela pode nomear o PR da divisão que a entrega (é só rótulo: a divisão fica no design). Um comportamento por cenário, com permissão, isolamento entre tenants e clientes e erro sempre que se aplicam. Requisito que falta na ERS entra na ERS no mesmo PR. Dúvida de requisito volta ao Daniel (NEEDS_CLARIFICATION); o analista não inventa.

### 3.3 Design
Template `docs/specs/_template/design.md`. Decisões D com opções e recomendação justificada, impacto em cada documento ("nenhum" também é resposta), divisão em PRs pequenos e independentes com os cenários de cada um, e o registro. Spec e design vão no mesmo PR; o G2 aprova os dois e as escolhas das D.

### 3.4 Desenvolvimento
Um PR da divisão por vez, numa branch a partir da `main`: Domain → Application → Infrastructure → Api → web, testes de cada cenário, documentos afetados no mesmo PR. Dúvida técnica o desenvolvedor decide e registra como premissa no `notas/`; cenário que se mostra errado volta como NEEDS_CLARIFICATION e reabre o G2 só para aquela mudança.

### 3.5 Testes e revisão
O testador escreve o que falta para cada cenário do PR verificar o "então". O revisor dá parecer `aprovado`, `aprovado com ressalvas` (as ressalvas vão para a descrição do PR) ou `reprovado`. Reprovado ou defeito do testador volta ao desenvolvedor e passa de novo pelo testador e pelo revisor. O orquestrador preenche a coluna Teste e o registro do design, faz os commits por camada e abre o PR com o `/pr`.

### 3.6 Implantação
`/deploy` confere o `CI ok` da `main`, mostra o que entra desde o último deploy e, só com o "pode" do Daniel, dispara o `deploy.yml`, acompanha e roda as conferências pendentes das specs (verificações marcadas "no primeiro deploy"). Falha: o workflow já faz rollback; o comando relata e abre issue `bug`.

### 3.7 Manutenção
`/triagem` recebe um aviso colado do Discord, um id do Error Reporting (`gcloud`, quando logado) ou um relato, e:
- procura issue igual antes de criar outra (repetição vira comentário);
- bug: issue `bug` com o cenário de regressão (dado / quando / então); o PR de correção cita `Closes #N`, cobre o cenário com teste e não precisa de spec;
- sugestão ou melhoria pequena: issue `sugestao` ou `melhoria`, sem dado pessoal;
- algo do tamanho de um marco: issue `marco`, que vai para o G1.

## 4. Portões

| Portão | O Daniel… | Sem ele o orquestrador… |
|---|---|---|
| G1 | escolhe o próximo marco | não começa spec |
| G2 | aprova spec, design e a escolha de cada D (merge do PR) | não escreve código do marco |
| G3 | revisa e faz merge de cada PR | não faz merge nem abre o PR seguinte do marco |
| G4 | autoriza cada deploy | não dispara o deploy |

**Aprovação é mensagem, não commit.** Antes de passar por um portão, o orquestrador cita na resposta o trecho literal do Daniel que aprovou (ou confere o merge com `gh pr view`). "Seguindo as recomendações" vale como escolha de todas as D.

Sempre param o orquestrador, mesmo entre portões: qualquer coisa irreversível (apagar branch remota, dado, secret, recurso de nuvem), mudança em ADR aceito, dúvida sobre a origem de algo (regra do ASMTasks no `CLAUDE.md`), decisão que o design não previu e BLOCKED (§6).

## 5. Contrato entre orquestrador e subagente

**Handoff** (o subagente começa sem contexto): marco, PR da divisão e branch; etapa; arquivos de entrada (spec, design, ADRs que se aplicam); decisões já tomadas; limite de escopo (cenários do PR, fora de escopo); onde anotar (`notas/`); comandos de verificação; o que corrigir, se for devolução; e "não acione outros subagentes; execute só esta etapa e devolva o bloco de status".

**Retorno**, sempre neste bloco:

```text
STATUS: DONE | BLOCKED | NEEDS_CLARIFICATION
PARECER: aprovado | aprovado com ressalvas | reprovado   (revisor e testador)
ARTEFATOS: caminhos criados ou alterados, um por linha
TESTES: comando executado e código de saída
BLOQUEIOS: itens, ou "nenhum"
PRÓXIMO: etapa sugerida, ou "nenhum"
```

**Verificação no disco:** o orquestrador confere que os `ARTEFATOS` existem e mudaram (`git status`, `git diff`), roda ele mesmo os comandos de `TESTES` e só avança se saírem 0. Relato sem arquivo ou sem comando executado é etapa não concluída. Antes de cada acionamento, o orquestrador confere a branch (`git branch --show-current`); trabalho na `main` reprova a etapa.

## 6. Devoluções, limites e estado

- Até **2 devoluções por etapa** e **5 rodadas de correção por PR** (revisor ou testador reprovando, CI falhando). Estourou: `BLOCKED`, o orquestrador para e explica ao Daniel.
- Mudança de requisito sempre volta ao G2.
- `docs/specs/<marco>/estado.json` guarda: etapa, PR atual e branch, contadores de devolução e de rodadas, comandos de verificação, portões aprovados (com o trecho da mensagem ou o número do PR mesclado) e bloqueios. Uma sessão nova lê o arquivo e continua da primeira etapa pendente; o merge é conferido com `gh pr view`.
- Ciclo do campo `etapa`: `ESPECIFICANDO` → `PROJETANDO` → ⏸ `AGUARDANDO_G2` → `IMPLEMENTANDO` → `TESTANDO` → `REVISANDO` → `PUBLICANDO_PR` → ⏸ `AGUARDANDO_MERGE` → (próximo PR: `IMPLEMENTANDO`) → `CONCLUIDO`. `BLOCKED` e `CANCELADO` em qualquer ponto.

## 7. Backlog (GitHub Issues)

Rótulos (criados no P1c):

| Rótulo | Uso |
|---|---|
| `marco` | trabalho que pede spec; entra no G1 |
| `bug` | comportamento errado; leva cenário de regressão |
| `melhoria` | mudança pequena sem spec (um PR) |
| `sugestao` | veio de #sugestoes ou de quem usa |
| `producao` | veio de #alertas ou do Error Reporting |
| `triagem` | ainda não classificado |

Issue de marco fechada pelo último PR do marco. Issue de bug fechada pelo PR da correção.

## 8. Garantias pela ferramenta, não só pelo prompt

Regra escrita só no prompt depende de o modelo obedecer; a ferramenta aplica a do cabeçalho.

- **Agentes** (`.claude/agents/*.md`): `tools:` limita o que cada um usa (o revisor só lê; nenhum subagente tem o `gh` nem o `git` de escrita no prompt, e o orquestrador confere no disco).
- **`.claude/settings.json`** (P1b), negado para todos: `gh pr merge`, `git push --force`/`-f`, `git reset --hard`, `git rebase`, `git push origin main`, `gh workflow run deploy.yml` sem pergunta (`ask`).
- **Modelo diferente para quem verifica:** o revisor e o testador usam outro modelo que o desenvolvedor (campo `model:` do agente), para os erros não serem os mesmos de quem escreveu.

## 9. Rastreabilidade

Issue → RF/RNF (ERS) → cenário (`spec.md`) → decisão (`design.md` / ADR) → PR (descrição cita cenários e issue) → teste (coluna Teste) → deploy (registro do design e aviso no #deploy).

Cenário conta como feito quando um teste automatizado verifica o "então" ou, se a verificação é manual, quando a coluna Teste diz como e quando foi conferido.
