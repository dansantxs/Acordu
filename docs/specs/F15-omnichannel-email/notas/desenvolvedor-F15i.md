# Notas do desenvolvedor — F15i (etapa 4)

Premissas técnicas do PR da resposta por e-mail na conversa (F15-23, F15-30 a F15-32, F15-37, F15-40 a F15-45, F15-56, F15-86; design D3, D6, D12 e §1.2; ADR 0016, itens 3 e 8). Sessão sem SDK .NET: o backend foi lido com cuidado, mas só compila e roda na CI. Nada do front mudou.

## Resposta (Application)

- **Núcleo único** (`Application.Solicitacoes.RespostaPeloContato`, design §1.2, como o `AberturaPeloContato` do F15f): `GarantirConversaAberta` (estático, para quem confere antes de subir os anexos) e `Responder(solicitação, autor, texto, canal, anexos)`, que registra a mensagem EXTERNA do contato e só acrescenta à unidade de trabalho. O `ComandosPortal.ResponderAsync` passou a usá-lo (perdeu o `IRepositorio<Interacao>`), sem mudar o comportamento: mesma ordem (encerrada → contato → storage → gravação), mesmo relógio, mesmo autor. Os efeitos (retomada de AGUARDANDO_CLIENTE no `PrimeiraRespostaERetomada`, avisos ao responsável e aos seguidores no `AvisosAosSeguidores`, nenhum ao próprio contato no `AvisosAoContato`, feed) já vinham do `InteracaoRegistrada` e são os mesmos nos dois canais. RESOLVIDA não muda (o manipulador não mexe nela) e o `ResolvidaEm` fica: o fechamento automático segue contado da resolução (F15-32, F15-45).
- **`InteracaoRegistrada` sem o canal:** nenhum manipulador precisa dele (o "O cliente respondeu" do seguidor e o feed são os mesmos do portal). Não acrescentei.
- **Ordem no `ComandosEntradaEmail`:** repetição → automático, laço, filtro → **endereço da conversa** → identificação → abertura ou triagem. A resposta automática ao endereço da conversa já caía no descarte antes do token (F15-86, sem mudança de código).
- **Token** (D3): só com o formato de `ConversaPorEmail.TokenValido` (16 símbolos do alfabeto; o destino já vem em minúsculas) vai ao banco, por `IConversasPorEmail.PorTokenAsync`, sob o filtro de tenant (índice `(tenant_id, token)`), sem rastrear. Sem conversa (adulterado, de outra empresa, de conversa que não existe mais) ou sem a solicitação dela, o e-mail **segue como e-mail novo** na empresa do código (abertura ou triagem; F15-40). O "#N" do assunto nunca é lido (F15-37).
- **Quem responde** (`IdentificacaoDoRemetente.DaSolicitacao`, função pura): o `From` com um endereço só, o do **contato da solicitação**, ativo, de cliente ativo, com `X-Email-Autenticidade: pass`. Os candidatos vêm da mesma consulta da identificação (`IContatosDeEntrada`). Autor: a conta do portal do contato ou o próprio contato (design §1.2).
- **Texto:** `ConteudoDaAbertura.TextoDaResposta`, a mesma limpeza da descrição (quebras em `\n`, sem o caractere nulo, aparado), cortada em `LimitesTexto.TextoInteracao` (10.000). A citação ainda fica (o corte é o D13, no F15j; design §3 e menor 11 do `plano-2.md`).
- **Anexos da resposta:** os **válidos** entram na mensagem, pela mesma `AnexosDaEntrada.Separar` e na chave da solicitação, subidos antes do commit e tirados se a gravação falhar (inclusive a corrida do F15-23). Premissa: o F15-44 ("vê a mensagem … com os anexos") é deste PR, então os válidos precisavam entrar aqui; os **recusados** (nota do sistema e o `aviso-anexos-recusados`, F15-34, evento `AnexosDoEmailRecusados`) ficam para o F15j: até lá não entram nem são avisados.
- **O que fica só no registro até o F15j** (design §3, "entre um PR e outro"; `IGNORADO`, desfecho `Registrada`): resposta a solicitação FECHADA ou CANCELADA (F15-33 abre outra), de quem não é o contato da solicitação (colega F15-38; outro cliente, desconhecido, autenticidade que falha, contato ou cliente inativo, F15-39 → triagem), e a resposta sem texto nem anexo válido (F15-36; sem isso o `Interacao.Registrar` recusaria e a rota daria `5xx`). Nada disso é visível: o recebimento está desligado em todas as empresas até o F15o.
- **Desfecho novo** `Respondida` (log `Entrada por e-mail: Respondida.`); `Registrada` passou a descrever só o que o F15j vai tratar.

## Registro de entrada (D6)

- `EmailRecebido.SolicitacaoId` passa a ser obrigatório também com `MENSAGEM` (sem migration: a coluna é do F15g). É a base da contagem "respostas por solicitação por hora" do F15n. Os recusados continuam só com `SOLICITACAO`.
- `EmailsRecebidos.RecusadosDaAberturaAsync` filtra também `Resultado = SOLICITACAO`, para a resposta (que agora aponta a solicitação) nunca ser lida como a abertura.
- O Id da mensagem (D6, "`InteracaoId` quando houver") não entrou: nada o lê neste PR, e evita uma migration (o F15d deixou os ids para o PR que os usa). Se o F15j precisar, entra por migration aditiva.

## Testes que mudaram por causa do comportamento novo

- `ComandosEntradaEmailTests.ReceberAsync_DestinoDaConversa_SoRegistraSemAbrir` (do F15f, que fixava o estado provisório "a resposta só registra") virou `ReceberAsync_TokenQueNaoEDeConversaNenhuma_SegueComoEmailNovo`: o token do teste nunca foi de conversa nenhuma, e pelo F15-40 o e-mail agora abre a solicitação; a asserção de que nada mais acontece foi trocada pela do comportamento novo (abre com o contato, registro `SOLICITACAO`). O construtor do helper `Comandos()` ganhou as três dependências novas. Nenhum outro teste anterior esperava `Registrada`/`IGNORADO` para a resposta.
- `AvisosAoContatoConversaTests.ConversasFalsas` ganhou o `PorTokenAsync` da porta.

## Documentos

- Contrato §18 (resposta na conversa, ordem, registro `MENSAGEM`, desfecho `Respondida`) e §12a (a resposta entra, sem "a partir do F15i"); modelo de domínio §4.3 (núcleo da resposta) e §4.15 (`SolicitacaoId` com `MENSAGEM`); segurança §9 (token só na empresa do código, só o contato da solicitação entra); fluxo BPMN ("Responder no portal ou por e-mail (SLA pausado enquanto aguarda)", design §2); `estado.md`; coluna Teste da spec.
- Sem mudança: OpenAPI e tipos do front (nenhuma rota nem DTO), ERS, read side, convenções (nenhuma regra nova), deploy e setup (sem variável nova), estrutura do repositório.
- ERS, RF-SOL-11: diz "*threading* pelo endereço da conversa ou pelos cabeçalhos dos e-mails do Acordu"; o D3 (G2) decidiu só pelo destinatário, e é o que o código faz. Não mexi no requisito (o texto admite o endereço; os cabeçalhos ficam só para o fio no programa do contato).

## Menores do `/plano` (`plano-2.md`)

- 6 ("resposta de contato de cliente inativo a solicitação aberta"): fica só no registro neste PR; o destino final (triagem com a solicitação, F15-39) é do F15j.
- 9 (F15i com 13 cenários): entregue num PR só, como a divisão manda.
- 11 (a citação, que pode conter o link sem senha do aviso, fica no texto entre o F15i e o F15j): confirmado; o risco só existe com o recebimento ligado, e ele só chega a quem usa no F15o. A mensagem é EXTERNA e o contato já vê o próprio aviso; quem mais vê é a equipe da empresa.
- 5 (`aviso-anexos-recusados`): é do F15j (os recusados da resposta não entram neste PR).
- 4, 7, 8 e 10: não são do F15i.
