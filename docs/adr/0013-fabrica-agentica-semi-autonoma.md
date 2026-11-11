# ADR 0013 — Fábrica agêntica semi-autônoma

- **Status:** Aceito
- **Data:** 11/11/2026
- **Substitui:** ADR 0011 (desenvolvimento guiado por spec)
- **Contexto do projeto:** Acordu (a partir do P1)

## Contexto

O ADR 0011 levou o plano de cada marco para o repositório, e as specs do F8 ao F13 funcionaram. Ficaram três problemas:

- **O processo está espalhado** em seis lugares (o próprio ADR, `CLAUDE.md`, README das specs, convenções §6, comandos e template de PR), e o ADR mistura a decisão com o procedimento, que muda com frequência.
- **A spec junta requisito e design.** Decisão de arquitetura às vezes vira ADR, às vezes fica só no registro da spec.
- **O ciclo está coberto só no meio.** Planejamento é feito no chat, implantação só pelo runbook, e o que chega da produção (#alertas, sugestões) não vira trabalho. O estado do projeto fica num documento fora do repositório que o Claude Code local não lê.

## Decisão

1. **Fábrica agêntica semi-autônoma:** as sete etapas do ciclo (planejamento, requisitos, design, desenvolvimento, testes, implantação, manutenção) têm cada uma um comando ou agente do Claude Code no repositório e um artefato de saída. O Daniel decide em cinco portões fixos (escolha do marco, aprovação de spec e design, decisões D, merge de cada PR, deploy). Entre os portões, o agente segue sem perguntar.
2. **Fonte única do procedimento:** `docs/processo.md`. Os outros documentos apontam para ele. Este ADR guarda só a decisão.
3. **Spec e design separados por marco:** `spec.md` (o quê) e `design.md` (como, com as decisões e a divisão em PRs). Uma decisão que vale além do marco é promovida a ADR ou convenção.
4. **Backlog em GitHub Issues** com rótulos; as etapas 1 e 7 leem e escrevem com o `gh`.
5. **Autonomia de um PR por vez:** o agente abre o PR sem perguntar e pode emendar o próximo depois do merge; não abre PRs do mesmo marco em paralelo. Subir de nível (paralelo, agente na nuvem) exige ADR.
6. **Estado no repositório:** `docs/estado.md`, curto, atualizado em cada PR.
7. **Não retroativo:** as specs F8–F13 ficam como estão.

## Alternativas consideradas

- **Manter o ADR 0011 e só acrescentar comandos:** mantém o procedimento dentro de um ADR e a mistura de requisito e design.
- **Spec e design num arquivo só:** mais simples, mas é o problema de hoje.
- **Backlog em `docs/backlog.md`:** versionado, mas sem rótulo, busca nem ligação com PR (`Closes #N`).
- **PRs independentes em paralelo:** mais rápido, mas o Daniel revisa um por vez e conflitos ficam mais prováveis.
- **Agente na nuvem disparado por issue (GitHub Action do Claude Code):** autonomia maior; falta confirmar custo e autenticação com a assinatura. Fica para ADR próprio.
- **Ferramenta externa de SDD (Spec Kit):** duplicaria ERS e ADRs (motivo já dado no 0011).

## Consequências

**Positivas**
- Um lugar para saber como trabalhar; ADRs voltam a guardar só arquitetura.
- Requisito e design revisáveis separadamente, no mesmo PR.
- Produção vira trabalho rastreável; bug ganha cenário de regressão sem custo de spec.
- Qualquer sessão começa do mesmo contexto: `CLAUDE.md`, `docs/processo.md`, `docs/estado.md` e a pasta do marco.

**Negativas**
- Um arquivo a mais por marco e mais comandos para manter.
- O backlog fica fora do git (no GitHub), dependente do `gh`.
- O documento de estado do Projeto do claude.ai deixa de ser a fonte; precisa só apontar para o repositório.
