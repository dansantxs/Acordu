# Spec — <marco>: <título>

| | |
|---|---|
| **Status** | Rascunho · Aprovada · Em implementação · Concluída |
| **Data** | DD/MM/AAAA |
| **Requisitos** | RF-…, RNF-… (ERS) |
| **ADRs** | 00xx |

## 1. Objetivo

Uma ou duas frases: o que o usuário ganha ao fim deste marco.

## 2. Cenários de aceite

Numerados `<marco>-NN`. Um comportamento por cenário. A coluna **Teste** é preenchida quando o teste existir (`Projeto/Arquivo › Metodo`) ou descreve a verificação manual.

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| <marco>-01 | … | … | … | — |

## 3. Fora de escopo

- …

## 4. Decisões que dependem do Daniel

Para cada uma: opções, recomendação e por quê. Depois de decidido, registrar a escolha e a data aqui (e em ADR, se for de arquitetura).

1. **…**
   - (a) … — **recomendado**, porque …
   - (b) …

## 5. Impacto nos documentos

ERS, contrato da API, modelo de domínio, read side, segurança, convenções, ADR novo. "Nenhum" também é resposta.

## 6. Divisão em PRs

Cada PR contra a `main`, sem empilhar, com testes e CI verde.

| PR | Entrega | Cenários |
|---|---|---|
| <marco>a | … | <marco>-01, <marco>-02 |

## 7. Registro

- DD/MM — spec aprovada (PR #…).
