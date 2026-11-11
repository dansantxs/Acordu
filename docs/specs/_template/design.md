# Design — <marco>: <título>

Spec: [spec.md](spec.md). Processo: `docs/processo.md` §3.3.

## 1. Decisões

Para cada uma: opções, recomendação e por quê. O G2 (merge deste PR) registra a escolha; anote aqui quem decidiu e quando. Decisão que outro marco terá de seguir vira ADR ou convenção no mesmo PR e fica só citada aqui.

1. **D1 — …**
   - (a) … — **recomendado**, porque …
   - (b) …

Decisões de implementação (sem portão):

- …

## 2. Impacto nos documentos

ERS, contrato da API e OpenAPI, modelo de domínio, read side, segurança, deploy e CI, convenções, ADR novo. "Nenhum" também é resposta.

- …

## 3. Divisão em PRs

Cada PR contra a `main`, sem empilhar, com testes e CI verde. Diga quais dependem de quais.

| PR | Entrega | Cenários |
|---|---|---|
| <marco>a | … | <marco>-01, <marco>-02 |

## 4. Registro

Uma linha por PR: o que entrou e as decisões não óbvias que surgiram na implementação.

- DD/MM — rascunho.
