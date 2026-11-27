# Notas do desenvolvedor — F15f (etapa 4)

Premissas técnicas do PR da abertura por e-mail (F15-10 a F15-13, F15-16 a F15-18, F15-21, F15-27 a F15-29, F15-50, F15-97; design §1.2, D4, D6, D9, D11 e D14; ADR 0016, itens 3 e 8). Sessão sem SDK .NET (o proxy não libera o download): o backend foi lido com cuidado, mas só compila e roda na CI. O front rodou aqui (`pnpm test`, `lint`, `format:check`, `build`).

## Abertura

- **Núcleo único** (`Application.Solicitacoes.AberturaPeloContato`, design §1.2): o próximo protocolo, o contrato como no portal (o único ATIVO vigente), sem responsável nem categoria, o contato como contato da solicitação e os anexos como a primeira mensagem EXTERNA no canal da entrada. Só acrescenta à unidade de trabalho. O `ComandosPortal.AbrirSolicitacaoAsync` passou a usá-lo (perdeu as dependências de contrato e protocolo); a entrada por e-mail também. Os anexos do e-mail são do F15g: aqui a lista vai vazia.
- **Remetente conhecido** (`IdentificacaoDoRemetente`, funções puras): `From` com um endereço só, normalizado por `Common.Email`; `X-Email-Autenticidade` igual a `pass` (aparado, sem diferenciar maiúsculas); exatamente um contato ativo de cliente ativo entre os contatos daquele e-mail na empresa. Um inativo ao lado do ativo não atrapalha (o e-mail é único só entre os ativos do cliente). O hash do remetente do registro passou a usar a mesma normalização.
- **Consulta dos contatos** (`Persistence.EntradaEmail.ContatosDeEntrada`): os clientes com algum contato daquele e-mail, sob os filtros do tenant do destino; o filtro do contato fica na memória (poucos clientes, e evita depender da tradução de um `SelectMany` com o dono num tipo próprio). Registrada em `AddInfrastructure`.
- **Ordem no `ComandosEntradaEmail`:** repetição → automático, laço, filtro → destino com `+token` (resposta, F15i: só o registro) → identificação (não identificado, F15h: só o registro) → abertura. O desfecho novo `Aberta` vai ao log; o não identificado e a resposta continuam `Registrada`, para os testes do F15e (do testador) que esperam `Registrada` com um remetente que não é contato seguirem valendo.
- **Título e descrição** (`ConteudoDaAbertura`): a expressão do design §1.2 (`re|res|fw|fwd|enc|tr|aw|wg`, com contador opcional, repetidos, sem diferenciar maiúsculas); espaços, quebras e caracteres de controle do assunto viram um espaço; corte em 200 e 10.000 sem partir um par substituto. Na descrição, `\r\n` e `\r` viram `\n` e o caractere nulo sai (o Postgres não guarda `\0`; um e-mail com ele viraria `5xx` e reenvio por dias).
- **Texto do corpo** (`LeitorMime`): `MimeMessage.TextBody` (a primeira parte `text/plain` que não é anexo); sem ela, `HtmlBody` pelo `HtmlToText` do MimeKit.Core (tira script, estilo e o que está escondido; decodifica entidades). O assunto é o `Subject` decodificado (RFC 2047).
- **Contrato e protocolo:** como no portal. A corrida de duas entregas iguais (F15-97) serializa no contador do protocolo; a segunda esbarra no índice único do registro e volta inteira (nem solicitação, nem conversa, nem outbox); o `RecebedorDeEmail` já tratava como `Repetida`.
- **Sem migration:** os ids do que o e-mail criou no `EmailRecebido` (D6) não são lidos por nada deste PR; a coluna da solicitação entra no F15g junto com os anexos recusados, que é quem a lê (aviso de recebida). O resultado `SOLICITACAO` já existia no enum.

## Fio dos avisos (D4)

- A `ConversaPorEmail` nasce na abertura, na mesma transação, pela `DaAberturaPorEmail` (Message-ID sem `<>`, assunto como o contato escreveu, numa linha, até 998). **Sem `Message-ID`** (ou com um maior que 998): a conversa nasce por `Criar`, sem o fio, e os avisos saem com os assuntos de sempre (não há a que responder; a fábrica exige o id e o teste dela é de outro PR).
- `AvisoAoContato` ganhou `MessageIdOrigem` e `AssuntoOrigem` (opcionais, no fim). O `AvisosAoContato` lê a conversa também com o recebimento desligado, **só na origem EMAIL**, para o fio: o fio segue a origem; o Reply-To e a marcadora seguem o recebimento (F15-52). Nas outras origens, nada mudou (desligado não lê nem cria a conversa).
- `CanalEmail`, na origem EMAIL com `AssuntoOrigem`: assunto `Re: {assunto original} (#N)` (`Re: (#N)` sem assunto; o original não é limpo dos `Re:`, como pede o F15-16), o assunto de sempre como a primeira linha do texto e `In-Reply-To`/`References` `<{Message-ID}>` ao lado dos cabeçalhos de mensagem automática.
- **F15-16 × F15-57:** com o recebimento ligado (sempre, na abertura por e-mail) o texto começa pela marcadora; li "o texto começando por 'Recebemos sua solicitação #N'" como o texto do aviso logo depois dela (marcadora, linha em branco, "Recebemos sua solicitação #N", linha em branco, o texto de sempre). O F15-57 continua valendo para todos os avisos.

## Avisos e outbox

- A abertura grava num escopo próprio (o do tenant do destino), então o `DespachoOutbox` da rota não vê as mensagens dele. O `RecebedorDeEmail` despacha ele mesmo as mensagens da `MensagensDaRequisicao` desse escopo pelo `ProcessadorOutbox` antes de responder (o design §1.2 pede o aviso de recebida antes do `202`). Falha no despacho vai ao log (EventId 411, sem dado do e-mail) e não muda o desfecho; o `tick` repete.
- **Equipe (F15-17):** o e-mail interno diz "Aberta pelo cliente, por e-mail" (e "pelo WhatsApp" para o #184). O feed e o seguimento automático tratam a origem EMAIL como a do portal (quem abriu é o contato; ninguém da equipe passa a seguir).
- Empresa não liberada (F15-18): nada muda no código; o `PodeAvisarAsync` do aviso ao contato e o do `AvisoInterno` já fazem o que o cenário pede.

## API e front

- `SolicitacaoResumoDto` ganhou `origem` (depois de `statusExterno`, como no detalhe); OpenAPI à mão no formato do gerado e `pnpm api:tipos`. Contrato §7.
- Fila: rótulo discreto da origem para o que veio do cliente ("E-mail", "Portal", "WhatsApp"), como o canal das mensagens; a solicitação da equipe não ganha rótulo. O detalhe já dizia "aberta … por e-mail" (`ORIGEM`); fica como estava, com teste.

## Testes

- F15-54 (F15c) deixou o atalho do banco: a solicitação de origem EMAIL nasce pela entrada real (com o recebimento ligado; o teste desliga depois, no caso "desligado").
- `ComandosEntradaEmailTests` (do F15d) ganhou as dependências novas no construtor e os casos da abertura; `EntradaEmailTests` (Api) do F15d só teve o motivo de uma asserção ajustado ("a abertura é do F15f" → o remetente não é contato).
- O redirecionamento (F15-27) é um MIME gravado com o `From` e o DKIM do remetente original, `X-Forwarded-To` e o envelope em SRS; a autenticidade é o `pass` que o Worker mandaria pelo carimbo do Email Routing (o DKIM do remetente sobrevive ao redirecionamento).

## Conferências manuais (primeiro deploy do F15f)

- O fio no Gmail e no Outlook (F15-16, F15-50; D4): se o Gmail separar a conversa por causa do `(#N)`, volta ao G2 (design D4).
- Um redirecionamento real da caixa de um prestador (F15-27). Para as duas, o Daniel liga a própria empresa pelo `PUT /configuracoes/recebimento-email`.

## Documentos

- Contrato §7 (origem na lista), §12a (fio da origem EMAIL), §12b (por onde chegou), §18 (abertura, ordem, registro, desfechos); modelo de domínio §4.3 e §4.3.1 e §4.15; segurança §9 (identificação e autenticidade); convenções §5 (texto do e-mail recebido e o despacho da outbox no escopo da entrada); fluxo BPMN (raia do contato "portal ou e-mail", "Abrir solicitação no portal ou por e-mail", início da equipe sem o e-mail); `estado.md`; coluna Teste da spec.
- Sem mudança: ERS (RF-SOL-11 e RF-NOT-06 já trazem a abertura e o fio), read side (nenhuma consulta nova; a lista só ganhou um campo do agregado), ADRs, deploy e setup (sem variável nova), Worker (já manda `X-Email-Autenticidade`).

## Menores do `/plano` (`plano-2.md`)

- 6 (casos sem cenário): "vários `From`" tratado aqui (desconhecido; teste de unidade). Os demais (conversão, junção, anexo da triagem, cliente inativo na resposta) são da triagem e da resposta.
- 9 (F15f com 13 cenários): entregue num PR só, como a divisão manda.
- 4, 5, 7, 8, 10 e 11: não são do F15f.

## Rodada 1 (devolução do revisor)

- `RecebedorDeEmail`: o log da falha de despacho da outbox (EventId 411) leva só o tipo da exceção, não a exceção (a mensagem de uma falha de envio pode trazer o endereço do contato; convenções §5). Sem teste novo do caminho: provocar a falha exigiria um enviador que falha na Api montada, e o log já não recebe nada além de contagem e tipo.
- Fila: só a origem EMAIL ganha rótulo ("E-mail", F15-29); PORTAL e WHATSAPP sem rótulo, como antes. `AvisosAEquipe.PorOnde` sem o caso WhatsApp (é do #184; cai em "pela equipe" até lá, como antes do F15f).
- Teste de arquitetura `EntradaEmailTests.ContatosDoRemetente_SemIgnoreQueryFilters`: nenhum método de `ContatosDeEntrada`, nem da máquina de estados aninhada, chama `IgnoreQueryFilters`.
