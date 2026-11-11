# Convenção de Pull Requests — Acordu

Como escrever o título e a descrição de um PR. O fluxo (quando abrir, portões, revisão) está em `docs/processo.md`. O comando `/pr` do Claude Code aplica esta convenção.

## Título

O assunto em Conventional Commits (`docs/convencoes-commit-acordu.md`), com o PR da divisão entre parênteses no fim:

```
feat(infra): vigia da API em #alertas no lugar do UptimeRobot (F13h)
feat: aviso de cronômetro esquecido (F12e)
docs: spec do F14, situação da empresa
fix(web): chave repetida dos primeiros passos (Closes #42)
```

- O tipo segue a camada que mais pesa; com várias camadas, sem escopo.
- PR de spec e design: `docs: spec do <marco>, <título curto>`.
- PR de bug: termina com `(Closes #N)` em vez do marco.

## Descrição

Três seções, nesta ordem, e o rodapé. Nada antes da primeira seção.

### O que entrega

- **Primeira frase fixa:** `O PR <id> da spec \`docs/specs/<marco>/spec.md\`: <o que é>, cenários <marco>-NN a <marco>-NN.` Se for o último do marco, acrescente que a spec passa a Concluída. Bug: `Corrige #N: <o comportamento errado>`, e o cenário de regressão.
- Depois, **um item por camada ou área**, com o rótulo em negrito e dois-pontos: `**Domain:**`, `**Application:**`, `**Infrastructure:**`, `**Api:**`, `**Web:**`, `**CI:**`, `**Deploy:**`. Cada item diz o que existe agora que não existia: nome do tipo, da rota ou da tela em `código`, e a regra em uma frase.
- Cenários entre parênteses quando o item cobre só alguns: `**No app (F12-17):** …`.
- Último item: **Documentos:** a lista dos que mudaram (ERS §, contrato §, modelo §, segurança §, convenções, spec e design), e o que o bot gerou (OpenAPI, tipos, migration, capturas).

### Decisões não óbvias

O que o revisor precisa saber para não estranhar o diff. Cada item:

- começa com **a decisão em negrito, terminada em ponto ou dois-pontos**, e depois diz o porquê em uma ou duas frases;
- cita o nome no código (`TarefasAbertas`, `xmin`) quando ajuda a achar no diff.

Itens com nome fixo, quando existem, nesta ordem no fim da seção:

- **Para o Daniel decidir:** desvio da spec ou do design, com o que era e o que ficou, e "se preferir o original, eu ajusto".
- **Limitação aceita:** o que fica de fora ou pode falhar, e por que é aceitável.

### Checklist

```
- [x] Nada do ASMTasks
- [x] Contra a `main`, sem depender de outro PR aberto
- [x] Testes: `ClasseTests` (o que cobre), `OutraTests` (…); comandos rodados
- [x] Documentos no mesmo PR
- [x] `revisor-spec`. Apliquei os achados:
  - …;
  - ….

  Ficou como estava: … (o motivo).
- [x] CI verde
```

- Testes pelo nome da classe, com o que cobrem entre parênteses.
- Achados do revisor (e ressalvas do `aprovado com ressalvas`) um por linha, em frase curta; o que não foi aplicado vai em "Ficou como estava", com o motivo.
- Sempre começa por "Nada do ASMTasks" e "Contra a `main`, sem depender de outro PR aberto", como no template. PR só de documentos troca o item de testes por "Specs ok" quando mexe em `docs/specs/`; o revisor roda em todo PR.

### Rodapé

A linha de geração definida pelo ambiente (no Claude Code, a que ele acrescenta) e o link da sessão, quando houver. Nada depois.

## Escrita

- Português, frases curtas, voz ativa, presente ("a rota devolve", não "foi implementada uma rota que irá devolver").
- Diga o que mudou para quem usa ou para o sistema, não o que o diff já mostra linha a linha.
- Lista aninhada só a partir de três subitens; abaixo disso, frase corrida.
- Ponto e vírgula no fim de itens de uma lista aninhada, ponto no último.
- Sem emoji, sem títulos além dos três, sem "Resumo" no fim.
- Termos do Acordu como estão no código e nos documentos (`TenantId`, `If-Match`, `RESOLVIDA`), em `código`.
- Cite a decisão do Daniel com a data quando o PR depende dela (`decidido em 03/11`).

## Exemplo curto

```markdown
## O que entrega

O PR F12e da spec `docs/specs/F12-cronometro-notificacoes-app/spec.md`: aviso de cronômetro esquecido, cenários F12-17 a F12-19.

- **Domain:** `CronometroEsquecido` diz que o cronômetro foi esquecido 30 min depois do fim do expediente do dia em que começou, ou com 10 h, o que vier primeiro.
- **Api:** `GET /cronometro` traz `esquecido` e `sugestaoMinutos`.
- **Web (F12-17):** o indicador mostra "Ficou ligado?" e a janela de parar já vem com a duração sugerida.
- **Documentos:** contrato §9, spec e design. OpenAPI pelo bot.

## Decisões não óbvias

- **Sem migration.** O "uma vez por cronômetro" usa a tabela de marcadores de aviso, como os alertas de SLA.
- **Para o Daniel decidir:** a D5 pedia a coluna `AvisadoEsquecidoEm`; ficou a tabela de marcadores. Se preferir a coluna, eu ajusto.

## Checklist

- [x] Nada do ASMTasks
- [x] Contra a `main`, sem depender de outro PR aberto
- [x] Testes: `CronometroEsquecidoTests` (regra), `CronometroTests` (GET e tick, outra empresa)
- [x] Documentos no mesmo PR
- [x] `revisor-spec`. Apliquei os achados:
  - a sugestão só vem quando o fim do expediente já passou;
  - o sino é conferido por tenant.
- [x] CI verde
```
