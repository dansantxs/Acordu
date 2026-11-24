# Notas do testador (F14s, etapa 5)

Sessão sem SDK .NET: nada de C# foi compilado nem rodado aqui; a CI roda. O front não foi tocado.

## Cobertura conferida

F14-87 a F14-90 e F14-110 têm teste que verifica o "então" (colunas Teste já preenchidas pelo desenvolvedor e conferidas: os nomes existem). Também cobertos: segunda criação 422, ATIVA/marcada/outbox vazia, contas sem senha, restaurar sem tocar outra empresa e mantendo indicados e sessões (integração), os quatro 422 de envio, indicar (sem conta, conta fictícia, já indicada, e-mail inválido), retirar, rotas da área com 404 fora da operação, trilha com antes/depois, front (tela e faixa).

## Lacunas fechadas (DemonstracaoTests)

- `Diario_DemonstracaoMarcadaComSituacaoDeExpurgo_NaoEApagada` (VENCIDA e CANCELADA antigas): o teste existente usava ATIVA e não provava a marca no filtro do `expurgo-empresas`.
- `Diario_DemonstracaoEmTesteComOFimPassado_NaoVence`: prova a marca no `vencimento-testes`.
- `Criar_ContaComSenhaNoEmailFicticio_422SemCriarNada`: a premissa de segurança da criação não tinha teste.

## Ressalvas

- `expurgo-cadastros` e `avisos-situacao` pulam a demonstração por filtro, mas sem teste funcional próprio: a demonstração sempre tem dados (o expurgo não a pegaria) e, sem e-mail, o aviso não é observável. Cobertos só por leitura de código.
- Nada compilado nem executado aqui; os três testes novos mexem no banco por SQL cru (UTC) e dependem da CI.
