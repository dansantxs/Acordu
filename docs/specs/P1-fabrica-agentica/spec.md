# Spec — P1: Fábrica agêntica semi-autônoma

| | |
|---|---|
| **Status** | Aprovada |
| **Data** | 11/11/2026 |
| **Requisitos** | Nenhum RF/RNF do produto (marco de processo) |
| **ADRs** | 0013 (novo; substitui o 0011) |
| **Design** | [design.md](design.md) |

## 1. Objetivo

O Daniel conduz o Acordu pelo Claude Code local decidindo só nos portões: um documento diz como trabalhar, cada etapa do ciclo tem um comando ou agente, o que chega da produção vira issue, e o estado do projeto está no repositório.

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

### Agentes e comandos das etapas (P1b)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| P1-11 | issues abertas e `docs/estado.md` | `/roadmap` | propõe até três marcos com tamanho, dependências e riscos; não cria nada até o Daniel escolher | — |
| P1-12 | o Daniel escolheu um marco | `/roadmap` continua | a issue do marco existe com rótulo `marco` | — |
| P1-13 | design em rascunho | agente `arquiteto` | lista as D que contradizem ADR ou que deveriam virar ADR, citando o trecho | — |
| P1-14 | branch com cenário sem teste do "então" | agente `testador` | escreve o teste que falta e preenche a coluna Teste; não muda código de produção | — |
| P1-15 | `main` com `CI ok` | `/deploy` | mostra os commits desde o último deploy e só dispara o `deploy.yml` depois do "pode" do Daniel | — |
| P1-16 | `main` sem `CI ok` | `/deploy` | recusa e diz o que falhou | — |
| P1-17 | aviso de erro colado do #alertas | `/triagem` | procura issue igual; se não houver, cria issue `bug` + `producao` com o cenário de regressão | — |
| P1-18 | o mesmo aviso de novo | `/triagem` | comenta na issue existente em vez de criar outra | — |
| P1-19 | sugestão colada do #sugestoes | `/triagem` | cria issue `sugestao` (ou `marco`, se for do tamanho de um marco) sem dado pessoal | — |
| P1-20 | marco com design aprovado | `/marco <id>` | encadeia `/tarefas` → `testador` → `revisor-spec` → `/pr`, para no merge (G4) e, depois dele, segue para o próximo PR da divisão | — |
| P1-21 | qualquer comando | encontra algo irreversível ou não previsto no design | para e pergunta (processo §3) | — |
| P1-22 | repositório no GitHub | script de rótulos | os rótulos do processo §5 existem; rodar de novo não duplica | — |

## 3. Fora de escopo

- Agente na nuvem disparado por issue ou rótulo (GitHub Action do Claude Code): ADR próprio, depois de confirmar custo e autenticação.
- PRs independentes do mesmo marco em paralelo.
- Ligação automática do Discord ou do Error Reporting com as issues (a API ou um Worker abrindo issue): o `/triagem` recebe o aviso colado.
- Specs retroativas e migração das specs F8–F13 para dois arquivos.
- Qualquer mudança no produto.
