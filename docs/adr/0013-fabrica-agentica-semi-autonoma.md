# ADR 0013 — Fábrica agêntica semi-autônoma

- **Status:** Aceito (emendado em 11/11/2026: orquestrador, verificação no disco e permissões)
- **Data:** 11/11/2026
- **Substitui:** ADR 0011 (desenvolvimento guiado por spec)
- **Contexto do projeto:** Acordu (a partir do P1)

## Contexto

O ADR 0011 levou o plano de cada marco para o repositório, e as specs do F8 ao F13 funcionaram. Ficaram três problemas:

- **O processo está espalhado** em seis lugares (o próprio ADR, `CLAUDE.md`, README das specs, convenções §6, comandos e template de PR), e o ADR mistura a decisão com o procedimento, que muda com frequência.
- **A spec junta requisito e design.** Decisão de arquitetura às vezes vira ADR, às vezes fica só no registro da spec.
- **O ciclo está coberto só no meio.** Planejamento é feito no chat, implantação só pelo runbook, e o que chega da produção (#alertas, sugestões) não vira trabalho. O estado do projeto fica num documento fora do repositório que o Claude Code local não lê.

## Decisão

1. **Fábrica agêntica semi-autônoma:** as sete etapas do ciclo (planejamento, requisitos, design, desenvolvimento, testes, implantação, manutenção) têm cada uma um agente ou comando do Claude Code no repositório e um artefato de saída. Um **orquestrador** conduz o marco e aciona **subagentes por papel** (analista, arquiteto, desenvolvedor, testador, revisor), um de cada vez; só ele faz commit, push e abre PR. O Daniel decide em quatro portões fixos (escolha do marco; spec e design com as decisões D; merge de cada PR; deploy). Entre os portões, o orquestrador segue sem perguntar.
2. **Verificação no disco:** cada subagente devolve um bloco de status; o orquestrador confere os arquivos e roda ele mesmo os testes antes de avançar. Devoluções têm limite (2 por etapa, 5 rodadas por PR); passou disso, o trabalho para e volta ao Daniel. O estado de cada marco fica num `estado.json` retomável.
3. **Garantias pela ferramenta:** o que não pode acontecer (merge, force push, deploy sem autorização) é negado nas permissões do Claude Code, não só no prompt; quem verifica usa um modelo diferente de quem escreve.
4. **Fonte única do procedimento:** `docs/processo.md`. Os outros documentos apontam para ele. Este ADR guarda só a decisão.
5. **Spec e design separados por marco:** `spec.md` (o quê) e `design.md` (como, com as decisões e a divisão em PRs). Uma decisão que vale além do marco é promovida a ADR ou convenção.
6. **Backlog em GitHub Issues** com rótulos; as etapas 1 e 7 leem e escrevem com o `gh`.
7. **Autonomia de um PR por vez:** o agente abre o PR sem perguntar e pode emendar o próximo depois do merge; não abre PRs do mesmo marco em paralelo. Subir de nível (paralelo, agente na nuvem) exige ADR.
8. **Estado no repositório:** `docs/estado.md`, curto, atualizado em cada PR, para a visão geral.
9. **Não retroativo:** as specs F8–F13 ficam como estão.

## Alternativas consideradas

- **Manter o ADR 0011 e só acrescentar comandos:** mantém o procedimento dentro de um ADR e a mistura de requisito e design.
- **Spec e design num arquivo só:** mais simples, mas é o problema de hoje.
- **Backlog em `docs/backlog.md`:** versionado, mas sem rótulo, busca nem ligação com PR (`Closes #N`).
- **PRs independentes em paralelo:** mais rápido, mas o Daniel revisa um por vez e conflitos ficam mais prováveis.
- **Agente na nuvem disparado por issue (GitHub Action do Claude Code):** autonomia maior; falta confirmar custo e autenticação com a assinatura. Fica para ADR próprio.
- **Ferramenta externa de SDD (Spec Kit):** duplicaria ERS e ADRs (motivo já dado no 0011). A estrutura de orquestrador, subagentes, bloco de status, limites e estado retomável segue o modelo de fábrica do curso (opencode + Spec Kit), adaptado: Claude Code, marco com vários PRs, ciclo inteiro em vez de spec → PR.
- **Comandos soltos por etapa, sem orquestrador** (primeira versão deste ADR, PR #175): cada etapa confiaria no relato do agente e não haveria estado retomável nem limite de devoluções.

## Consequências

**Positivas**
- Um lugar para saber como trabalhar; ADRs voltam a guardar só arquitetura.
- Requisito e design revisáveis separadamente, no mesmo PR.
- Produção vira trabalho rastreável; bug ganha cenário de regressão sem custo de spec.
- Qualquer sessão começa do mesmo contexto: `CLAUDE.md`, `docs/processo.md`, `docs/estado.md` e a pasta do marco.

**Negativas**
- Dois arquivos a mais por marco (design e `estado.json`) e mais agentes para manter.
- O backlog fica fora do git (no GitHub), dependente do `gh`.
- O documento de estado do Projeto do claude.ai deixa de ser a fonte; precisa só apontar para o repositório.
