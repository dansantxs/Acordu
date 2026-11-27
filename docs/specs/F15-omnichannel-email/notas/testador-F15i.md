# Notas do testador — F15i (etapa 5)

Cenários F15-23, F15-30 a F15-32, F15-37, F15-40 a F15-45, F15-56, F15-86 (design D3, D4, D6, D10, D11, D12). Sem SDK .NET: os testes C# só rodam na CI; lidos contra as regras de análise (nameof, CA1826 sem `Single()` em lista, CA1859, CA1305, async/await). `node scripts/conferir-specs.mjs`: ok.

## Já coberto pelo desenvolvedor (conferido, verifica o "então")
- Api: mensagem EXTERNA no canal EMAIL, autor, texto limpo, sino e e-mail do responsável e dos seguidores, registro `MENSAGEM` com a solicitação; AGUARDANDO_CLIENTE volta; RESOLVIDA e `resolvidaEm` intactos; diário fecha no prazo da resolução; duas entregas e três em paralelo; contato sem aviso de si; "#N" sem token abre outra; token adulterado, conversa apagada e token de outra empresa (nada em B, triagem em A); empresa não liberada, só leitura (VENCIDA/CANCELADA), recebimento desligado; portal com conta e por link vê a mensagem com anexo no canal E-mail; automática (três cabeçalhos) sem mensagem nem aviso; log só com o desfecho.
- Application: núcleo `RespostaPeloContato` nos dois canais, encerrada recusada; `DaSolicitacao` (colega, inativos, sem autenticidade, sem contato); `ComandosEntradaEmail` (autor conta/contato, anexos na chave da solicitação, repetição, falha de gravação descarta o storage, tabela do que fica só no registro); integração (`PorToken` só na empresa do contexto; `RecusadosDaAbertura` ignora a resposta); domínio (`MENSAGEM` exige a solicitação e não aceita recusados).
- Teste antigo alterado (`DestinoDoConversa_SoRegistraSemAbrir` virou `TokenQueNaoEDeConversaNenhuma_SegueComoEmailNovo`): conferido; é a regra nova do F15-40, sem asserção de aceite enfraquecida.

## Acrescentado
- Api: `Responder_QuemOuOQueOF15jTrata_SoNoRegistroSemMensagemNemTriagem` (colega, contato de outro cliente da mesma empresa, autenticidade `fail`, contato inativo, solicitação cancelada, corpo vazio: nenhuma mensagem, solicitação, item de triagem nem aviso; só o registro `IGNORADO`); `Portal_RespostaPeloPortal_RetomaAvisaAEquipeESemAvisoAoContato` e `Portal_RespostaPeloPortalComSolicitacaoResolvida_EntraEContinuaResolvida` (o portal não regrediu depois da troca para o núcleo comum: canal PORTAL, AGUARDANDO_CLIENTE volta, equipe avisada, contato sem aviso, RESOLVIDA intacta); o teste do log passou a conferir também que o assunto não aparece.
- Application: token com "#N" de outra solicitação entra na do token (F15-37); conversa sem solicitação segue como e-mail novo (F15-40); resposta de 12.000 caracteres cortada em 10.000; anexos válidos e recusados juntos (só os válidos entram, sem nota nem recusados no registro); From com dois endereços só registra.
- Coluna Teste da spec atualizada (F15-30, 31, 32, 37, 40, 56).

## Observações (ressalvas para a descrição do PR)
- O corte da citação (D13) e os avisos de anexo recusado são do F15j: hoje a resposta guarda a citação inteira no texto (até 10.000), e quem tem o link sem senha do aviso na citação deixa o link na mensagem, visível à equipe. O recebimento está desligado em todas as empresas até o F15o.
- "Cliente inativo" na resposta só tem teste de unidade (`ReceberAsync_RespostaQueOF15jTrata_SoRegistraSemMensagem`); no Api foi coberto o contato inativo (a rota de inativar cliente não foi usada).
- Não há rota nova (OpenAPI intocado), então não há 403/404/409 de API a testar; o isolamento está no teste do token de outra empresa (Api) e na integração de `PorToken`.
- Nada foi executado em .NET; a conferência real é a CI.
