# ADR 0004 — Tempo como `Duracao` em minutos inteiros; consumo de contrato como read model

- **Status:** Aceito
- **Data:** 30/09/2026
- **Contexto do projeto:** Acordu (v1)

## Contexto

No Acordu, **tempo é o dinheiro**: horas apontadas definem o consumo do contrato, o saldo do banco de horas e, no futuro, a cobrança. É o mesmo papel que o `Dinheiro` tem no Saldu, e os mesmos riscos existem: representação em ponto flutuante acumula erro (1,1h + 2,2h ≠ 3,3h em `double`), e arredondamentos espalhados pelo código geram extratos que não batem.

Também é preciso decidir onde vive o "quanto já foi consumido": como número guardado no contrato, ou calculado a partir dos apontamentos.

## Decisão

1. **Value object `Duracao`** com `Minutos: int` (≥ 0). Somas e subtrações retornam nova instância. Conversão para "h:mm" ou horas decimais **só na apresentação**. `DuracaoAssinada` para ajustes de saldo (podem ser negativos).
2. **Granularidade de 1 minuto**, sem arredondamento no registro. Regras de arredondamento comercial (ex.: cobrar em blocos de 15 min) ficam para o faturamento (v3), aplicadas no cálculo da cobrança — nunca alterando o apontamento.
3. **Consumo não é estado persistido.** Consumo do período e saldo do banco de horas são **read model**: soma dos apontamentos faturáveis do contrato no período + ajustes de saldo. Não existe coluna "horas consumidas" no contrato.
4. **Contrato congelado no apontamento:** o `ContratoId` é resolvido ao registrar e não muda depois, mesmo que a solicitação troque de contrato. O extrato de um mês fechado não muda por edição posterior de outra entidade.
5. **Fechamento de período:** o gestor pode marcar um mês como fechado para um contrato; apontamentos nesse período ficam imutáveis (correção vira ajuste de saldo no período aberto).
6. **Persistência:** `Duracao` mapeada como coluna `integer` (`duracao_minutos`) via value converter do EF Core.

## Consequências

**Positivas**
- Aritmética exata; extrato do portal sempre soma igual ao detalhe.
- Uma fonte da verdade (os apontamentos); impossível o consumo "descolar" das horas.
- Coerente com o Saldu (saldo também é read model), então o mesmo raciocínio vale nos dois produtos.

**Negativas / riscos**
- Consumo é calculado a cada leitura. Mitigação: índice `(TenantId, ContratoId, Data)` em apontamentos com `INCLUDE (duracao_minutos, faturavel)`; cache por contrato/período invalidado pelos eventos de apontamento, **só se** a medição pedir.
- Minuto inteiro não representa segundos do cronômetro: o cronômetro arredonda para o minuto mais próximo ao parar (mínimo 1 minuto). Documentado para o usuário.

## Alternativas consideradas

- **`decimal` de horas:** exato, mas convida a arredondar em lugares diferentes (0,25h vs 15min) e complica a exibição. Rejeitado.
- **`TimeSpan`:** tipo do .NET, mas carrega ticks (sub-minuto) e mapeia como `interval` no Postgres, pior para somas e índices. Rejeitado no domínio; pode ser usado na borda.
- **Contador de consumo no contrato, atualizado a cada apontamento:** leitura barata, mas exige consistência entre agregados e reprocessamento a cada edição/exclusão. Rejeitado no v1.
