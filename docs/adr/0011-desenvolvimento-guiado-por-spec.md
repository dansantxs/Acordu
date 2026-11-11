# ADR 0011 — Desenvolvimento guiado por spec (SDD) por marco

- **Status:** Substituído pelo ADR 0013 (11/11/2026)
- **Data:** 28/10/2026
- **Contexto do projeto:** Acordu (a partir do F8 e da v2)

## Contexto

O Acordu já é especificado antes de ser codificado:
- a ERS traz os requisitos numerados;
- o contrato da API e o OpenAPI versionado definem a interface, conferidos por teste;
- o modelo de domínio e o read side definem as regras;
- os ADRs e as convenções valem para toda mudança.

Falta o nível da **funcionalidade**. Hoje o plano de cada marco (escopo, decisões, divisão em PRs) é apresentado e aprovado no chat e só sobrevive como resumo no `estado-implementacao.md`, que fica fora do repositório. A ERS marca requisitos como "verificado por teste" sem dizer qual teste. A implementação também passa a rodar no Claude Code local, que lê o repositório, e não o Projeto do claude.ai.

## Decisão

1. **Cada marco nasce de uma spec versionada** em `docs/specs/<marco>/spec.md` (ex.: `docs/specs/F8-deploy/spec.md`), a partir de `docs/specs/_template.md`. A spec tem:
   - objetivo;
   - RFs e RNFs afetados;
   - cenários de aceite no formato **dado / quando / então**, numerados (`F8-01`, `F8-02`…);
   - fora de escopo;
   - decisões que dependem do Daniel, com opções e recomendação;
   - divisão em PRs.
2. **A aprovação do plano é a revisão do PR da spec.** Esse PR só tem a spec (e, se for o caso, as mudanças na ERS, no contrato ou num ADR que ela exige). Os PRs de código vêm depois, um de cada vez, contra a `main`.
3. **Rastreabilidade:** cada cenário cita o teste que o cobre (`Projeto/Arquivo › Metodo`) assim que o teste existe. PR de código cita na descrição os cenários que entrega. Um cenário só conta como feito quando um teste automatizado o cobre ou, se a verificação for manual (deploy, e-mail real), quando a spec diz como foi conferido.
4. **Spec viva:** mudou o comportamento, a spec muda no mesmo PR, como já acontece com o contrato da API. Ao fim do marco, a spec fica com status `Concluída` e continua como documentação.
5. **Ferramenta no repositório, sem dependência externa:**
   - `CLAUDE.md` na raiz com as regras do projeto (papéis, PRs, ADRs, commits, a regra do ASMTasks);
   - comandos do Claude Code em `.claude/commands/`: `/spec` (escreve a spec do marco), `/plano` (confere a spec contra ERS, ADRs e contrato e lista o que falta decidir) e `/tarefas` (implementa o próximo PR da divisão);
   - subagente `revisor-spec` em `.claude/agents/`, que confere um PR contra os cenários, os ADRs e as convenções antes de abri-lo;
   - template de PR com o checklist, incluindo a regra do ASMTasks.
6. **Não retroativo:** o que já está na `main` (backend do v1 e F1–F7) não ganha spec. O primeiro marco no formato novo é o F8.

## Alternativas consideradas

- **Spec Kit do GitHub (ou ferramenta parecida):** traz estrutura e comandos próprios que duplicariam a ERS e os ADRs. Os comandos em `.claude/commands/` fazem o mesmo dentro das convenções que já existem.
- **Continuar com o plano no chat:** funciona, mas a decisão fica fora do repositório e depende de resumo manual.
- **Specs retroativas do v1:** custo alto para pouco ganho; o código e os testes já existem.
- **Subagente que vigia a regra do ASMTasks:** precisaria de uma referência do ASMTasks para comparar, e trazê-la para o repositório já violaria a regra. Ficam a regra no `CLAUDE.md` e o item no checklist do PR.

## Consequências

**Positivas**
- O plano aprovado fica no repositório, revisável e com histórico.
- Qualquer sessão (local, nuvem, outro modelo) começa do mesmo contexto: `CLAUDE.md` + spec do marco.
- Requisito → cenário → teste fica explícito; lacunas de teste aparecem na spec.

**Negativas**
- Um PR a mais por marco (o da spec) e o trabalho de manter os cenários em dia.
- O `estado-implementacao.md` do Projeto continua existindo, mas só como resumo: a fonte é o repositório.
