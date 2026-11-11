# Acordu — Processo de desenvolvimento (fábrica agêntica semi-autônoma)

Fonte única de **como** o Acordu é construído. O **porquê** está no ADR 0013. `CLAUDE.md`, o README das specs, as convenções e os comandos do Claude Code apontam para cá e não repetem estas regras.

Os agentes do Claude Code fazem o trabalho das sete etapas do ciclo. O Daniel decide nos **portões**. Entre um portão e outro, o agente segue sem perguntar.

> Itens marcados com (P1a) ou (P1b) entram nesses PRs do marco P1. Até lá vale o fluxo atual: `/spec`, `/plano`, `/tarefas` e `revisor-spec`.

## 1. Onde fica cada coisa

Cada informação tem um lugar só. Na dúvida, siga esta tabela.

| O quê | Onde | Muda quando |
|---|---|---|
| Ideia, bug, sugestão, erro de produção | GitHub Issues, com rótulo (§5) | chega algo novo (etapas 1 e 7) |
| Requisito do produto (vale para sempre) | `docs/ers/ERS-acordu-v1.md` | uma spec cria ou muda um RF/RNF |
| **O quê** um marco entrega: objetivo, cenários, fora de escopo | `docs/specs/<marco>/spec.md` | etapa 2; viva até o fim do marco |
| **Como** o marco é feito: decisões D, impacto nos documentos, divisão em PRs, registro | `docs/specs/<marco>/design.md` | etapa 3; o registro cresce a cada PR |
| Decisão de arquitetura que vale **além** do marco | `docs/adr/NNNN-*.md` | o design promove uma D a ADR |
| Regra de código que vale para todo o repositório | `docs/convencoes-codigo-acordu.md` | um PR cria a regra |
| Interface da API | contrato + `openapi/*.json` | a API muda |
| O que está em andamento e o que vem depois | `docs/estado.md` (P1a) | a cada PR |
| Histórico do que já foi feito | git, PRs e o registro dos designs | sozinho |

Regra de promoção: uma decisão D que só faz sentido dentro do marco fica no `design.md`. Se outro marco vai ter de segui-la, ela vira ADR ou convenção no mesmo PR, e o design só aponta para ela.

Specs dos marcos F8 a F13 continuam num arquivo só (`spec.md` com tudo): o formato em dois arquivos vale a partir do P1.

## 2. Etapas

| # | Etapa | Entrada | Comando / agente | Sai | Portão |
|---|---|---|---|---|---|
| 1 | Planejamento | issues, `docs/estado.md`, estratégia do produto | `/roadmap` (P1b) | proposta do próximo marco: objetivo, tamanho, dependências, riscos | **G1** — o Daniel escolhe o marco (issue com rótulo `marco`) |
| 2 | Requisitos | issue do marco, ERS | `/spec` | `spec.md` (Rascunho) e o RF/RNF novo na ERS | **G2** — merge do PR da spec e do design |
| 3 | Design e arquitetura | spec, ADRs, contrato, domínio, segurança | `/design` + agente `arquiteto` (P1b); `/plano` revisa | `design.md` com as decisões D (opções e recomendação), rascunho de ADR | **G2** (mesmo PR) e **G3** — o Daniel escolhe cada D |
| 4 | Desenvolvimento | design aprovado | `/tarefas` | branch, código de dentro para fora, testes, documentos, commits | — |
| 5 | Testes e revisão | a branch | agente `testador` (P1b), `revisor-spec`, CI | testes que faltavam, achados aplicados, CI verde, `/pr` abre o PR (P1a) | **G4** — merge de cada PR |
| 6 | Implantação | `main` verde | `/deploy` (P1b) | workflow disparado, conferências do deploy, aviso no Discord | **G5** — o Daniel autoriza cada deploy |
| 7 | Manutenção | #alertas, Error Reporting, #sugestoes, relatos | `/triagem` (P1b) | issue com rótulo; bug com cenário de regressão | — (a prioridade é decidida no G1) |

`/marco <id>` (P1b) encadeia 2 → 5 de um marco e para em cada portão.

### 2.1 Planejamento
`/roadmap` lê as issues abertas, o `docs/estado.md` e `docs/estrategia-produto-acordu.md` e propõe até três candidatos a próximo marco, com tamanho estimado em PRs, dependências e riscos. Não cria nada: o Daniel escolhe, e o comando abre ou rotula a issue do marco.

### 2.2 Requisitos
`/spec` segue o template `docs/specs/_template/spec.md` (P1a). Cenários **dado / quando / então**, numerados `<marco>-NN`, um comportamento por cenário, com permissão, isolamento entre tenants e clientes e erro sempre que se aplicam. Requisito que falta na ERS entra na ERS no mesmo PR.

### 2.3 Design e arquitetura
`/design` escreve o `design.md`: decisões D com opções e recomendação justificada, impacto em cada documento ("nenhum" também é resposta), divisão em PRs pequenos e independentes com os cenários de cada um, e o registro. O agente `arquiteto` confere contra os ADRs e diz que D merece ADR. `/plano` revisa spec e design juntos antes de abrir o PR.

### 2.4 Desenvolvimento
`/tarefas` implementa o próximo PR da divisão: branch a partir da `main`, Domain → Application → Infrastructure → Api → web, testes de cada cenário, documentos no mesmo PR, commits em Conventional Commits por camada. Cenário que se mostra errado: o agente para e propõe a mudança na spec (portão G2 de novo, só para aquela mudança).

### 2.5 Testes e revisão
O `testador` procura cenário sem teste que verifique o "então", isolamento, `If-Match`, casos de borda das checklists dos documentos, e escreve o que falta. O `revisor-spec` revisa a branch (não edita). O agente aplica os achados, preenche a coluna Teste da spec e o registro do design, e o `/pr` abre o PR no formato de `docs/convencoes-pr-acordu.md` (P1a). CI verde é condição para pedir revisão.

### 2.6 Implantação
`/deploy` confere o `CI ok` da `main`, mostra o que entra desde o último deploy e, só com o "pode" do Daniel, dispara o `deploy.yml`, acompanha, e roda as conferências pendentes das specs (verificações manuais marcadas "no primeiro deploy"). Falha: o workflow já faz rollback; o comando relata e abre issue `bug`.

### 2.7 Manutenção
`/triagem` recebe um aviso colado do Discord, um id do Error Reporting (`gcloud`, quando logado) ou um relato, e:
- procura issue igual antes de criar outra;
- bug: issue `bug` com o cenário de regressão (dado / quando / então); o PR de correção cita `Closes #N`, cobre o cenário com teste e não precisa de spec;
- sugestão ou melhoria pequena: issue `sugestao` ou `melhoria`;
- algo do tamanho de um marco: issue `marco`, que vai para o G1.

## 3. Portões

| Portão | O Daniel… | Sem ele o agente… |
|---|---|---|
| G1 | escolhe o próximo marco | não começa spec |
| G2 | aprova spec e design (merge do PR) | não escreve código do marco |
| G3 | escolhe cada decisão D (no PR do design ou no chat) | segue a recomendação só se o Daniel disser "seguindo as recomendações" |
| G4 | revisa e faz merge de cada PR | não faz merge nem empilha PRs |
| G5 | autoriza cada deploy | não dispara o deploy |

Sempre param o agente, mesmo entre portões: qualquer coisa irreversível (apagar branch remota, dado, secret, recurso de nuvem), mudança em ADR aceito, dúvida sobre a origem de algo (regra do ASMTasks no `CLAUDE.md`), e decisão que o design não previu.

## 4. Autonomia

Nível atual: **um PR por vez** (ADR 0013, D3 do P1).

- Entre G2 e G4, o agente implementa, testa, revisa, aplica os achados e **abre o PR sem perguntar**: o PR é a revisão.
- Depois do merge, o próximo PR da divisão pode começar no mesmo comando (`/marco`), sem novo pedido.
- Não abre dois PRs do mesmo marco ao mesmo tempo. PRs independentes em paralelo e agente na nuvem disparado por issue são os próximos níveis, cada um por ADR.

## 5. Backlog (GitHub Issues)

Rótulos (criados no P1b):

| Rótulo | Uso |
|---|---|
| `marco` | trabalho que pede spec; entra no G1 |
| `bug` | comportamento errado; leva cenário de regressão |
| `melhoria` | mudança pequena sem spec (um PR) |
| `sugestao` | veio de #sugestoes ou de quem usa |
| `producao` | veio de #alertas ou do Error Reporting |
| `triagem` | ainda não classificado |

Issue de marco fechada pelo último PR do marco. Issue de bug fechada pelo PR da correção.

## 6. Rastreabilidade

Issue → RF/RNF (ERS) → cenário (`spec.md`) → decisão (`design.md` / ADR) → PR (descrição cita cenários e issue) → teste (coluna Teste) → deploy (registro do design e aviso no #deploy).

Cenário conta como feito quando um teste automatizado verifica o "então" ou, se a verificação é manual, quando a coluna Teste diz como e quando foi conferido.

## 7. Estado do projeto

`docs/estado.md` (P1a) tem só: o que está em revisão, o marco em andamento, os próximos passos, limitações do ambiente. Curto: o histórico está no git e nos designs. Cada PR atualiza o estado no próprio PR.
