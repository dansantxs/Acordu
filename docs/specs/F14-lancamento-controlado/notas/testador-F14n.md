# Notas do testador (F14n, etapa 5)

Sem SDK .NET nesta sessão: nada de C# foi compilado nem rodado; os testes novos só rodam na CI (e dependem da migration `ConvitesDeEmpresa` do bot). Escritos por leitura de `ApiFixture`, `OperadorNebrys`, `Equipe`, `CaixaDeEmails` e do código de produção.

## Cobertura que já existia (desenvolvedor)
Todos os cenários F14-45, 47, 48, 49, 51, 72, 73, 74 e 105 tinham teste que verifica o "então" (convite usado/PENDENTE, nada criado por contagem, e-mail enviado, trilha sem token, link anterior que para de valer, 429 por IP, Turnstile antes do convite). Modo `convite`, `aberto` e ausente = `convite` cobertos (`Cadastro_ModoDaConfiguracao_DevolveOModo`, `Registro_ModoConviteSemConvite...`).

## Lacunas fechadas (em `tests/Acordu.Api.FunctionalTests/ConvitesEmpresaTests.cs`)
- Permissão: as 5 rotas (GET lista, GET detalhe, POST emitir, reenvio, revogação) com usuário de empresa e sem token → 404, e o convite não muda nem reenvia (antes só o POST de emissão e a lista anônima).
- Leitura: detalhe inexistente 404; lista com as 4 situações calculadas (PENDENTE, VENCIDO, USADO, REVOGADO) sem `token`/`tokenHash`; paginação.
- Erros: reenviar/revogar usado ou revogado → 422 `transicao-invalida` sem novo e-mail; reemitir para e-mail com convite revogado vale; vencido recusado no registro e reaberto pelo reenvio (link velho não volta).
- Modo aberto: sem convite 201; convite que não vale → 400 no convite.
- Concorrência (extra do desenvolvedor): 4 cadastros abertos simultâneos com o mesmo e-mail → 1 `201`, os demais `400` em `errors.email` ("Este e-mail já tem cadastro."), nunca 500, uma conta só (o teste do convite já cobria o caminho com convite).
- Cadastro com convite e dados inválidos (senha curta) → 400 e o convite não é gasto.

## Observações (para a descrição do PR)
- Isolamento entre tenants não se aplica: `convites_empresa` não tem tenant (área da Nebrys, D14); o isolamento é a sessão de operação (404 para quem não a tem), testado.
- Ordem da lista (emitidos por último primeiro) não foi testada na API: o relógio fixo da fixture empata `criado_em` e o desempate por `Id` (v7) não é monotônico no mesmo milissegundo. Fica coberta só pela leitura do código; se o Daniel quiser, um teste com o relógio avançado (estado compartilhado da fixture) resolve.
- Premissas do desenvolvedor a confirmar pelo Daniel (conta por convite nasce confirmada; um convite em aberto por e-mail; retenção dos e-mails dos convites) continuam abertas.
- Se algum teste novo falhar na CI, é defeito a investigar, não ajuste de teste.
