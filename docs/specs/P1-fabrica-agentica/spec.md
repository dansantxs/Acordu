# Spec — P1: Fábrica agêntica semi-autônoma

| | |
|---|---|
| **Status** | Aprovada |
| **Data** | 11/11/2026 |
| **Requisitos** | Nenhum RF/RNF do produto (marco de processo) |
| **ADRs** | 0013 (novo; substitui o 0011) |
| **Design** | [design.md](design.md) |

## 1. Objetivo

O Daniel conduz o Acordu pelo Claude Code local decidindo só nos portões: um documento diz como trabalhar, um orquestrador leva cada marco da spec ao PR com subagentes por papel e confere tudo no disco, o que chega da produção vira issue, e o estado do projeto está no repositório.

## 2. Cenários de aceite

A maior parte do marco é texto de processo e comandos do Claude Code: a verificação é a execução do comando num caso real, descrita na coluna Teste. A estrutura das specs é conferida pela CI (P1-06 a P1-08).

### Fonte única e unificação (P1a)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| P1-01 | o repositório na `main` | se procura uma regra do processo (portões, onde fica cada decisão, formato de spec e design) | ela está só em `docs/processo.md`; `CLAUDE.md`, `docs/specs/README.md`, convenções §6 e os comandos apontam para lá sem repetir | — |
| P1-02 | ADR 0011 | se lê o ADR | o status é "Substituído pelo ADR 0013" e o texto não muda | este PR |
| P1-03 | marco novo | `/spec <marco>` | cria a pasta com `spec.md` (só o quê) a partir de `docs/specs/_template/spec.md` e a linha no README | — |
| P1-04 | spec em rascunho | `/design <marco>` | cria `design.md` a partir de `docs/specs/_template/design.md` com decisões D (opções e recomendação), impacto, divisão em PRs e registro | — |
| P1-05 | decisão D que outro marco terá de seguir | o design é escrito | a D aponta para um ADR ou convenção criado no mesmo PR | — |
| P1-06 | pasta de marco P1 ou posterior sem `design.md` | a CI roda | o job falha dizendo qual pasta | — |
| P1-07 | spec com Id de cenário repetido ou fora do padrão `<marco>-NN` | a CI roda | o job falha com o Id | — |
| P1-08 | spec com status `Concluída` e cenário com Teste `—` | a CI roda | o job falha com o cenário | — |
| P1-09 | branch pronta | `/pr` | a descrição segue `docs/convencoes-pr-acordu.md` (título, O que entrega, Decisões não óbvias, Checklist, rodapé) e o PR é aberto contra a `main` sem perguntar | — |
| P1-10 | qualquer PR mesclado | se abre `docs/estado.md` | ele diz o que está em revisão, o marco em andamento e os próximos passos, atualizado pelo próprio PR | — |

### Orquestrador e subagentes (P1b)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| P1-11 | marco com spec e design aprovados (G2) | `/marco <id>` | aciona desenvolvedor → testador → revisor, um de cada vez, com o handoff do processo §5, e abre o PR pelo `/pr` | — |
| P1-12 | um subagente devolve `DONE` com um arquivo em `ARTEFATOS` que não mudou | o orquestrador confere | a etapa não avança e conta como devolução | — |
| P1-13 | o desenvolvedor diz que os testes passaram | o orquestrador confere | ele mesmo roda os comandos de `TESTES`; código de saída ≠ 0 devolve ao desenvolvedor | — |
| P1-14 | o revisor dá `reprovado` ou o testador acha defeito | a etapa termina | o trabalho volta ao desenvolvedor e passa de novo pelo testador e pelo revisor | — |
| P1-15 | terceira devolução na mesma etapa, ou sexta rodada no PR | acontece | `estado.json` fica `BLOCKED` e o orquestrador para explicando ao Daniel | — |
| P1-16 | `aprovado com ressalvas` | o PR é aberto | as ressalvas estão na descrição do PR | — |
| P1-17 | branch atual diferente da do PR | o orquestrador vai acionar um subagente | troca para a branch e confere com `git branch --show-current` antes; trabalho na `main` reprova a etapa | — |
| P1-18 | sessão nova com `estado.json` em `IMPLEMENTANDO` | `/marco <id>` | continua da etapa pendente, sem refazer as anteriores | — |
| P1-19 | `estado.json` em `AGUARDANDO_MERGE` | `/marco <id>` | confere com `gh pr view`; mesclado → próximo PR da divisão; não mesclado → para | — |
| P1-20 | qualquer agente | tenta `gh pr merge`, `git push --force`, `git reset --hard`, `git rebase` ou push na `main` | a permissão do `.claude/settings.json` nega | — |
| P1-21 | um subagente | tenta `git commit`, `git push` ou `gh` | não tem a ferramenta ou a permissão nega | — |
| P1-22 | revisor e testador | são acionados | usam um modelo diferente do desenvolvedor (`model:` no agente) | — |
| P1-23 | dúvida de requisito durante a implementação | o subagente devolve `NEEDS_CLARIFICATION` | o orquestrador para e leva ao Daniel; a mudança volta ao G2 | — |
| P1-24 | qualquer etapa | encontra algo irreversível ou não previsto no design | para e pergunta (processo §4) | — |
| P1-25 | marco novo | `/marco <id>` sem spec | aciona o analista e depois o arquiteto, roda o `/plano`, abre o PR de spec e design e para no G2 | — |

### Comandos fora do marco (P1c)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| P1-26 | issues abertas e `docs/estado.md` | `/roadmap` | propõe até três marcos com tamanho, dependências e riscos; não cria nada até o Daniel escolher | — |
| P1-27 | o Daniel escolheu um marco | `/roadmap` continua | a issue do marco existe com rótulo `marco` | — |
| P1-28 | `main` com `CI ok` | `/deploy` | mostra os commits desde o último deploy e só dispara o `deploy.yml` depois do "pode" do Daniel | — |
| P1-29 | `main` sem `CI ok` | `/deploy` | recusa e diz o que falhou | — |
| P1-30 | aviso de erro colado do #alertas | `/triagem` | procura issue igual; se não houver, cria issue `bug` + `producao` com o cenário de regressão | — |
| P1-31 | o mesmo aviso de novo | `/triagem` | comenta na issue existente em vez de criar outra | — |
| P1-32 | sugestão colada do #sugestoes | `/triagem` | cria issue `sugestao` (ou `marco`, se for do tamanho de um marco) sem dado pessoal | — |
| P1-33 | repositório no GitHub | script de rótulos | os rótulos do processo §7 existem; rodar de novo não duplica | — |

## 3. Fora de escopo

- Agente na nuvem disparado por issue ou rótulo (GitHub Action do Claude Code): ADR próprio, depois de confirmar custo e autenticação.
- PRs independentes do mesmo marco em paralelo.
- Ligação automática do Discord ou do Error Reporting com as issues (a API ou um Worker abrindo issue): o `/triagem` recebe o aviso colado.
- Specs retroativas e migração das specs F8–F13 para dois arquivos.
- Qualquer mudança no produto.
