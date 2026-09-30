# Convenção de Commits — Acordu

Padrão adotado: **Conventional Commits** (igual ao Saldu).

## Formato

```
tipo(escopo): assunto no imperativo, minúsculo, sem ponto final

Corpo opcional: explica O QUÊ e POR QUÊ (não o "como", que o diff mostra).
Quebrar linhas em ~72 colunas.

Rodapé opcional: refs de issue, BREAKING CHANGE.
```

- **Summary** (GitHub Desktop) = primeira linha. **Description** = corpo.

## Regras do assunto
- Imperativo: "adiciona", "corrige", "remove".
- Minúsculo, sem ponto final, até ~50 caracteres.
- Escopo opcional, recomendado.

## Quando incluir corpo
- Mudança trivial → só o Summary.
- Mudança não óbvia, decisão ou impacto amplo → corpo com o porquê.

## Tipos

| Tipo | Quando usar |
|---|---|
| `feat` | Nova funcionalidade. |
| `fix` | Correção de bug. |
| `docs` | Só documentação (ERS, ADRs, README, estes docs). |
| `test` | Testes, sem mudar produção. |
| `refactor` | Mudança interna sem alterar comportamento. |
| `perf` | Desempenho. |
| `build` | Build, dependências, empacotamento (NuGet, npm, Dockerfile). |
| `ci` | Workflows do GitHub Actions. |
| `chore` | Manutenção (config, `.gitignore`, `.editorconfig`). |
| `style` | Formatação, sem efeito em lógica. |
| `revert` | Reversão de commit. |

## Escopos (deste repositório)

`domain`, `application`, `infrastructure`, `api`, `web`, `portal`, `worker`, `docs`, `adr`, `infra`, `ci`, `deploy`.

`portal` cobre o que é exclusivo do portal do cliente, em qualquer camada (casos de uso em `Application.Portal`, endpoints `/portal`, telas `portal/`). Mudança que atravessa interno e portal usa o escopo da camada.

## Exemplos

```
feat(domain): implementa value object Duracao
feat(domain): adiciona máquina de estados da solicitação
test(domain): cobre cálculo de SLA com feriado e fim de semana
feat(infrastructure): aplica filtro global por ClienteId
feat(portal): adiciona extrato do contrato
fix(api): retorna 404 para solicitação de outro cliente
docs(adr): adiciona ADR 0005 sobre armazenamento de anexos
ci: adiciona workflow de build e testes
```

## Rodapé — casos especiais

- Referência a issue: `Refs #12` ou `Closes #12`.
- Mudança incompatível:

```
feat(api): troca horas decimais por minutos inteiros

BREAKING CHANGE: campos de tempo passam a ser inteiros em minutos.
```

## Commits gerados com o Claude

O Acordu é implementado pelo Claude e revisado pelo Daniel. O commit segue o mesmo padrão; o rodapé `Co-Authored-By` identifica a coautoria. Nada vai para `main` sem revisão (PR).
