# Design — P1: Fábrica agêntica semi-autônoma

Spec: [spec.md](spec.md). Processo: [docs/processo.md](../../processo.md). ADR: 0013.

## 1. Decisões

Decididas pelo Daniel em 11/11 (todas as recomendações).

1. **D1 — Spec e design separados.**
   - (a) **Escolhida:** `spec.md` (o quê) e `design.md` (como). Requisito e design são revisados lado a lado, e a decisão que vale além do marco sobe para ADR.
   - (b) Um arquivo só: mais simples, mas é a mistura de hoje.
2. **D2 — Backlog.**
   - (a) **Escolhida:** GitHub Issues com rótulos (processo §5). `/roadmap` e `/triagem` usam o `gh`; PR fecha issue com `Closes #N`.
   - (b) `docs/backlog.md`: versionado, sem rótulo nem ligação com PR.
3. **D3 — Autonomia.**
   - (a) **Escolhida:** um PR por vez; o agente abre o PR sem perguntar e emenda o próximo depois do merge.
   - (b) PRs independentes em paralelo.
   - (c) Agente na nuvem disparado por issue: fica para ADR próprio (fora de escopo).

Decisões de implementação (sem portão):

- **Marco de processo usa o prefixo `P`** (P1), para não disputar numeração com os marcos do produto (F14 em diante).
- **`/plano` continua**, agora revisando spec e design juntos; o `arquiteto` é o agente que ele chama para a parte de ADRs.
- **`revisor-spec` mantém o nome** (já citado em PRs e no template); passa a ler também o `design.md`.
- **Conferência das specs na CI** por um script em Node (`scripts/conferir-specs.mjs`), sem dependência, num job próprio filtrado por `docs/specs/**`, dentro do `CI ok` (convenção §6 dos jobs).
- **Estado:** `docs/estado.md` nasce curto a partir do `estado-implementacao.md` do Projeto: só em revisão, marco atual, próximos passos e limitações do ambiente. O documento do Projeto passa a apontar para o repositório.
- **Rótulos** por script idempotente (`scripts/rotulos.sh`, `gh label create --force`), para não depender de clique.

## 2. Impacto nos documentos

- **ADR 0013 (novo)** e **ADR 0011** com status Substituído — este PR.
- **`docs/processo.md` (novo)** — este PR.
- **`CLAUDE.md`:** seção "Papéis e fluxo" reduzida a ponteiros para o processo; tabela com ADRs 0001–0013, processo e estado — P1a.
- **`docs/specs/README.md`:** regras saem (ficam no processo); tabela ganha a coluna Design — P1a (a linha do P1 entra neste PR).
- **Convenções §6:** o item das specs vira ponteiro para o processo — P1a.
- **`docs/convencoes-pr-acordu.md` (novo)** e `.github/pull_request_template.md` — P1a.
- **`docs/specs/_template.md`** vira `docs/specs/_template/spec.md` e `design.md` — P1a.
- **Deploy e CI:** job novo de conferência das specs — P1a.
- **ERS, contrato, domínio, read side, segurança:** nenhum.

## 3. Divisão em PRs

Cada PR contra a `main`, sem empilhar.

| PR | Entrega | Cenários |
|---|---|---|
| P1 | Este: processo, ADR 0013, spec e design do P1 | P1-02 |
| P1a | Unificação: `CLAUDE.md`, README das specs, convenções §6, templates de spec e design, `/spec` e `/plano` atualizados, `/design`, convenção de PR + template + `/pr`, `docs/estado.md`, `scripts/conferir-specs.mjs` na CI | P1-01, P1-03 a P1-10 |
| P1b | Agentes e comandos das etapas 1, 3, 5, 6 e 7: `/roadmap`, `arquiteto`, `testador`, `/deploy`, `/triagem`, `/marco`, `scripts/rotulos.sh`; `revisor-spec` lendo o design | P1-11 a P1-22 |

## 4. Registro

- 11/11 — o Daniel pediu para unificar o SDD (processo espalhado entre ADR e spec) e montar uma fábrica agêntica semi-autônoma sobre as sete etapas do ciclo; aprovou as recomendações D1 a D3. Spec, design, ADR 0013 e processo no PR do P1.
