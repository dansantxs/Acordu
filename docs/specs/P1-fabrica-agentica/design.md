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

Decididas pelo Daniel em 11/11, depois da comparação com a fábrica do curso (`andremenegassi/fabrica-software-agentica`):

4. **D4 — Orquestrador com subagentes por papel,** no lugar de comandos soltos. `/marco` é o orquestrador; analista, arquiteto, desenvolvedor, testador e revisor são subagentes. Só o orquestrador faz commit, push e PR.
5. **D5 — Contrato e verificação no disco:** bloco `STATUS` no retorno; o orquestrador roda os testes e confere os arquivos; 2 devoluções por etapa e 5 rodadas por PR, depois `BLOCKED`; `estado.json` por marco.
6. **D6 — Garantias pela ferramenta:** `tools:` nos agentes e negações no `.claude/settings.json`; revisor e testador com modelo diferente do desenvolvedor.
7. **D7 — Quatro portões:** a escolha das D entra no G2 (merge do PR de spec e design), como já acontecia. G1 marco, G2 spec e design, G3 merge, G4 deploy.

Diferenças mantidas em relação à fábrica do curso: o ciclo inteiro (planejamento, deploy e manutenção também), sem Spec Kit (duplicaria ERS e ADRs), marco com vários PRs (a divisão no design substitui o `tasks.md`) e Claude Code no lugar do opencode.

Decisões de implementação (sem portão):

- **Marco de processo usa o prefixo `P`** (P1), para não disputar numeração com os marcos do produto (F14 em diante).
- **`/plano` continua**, revisando spec e design juntos antes do G2 (é a análise de consistência).
- **`/tarefas` continua** para rodar um PR à mão, fora do orquestrador; por dentro, ele aciona o mesmo agente `desenvolvedor`.
- **Modelos:** desenvolvedor e arquiteto no modelo da sessão; revisor e testador fixam outro (`model: sonnet` se a sessão for Opus). A diversidade é menor que entre fornecedores, mas reduz pontos cegos em comum.
- **`estado.json` versionado** na pasta do marco (o orquestrador faz commit dele na branch), para a retomada funcionar em outra máquina.
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
- **`.claude/settings.json` (novo):** negações do processo §8 — P1b.
- **ERS, contrato, domínio, read side, segurança:** nenhum.

## 3. Divisão em PRs

Cada PR contra a `main`, sem empilhar.

| PR | Entrega | Cenários |
|---|---|---|
| P1 | Este: processo, ADR 0013, spec e design do P1 | P1-02 |
| P1a | Unificação (inclui a convenção de PR pedida pelo Daniel): `CLAUDE.md`, README das specs, convenções §6, templates de spec e design, `/spec` e `/plano` atualizados, `/design`, convenção de PR + template + `/pr`, `docs/estado.md`, `scripts/conferir-specs.mjs` na CI | P1-01, P1-03 a P1-10 |
| P1b | Orquestrador `/marco` e subagentes (`analista`, `arquiteto`, `desenvolvedor`, `testador`; `revisor-spec` lendo o design e com o bloco de status), `estado.json`, `.claude/settings.json` com as negações | P1-11 a P1-25 |
| P1c | `/roadmap`, `/deploy`, `/triagem`, `scripts/rotulos.sh` | P1-26 a P1-33 |

## 4. Registro

- 11/11 — o Daniel pediu para unificar o SDD (processo espalhado entre ADR e spec) e montar uma fábrica agêntica semi-autônoma sobre as sete etapas do ciclo; aprovou as recomendações D1 a D3. Spec, design, ADR 0013 e processo no PR do P1.
- 11/11 — o Daniel aprovou adotar da fábrica do curso o orquestrador com subagentes, o contrato de status com verificação no disco, os limites de devolução, o `estado.json` e as garantias por permissão (D4 a D7); os portões ficam quatro. P1b dividido em P1b (orquestrador) e P1c (comandos fora do marco). Processo e ADR 0013 emendados.
- 11/11 — P1a: `CLAUDE.md` com a seção "Como trabalhar" (ponteiros e o essencial dos portões); README das specs e convenções §6 só apontam para o processo; templates em `_template/spec.md` e `design.md` (cenários agrupados por PR na spec); `/design` novo, `/spec` só o quê, `/plano` revisando os dois, `/tarefas` abrindo o PR com o `/pr` sem perguntar; `docs/convencoes-pr-acordu.md` tirada dos PRs #156, #164, #171 e #174, com o template de PR alinhado; `docs/estado.md` a partir do documento do Projeto, só com o que segue valendo; `scripts/conferir-specs.mjs` (Node sem dependência, F8–F13 como legado sem `design.md`) num job `Specs` com filtro próprio, dentro do `CI ok`.
  Achados do `revisor-spec` aplicados: comandos e `CLAUDE.md` citam a seção do processo em vez de repetir a regra (P1-01 reescrito para isso); o título das tabelas da spec pode nomear o PR (processo §3.2); o script trata como cenário toda linha de tabela com coluna Id; checklist do PR com uma regra só; alternativa ao `gh pr checks` sem GraphQL; F11 marcado Concluída (estava Em implementação com todos os testes).
