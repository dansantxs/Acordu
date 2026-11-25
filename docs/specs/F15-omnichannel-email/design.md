# Design — F15: omnichannel A, solicitação por e-mail e situação pelo canal de origem

Spec: [spec.md](spec.md). Processo: `docs/processo.md` §3.3. ADRs: 0001, 0002, 0007, 0008, 0010, 0012, 0013, 0015; proposto por este design: [0016](../../adr/0016-entrada-pela-borda-e-avisos-pelo-canal-de-origem.md) (entrada pela borda, destino → tenant, triagem, canal por mensagem e porta de canal).

Os comportamentos estão nos cenários; aqui fica o **como**. Enquanto o G2 não decide as perguntas da spec §4, valem as recomendações; a §1.1 diz que D muda com cada resposta. Os valores numéricos (limites, retenções, tamanhos) ficam em configuração, com o padrão recomendado.

## 1. Decisões

Proposto. O G2 (merge do PR de spec e design) registra a escolha de cada D; "seguindo as recomendações" vale para todas.

1. **D1 — Por onde o e-mail entra** (ADR 0016, itens 1 e 2).
   - (a) **Recomendado.** **Cloudflare Email Routing** no subdomínio de entrada (D2), com uma regra *catch-all* que entrega a um **Email Worker** novo (`acordu-email-entrada`, em `infra/cloudflare/email-entrada/`, como a borda e a vigia). O Worker faz `POST https://api.acordu.app.br/internal/email/entrada` pela borda (que acrescenta o `X-Origem-Token`), com o MIME **bruto** em stream no corpo (`Content-Type: message/rfc822`) e cabeçalhos próprios: `X-Email-Token` (D5), `X-Email-Remetente` e `X-Email-Destinatario` (envelope), `X-Email-Autenticidade` e `X-Email-Filtro` (veredito neutro, D11). A API lê o MIME com **MimeKit** (biblioteca de protocolo, licença MIT, sem conta nem serviço; ADR 0010 §10 trata assim o `AWSSDK.S3`). Por quê: custo zero (o Email Routing é gratuito, e cada e-mail gasta duas requisições de Worker das 100 mil diárias, divididas com a borda e a vigia); o Email Routing já está provisionado na zona para `contato@` (setup Parte D); o Worker é a mesma peça que a borda já usa; e a API fica neutra: outro provedor é outro adaptador que faça o mesmo `POST`.
     - **Respostas ao Worker:** `202` para tudo que foi tratado ou ignorado de propósito (desligado, desconhecido, só leitura, automático, limite), sem dizer qual; `413` acima do tamanho (D12); `404` sem token. `5xx` ou rede fora: o Worker lança erro, o Email Routing devolve falha temporária ao servidor de origem e ele reenvia mais tarde; a idempotência (D6) absorve a repetição (F15-23).
     - **Limites do provedor:** mensagem de até 25 MiB no Email Routing; o Worker recusa antes (`setReject`) acima do limite configurado (F15-89). A API limita o corpo da rota ao mesmo valor, com folga para os cabeçalhos.
   - (b) **Inbound do Resend** (webhook `email.received`): um provedor só para enviar e receber, MX do subdomínio no Resend. Contra: o conteúdo e os anexos vêm por chamadas à API dele depois do webhook (formato próprio dentro da API, contra a neutralidade do ADR 0010), assinatura no formato do serviço de webhooks dele, e a cota do plano gratuito (100 e-mails por dia, conferida em 26/10 para o envio) pode ser dividida com o recebimento: conferir antes de escolher.
   - (c) Worker que já interpreta o MIME (`postal-mime`) e manda JSON: tira o parser da API, mas leva regra para o código do provedor e gasta a CPU do Worker (10 ms no plano gratuito) num e-mail de 25 MiB.
   - Descartado: SMTP próprio (o Cloud Run não abre a porta 25; servidor dedicado contraria o custo zero).

2. **D2 — Domínio e endereço da empresa** (pergunta 1).
   - (a) **Recomendado.** Subdomínio **`entrada.acordu.app.br`** (configuração `EmailEntrada__Dominio`), separado do `send.acordu.app.br` do Resend (SPF, DKIM e MX de retorno próprios) e do raiz (`contato@`). Endereço `{codigo}@entrada.acordu.app.br`, com `codigo` de **8 caracteres** sorteados (`RandomNumberGenerator`, no domínio) num alfabeto minúsculo sem os ambíguos (`23456789abcdefghjkmnpqrstuvwxyz`, ~39 bits), único entre todas as empresas (índice único global), guardado em claro no `Tenant` (`CodigoEntrada`), não derivado do nome. A empresa de demonstração não ganha código (F15-08). A migration sorteia o código das empresas existentes (F15-01), em SQL, repetindo em colisão. Por quê: o subdomínio isola a reputação e as regras de recebimento; o código curto se copia e não se adivinha, e com os limites do D12 a força bruta não compensa.
   - (b) Raiz com subendereço (`entrada+{codigo}@acordu.app.br`): dispensa DNS novo, mas mistura com `contato@` e depende do subendereçamento do Email Routing.
   - (c) Endereço legível pelo nome (`padaria-bom-grao@…`): alternativa da pergunta 1; adivinhável.
   - **Conferir no provisionamento do F15d:** que o Email Routing aceita o subdomínio com regra *catch-all* própria (o painel tem "subdomínios" no Email Routing). Se não aceitar, cai no (b) e o formato do endereço volta ao G2.

3. **D3 — Endereço da conversa e reconhecimento da resposta** (pergunta 2).
   - (a) **Recomendado.** `{codigo}+{token}@entrada.acordu.app.br`, com `token` de **16 caracteres** no mesmo alfabeto (~78 bits), sorteado uma vez por solicitação, guardado em claro numa entidade nova `ConversaPorEmail` (D9). A resposta é reconhecida **só pelo destinatário**: o código diz a empresa, e o token é procurado dentro dela (F15-40). O "#N" do assunto nunca conta (F15-37). `In-Reply-To` e `References` servem só para o fio no programa do contato (D4). Por quê: o destinatário é o único dado que o Acordu controla e que todo programa de e-mail respeita ao responder; o token não é credencial, porque entrar na conversa exige ainda remetente contato ativo do cliente e autenticidade (F15-38, F15-39).
   - (b) Token derivado por HMAC do Id da solicitação (sem tabela): o endereço fica longo (o Id precisa ir junto) e a chave nunca pode girar.
   - (c) Só os cabeçalhos (`In-Reply-To` com o `Message-ID` do aviso): depende de o Resend aceitar um `Message-ID` próprio e de o programa do contato manter o cabeçalho; respostas encaminhadas ou por outro programa se perdem.
   - Guardado em claro porque vai em todo aviso (o hash obrigaria a trocar o endereço a cada e-mail). Convenções §1 ganham a distinção entre segredo de uso único (só o hash) e identificador de roteamento (em claro, índice único), no F15c.

4. **D4 — Assunto e fio dos avisos de solicitação aberta por e-mail** (F15-16, F15-50).
   - (a) **Recomendado.** Na origem EMAIL, todo aviso leva `In-Reply-To` e `References` com o `Message-ID` do e-mail que abriu a solicitação (guardado na `ConversaPorEmail`) e o assunto `Re: {assunto original} (#N)`; o texto de cada aviso (hoje no assunto) abre o corpo, por exemplo "Recebemos sua solicitação #N". Nas origens INTERNA e PORTAL, os assuntos de hoje (F15-51). Por quê: o Gmail agrupa pelo assunto além dos cabeçalhos, então um assunto novo abriria outra conversa no programa do contato.
   - (b) Assuntos de hoje com os cabeçalhos: Outlook e Apple Mail agrupam; o Gmail não.
   - **Conferir no F15e** com Gmail e Outlook que o sufixo `(#N)` mantém a conversa. Se o Gmail separar, a saída é o assunto original sem o número (o protocolo vai no corpo), o que muda o "protocolo no assunto" do F15-50 e volta ao G2.

5. **D5 — Autenticação do endpoint de entrada** (ADR 0016, item 2).
   - (a) **Recomendado.** Token compartilhado (`EmailEntrada__Token` na API; segredo `TOKEN_ENTRADA` no Worker; os dois do secret `API_EMAIL_ENTRADA_TOKEN`), em `X-Email-Token`, comparado em tempo constante, `404` sem ele, rota fora do OpenAPI, como `/internal/jobs/*`. Sem token configurado, a rota inteira responde `404`. Por quê: é o padrão revisado dos jobs; a borda já exige o token de origem; repetir uma requisição autêntica só reprocessa o que a idempotência descarta.
   - (b) HMAC do corpo com carimbo de tempo: protege contra reenvio, que já é inofensivo, e obriga o Worker a ler os 25 MiB para assinar (CPU do plano gratuito).

6. **D6 — Idempotência e registro de entrada.**
   - (a) **Recomendado.** Entidade por tenant `EmailRecebido` (Domain, raiz sem cliente): `ChaveEntrada` = SHA-256 de (`Message-ID` normalizado, endereço de destino), ou do MIME inteiro quando não há `Message-ID`, com índice único `(tenant_id, chave_entrada)`; `RemetenteHash` (SHA-256 do e-mail normalizado), `RecebidoEm`, `Resultado` (SOLICITACAO, MENSAGEM, TRIAGEM, IGNORADO, RETIDO), `SolicitacaoId`/`InteracaoId` quando houver e a lista dos anexos recusados (nome e motivo). Gravado na mesma transação do efeito; a repetição esbarra no índice e responde `202` sem fazer nada. Retenção de **7 dias** (o reenvio dos servidores de origem costuma parar em 5), pelo diário. É também a base dos limites (D12). Na empresa só leitura e com o recebimento desligado nada é gravado, nem este registro (F15-05, F15-19).
   - (b) Chave só pelo `Message-ID`, global: o mesmo e-mail para as empresas A e B viraria uma entrega só (contra o F15-21).

7. **D7 — Interface de canal para os avisos ao contato** (RF-NOT-06; ADR 0016, item 7).
   - (a) **Recomendado.** Em `Acordu.Application.Notificacoes.Canais`:
     - `ICanalDeAviso { CanalAviso Canal; Task<bool> EnviarAsync(AvisoAoContato aviso, CancellationToken) }`;
     - `AvisoAoContato`: categoria, contato (nome e e-mail; o telefone entra no #184), prestador, assunto, texto, link, solicitação (Id, número, origem) e o fio (endereço da conversa e `Message-ID` de origem, quando houver);
     - `SeletorDeCanal`: escolhe pela `OrigemSolicitacao` (INTERNA, PORTAL e EMAIL → e-mail; WHATSAPP → WhatsApp quando houver implementação e o contato tiver número, senão e-mail);
     - `CanalEmail`: monta a `MensagemEmail` com o Reply-To (endereço da conversa com o recebimento ligado; senão o "responder para", F15-52), `Auto-Submitted: auto-generated` (F15-55) e, na origem EMAIL, o assunto e os cabeçalhos do D4.

     O `AvisosAoContato` segue como manipulador pós-commit dos mesmos eventos, com as regras de quem avisar (contato ativo, exemplos, `PodeAvisarAsync`, link): monta o conteúdo e entrega ao canal escolhido. A outbox não muda. `MensagemEmail` ganha `Cabecalhos` opcionais, e o `EnviadorEmailResend` os manda no campo `headers` (o SMTP de desenvolvimento, direto). Por quê: um ponto só decide o quê e para quem; o WhatsApp é uma implementação e uma linha no seletor.
   - (b) Um manipulador por canal, cada um assinando os eventos: repetiria as regras de destinatário em cada canal.
   - (c) Fila própria de avisos com canal (tabela): reentrega e estado por canal, que a outbox já dá.

8. **D8 — Canal de cada mensagem** (RF-SOL-14; F15-60 a F15-62; ADR 0016, item 6).
   - (a) **Recomendado.** Enum novo `CanalMensagem` (`ACORDU`, `PORTAL`, `EMAIL`, `WHATSAPP`) em `Interacao.Canal`, obrigatório no `Registrar`. Invariante: equipe só `ACORDU`; contato nunca `ACORDU`. Migration aditiva com padrão e preenchimento: do contato → PORTAL, da equipe → ACORDU (F15-62). As conversas da equipe (`interno.json`) e do portal (`portal.json`) trazem `canal` em cada mensagem (F15-61, pergunta 11). Por quê: canal da mensagem e origem da solicitação são coisas diferentes (a solicitação aberta no portal recebe resposta por e-mail), e "INTERNA" já é nome de visibilidade.
   - (b) Reusar `OrigemSolicitacao` na interação: menos um enum, com o `INTERNA` ambíguo ao lado da visibilidade.

9. **D9 — Onde ficam o código, o token e o fio.**
   - (a) **Recomendado.**
     - `Tenant.CodigoEntrada` (nulo só na demonstração) e `Tenant.RecebimentoEmail` (padrão desligado), com `LigarRecebimentoEmail()` e `DesligarRecebimentoEmail()`, que recusam a demonstração (§4.11 do modelo);
     - `ConversaPorEmail` (raiz com cliente, uma por solicitação): `Token`, `MessageIdOrigem` e `AssuntoOrigem` (nulos fora da origem EMAIL), criada na abertura por e-mail ou, nas outras origens, no primeiro aviso com o recebimento ligado (dentro do manipulador, como o `AcessoPortal` do link);
     - classificação "da empresa" para o só leitura, e apagada com a solicitação e com os clientes de exemplo (tem `ClienteId`).

     Por quê: não incha o agregado `Solicitacao` com dado de um canal, e o WhatsApp pode ter a sua conversa ao lado.
   - (b) Colunas na `Solicitacao`: menos uma tabela, com dado do e-mail no agregado central.

10. **D10 — Triagem** (RF-SOL-15; F15-70 a F15-81; perguntas 6 e 9).
    - (a) **Recomendado.**
      - **Modelo:** raiz por tenant `ItemTriagemEmail`, sem cliente: remetente (endereço normalizado e nome), assunto, texto (como no F15-13), `RecebidoEm`, `Motivo` (DESCONHECIDO, CONTATO_INATIVO, MAIS_DE_UM_CLIENTE, REMETENTE_NAO_CONFIRMADO, NAO_CONTATO_DA_SOLICITACAO), os clientes possíveis (F15-24), a solicitação respondida (F15-39), os anexos válidos (filhos, chave `{tenantId}/triagem/{itemId}/{anexoId}`) e os recusados. Versão por `xmin`.
      - **Ações** em `Acordu.Application.EntradaEmail.Triagem`:
        - converter: cliente ativo e contato ativo dele, ou contato novo com o nome e o e-mail do remetente pelas regras do RF-CLI-02 (F15-73, F15-74);
        - juntar: solicitação aberta à conversa e contato ativo do cliente dela (F15-75);
        - descartar (F15-76).

        Converter e juntar reaproveitam os mesmos serviços da abertura e da resposta por e-mail, copiam os anexos para a chave da solicitação (`{tenantId}/{solicitacaoId}/{anexoId}`, segurança §7) e apagam o item na mesma transação; os objetos antigos saem do storage depois do commit, em melhor esforço.
      - **Concorrência** (F15-81): quem perde a corrida do `xmin` recebe `409 triagem-ja-tratada`; quem chega depois que o item sumiu recebe `404` (a tela diz "já tratado ou não existe mais").
      - **Retenção:** passo `expurgo-email-entrada` no diário apaga o item não tratado com **30 dias** (`EmailEntrada__DiasTriagem`, F15-77) e os seus anexos no storage.
      - **Trilha:** `triagem.conversao`, `triagem.juncao` e `triagem.descarte`, com o motivo e o resultado, sem o endereço nem o texto.
      - **Permissões:** `solicitacao.criar`; juntar também `solicitacao.comentar`.
      - **Classificação:** da empresa (só leitura, F15-80); conta como dado no expurgo do cadastro.
    - (b) Solicitação num status "em triagem": exigiria cliente (não há) e entraria em fila, SLA e semelhantes.
    - (c) Sem triagem: descartar o não identificado (alternativa da pergunta 6); o F15f e o F15h somem, e o e-mail se perde sem o prestador saber.

11. **D11 — Autenticidade, spam e vírus** (F15-26, F15-87; pergunta 6).
    - (a) **Recomendado.** O Worker lê o carimbo de autenticação que o Email Routing acrescenta (`Authentication-Results` ou `ARC-Authentication-Results` com o identificador do servidor do Cloudflare; os de outro servidor são ignorados, porque o remetente pode forjá-los) e manda `X-Email-Autenticidade`: `pass` com DMARC `pass`; ou, quando o domínio do remetente não publica DMARC, DKIM alinhado ao domínio do `From` com `pass`; `fail` no resto; `none` sem carimbo. A API trata como remetente conhecido só o `pass`; `fail` e `none` vão à triagem com REMETENTE_NAO_CONFIRMADO. `X-Email-Filtro` (`ok`, `spam`, `virus`) leva o que o provedor indicar; `spam` e `virus` são descartados com contagem no log (F15-87). Por quê: o provedor já fez a conferência e a API não precisa de DNS nem de chaves; o redirecionamento da caixa do prestador (F15-27) passa pelo DKIM do remetente original.
    - (b) A API confere o DKIM sozinha (MimeKit tem verificador, com consulta DNS das chaves): reserva, se o carimbo não chegar ao Worker.
    - **Riscos a aceitar no G2:**
      - **sem carimbo, ninguém é remetente conhecido** (todo e-mail vai à triagem). O F15d confere com e-mails reais (Gmail, Outlook, domínio próprio) e registra o achado; se faltar, o (b) entra antes do F15e;
      - **o plano gratuito não tem nota de spam nem antivírus** expostos ao Worker. O "filtro do provedor" do F15-87 é, na prática, o que o Email Routing recusa antes de entregar (falha de DMARC com política `reject`, origens bloqueadas) mais o veredito, testado pelo contrato do Worker. Anexos passam só pela lista de tipos detectados pelo conteúdo, e o antivírus segue fora do v1 (segurança §7).

12. **D12 — E-mails automáticos, laço e limites** (F15-85 a F15-92; pergunta 10).
    - (a) **Recomendado.** Tudo na API (`Acordu.Application.EntradaEmail`), em funções puras com testes de unidade.
      - **Automático (F15-85, F15-86):** envelope vazio (`<>`); remetente `mailer-daemon`, `postmaster` ou `noreply` de sistema de entrega; `Auto-Submitted` diferente de `no`; `Precedence: bulk/junk/list`; `List-Id` ou `List-Unsubscribe`; `X-Autoreply`/`X-Autorespond`; `Content-Type: multipart/report` (aviso de entrega).
      - **Laço (F15-88):** remetente no domínio de entrada ou no do remetente dos avisos (`Email__Remetente`).
      - **Limites:**
        - tamanho total: `EmailEntrada__TamanhoMaximoBytes`, padrão 25 MiB, no Worker (recusa do servidor) e na API (`413`);
        - por remetente, solicitações novas por hora em cada empresa: 10;
        - por empresa, e-mails por dia: 300;
        - por solicitação, respostas por hora: 30.

        As contagens saem do `EmailRecebido` (D6), sem tabela nova. O excedente vira `RETIDO` e não cria nada. Um aviso no sino por empresa e por hora a quem tem `solicitacao.criar` (F15-90), marcado pelo primeiro `RETIDO` da hora.
      - **Ordem das conferências:** token → tamanho → destino → tenant (desconhecido, demonstração, só leitura, desligado) → automático, laço e filtro → limite da empresa → identificação e resto.
      - **Log:** só contagens por motivo, sem endereço nem conteúdo (RNF-OPS-01).
    - (b) Limites no Worker com KV: o KV gratuito grava 1.000 vezes por dia, e o Worker não sabe o tenant sem a API.

13. **D13 — Citação e assinatura** (F15-35, F15-36; pergunta 7).
    - (a) **Recomendado.** O aviso com o recebimento ligado leva no topo do texto uma linha marcadora ("— Escreva sua resposta acima desta linha —"). Na resposta, o texto novo é o que vem antes da marcadora; sem ela, valem regras conhecidas, a primeira que casar: linha "Em … escreveu:" ou "On … wrote:" (inclusive quebrada em duas linhas), "-----Mensagem original-----"/"-----Original Message-----", bloco "De:/From: … Enviado:/Sent:", e as linhas começadas por `>` no fim. No HTML sem parte em texto, os blocos `gmail_quote`, `blockquote` e `divRplyFwdMsg` saem antes da conversão. A assinatura fica. Só citação, sem anexo, não cria nada (F15-36). O texto original não é guardado (spec §3). Por quê: a marcadora resolve os programas comuns, e as regras pegam quem a apaga.
    - (b) Só as regras, sem marcadora: mais falhas com programas em outros idiomas.
    - (c) Guardar o texto inteiro (alternativa da pergunta 7).

14. **D14 — Conteúdo do e-mail e segurança.**
    - (a) **Recomendado.**
      - **Corpo:** só texto. A parte `text/plain` tem preferência; sem ela, o HTML vira texto no servidor (tokenizador do MimeKit: `script`, `style` e comentários saem, blocos viram quebras de linha, entidades decodificadas). Nenhum HTML é guardado nem exibido, e o front já mostra a mensagem como texto.
      - **Anexos:** cada um passa pela `PoliticaAnexos` e pela detecção pelo conteúdo (`TiposDeArquivo`), **um por um**. Os válidos entram até o 5º, na ordem do MIME; os outros vão à lista de recusados com o motivo (tipo, tamanho, além do 5º; F15-15). Mensagem anexada (`message/rfc822`) e partes cifradas ou assinadas sem tipo permitido são recusadas como "tipo não permitido". Imagem embutida (`cid:`) conta como anexo (pergunta 4).
      - **Horário:** o da recepção (`TimeProvider`), nunca o `Date` do e-mail, que o remetente escolhe; vale também para o SLA (ADR 0003).
      - **Isolamento:** o destino → tenant segue o ADR 0016, item 3: consulta global do código num ponto revisado da Infrastructure, e o processamento num escopo com o tenant fixado.
      - **Mensagem bruta:** não é guardada.
    - (b) Guardar o HTML sanitizado e exibi-lo: fidelidade maior, com superfície de XSS e um sanitizador para manter.

### 1.1 Perguntas da spec §4 e as D que dependem delas

As perguntas ficam com o Daniel. O que cada resposta move:

1. **Endereço:** D2. Legível pelo nome: o código vira `slug-xxxx` (sufixo sorteado contra colisão) e o F15-01 muda. Ligado para todas: muda só o padrão do `Tenant.RecebimentoEmail` e o preenchimento da migration (o F15-52 deixa de valer nas existentes). "Gerar outro endereço": uma ação a mais no F15b, que guarda o código anterior para as respostas aos avisos antigos (senão `{antigo}+{token}` se perde).
2. **Responder para:** D7 (`CanalEmail`). Opção do Cc: o Reply-To leva os dois endereços (o Resend aceita lista), e a resposta do prestador por fora fica sem registro.
3. **RESOLVIDA, FECHADA, CANCELADA:** F15g. Reabrir pela resposta: `Solicitacao.Reabrir` com o texto como motivo, e o F15-32 muda. Recusar com e-mail: um aviso novo ao contato conhecido (sem *backscatter*, mas é mais um e-mail automático). Triagem: motivo novo no D10.
4. **Anexos:** D14. Recusar o e-mail inteiro: nada é criado, e o contato precisa saber (aviso novo). Ignorar imagens embutidas pequenas: um limite em configuração (`EmailEntrada__ImagemEmbutidaMinimaBytes`).
5. **Contato em mais de um cliente:** identificação do F15e e motivo do D10. "O mais recente": consulta pela última solicitação do contato; o F15-24 muda. Impedir o e-mail repetido: muda o RF-CLI-02 e pede migration que resolva os cadastros existentes; fora deste marco.
6. **Não identificado, autenticidade e spam:** D10 e D11. Descartar o desconhecido: some a triagem (F15f, F15h e parte do F15j). Responder ao desconhecido: e-mail a endereço não verificado, contra o RF-AUT-06 e a reputação do domínio; não recomendado. Cliente "a identificar": cadastro sem dono, que entra em listas e relatórios. Spam na triagem: o `X-Email-Filtro` vira um motivo.
7. **Citação e assinatura:** D13.
8. **Colega do cliente:** conferência do remetente no F15g. "Só o contato da solicitação" ou "só TODAS_DO_CLIENTE": o F15-38 muda e o colega cai na triagem.
9. **Retenção:** valor de `EmailEntrada__DiasTriagem`. "Sem prazo" tira o passo do diário e pede nota na política de privacidade.
10. **Limites:** valores em configuração (D12). Acima de 25 MiB não cabe no Email Routing (D1). Excedente à triagem: o `RETIDO` vira item, e os limites passam a proteger só a criação de solicitações.
11. **Canal no portal:** D8. Só a equipe: o DTO do portal não leva `canal`, e o F15-61 muda.

### 1.2 Decisões de implementação (sem portão)

- **Camadas:**
  - Domain: `CanalMensagem`, `Interacao.Canal`, `Tenant` (código e opção), `ConversaPorEmail`, `EmailRecebido`, `ItemTriagemEmail`, as regras de citação e de e-mail automático como serviços puros;
  - Application: `Acordu.Application.EntradaEmail` (recebimento, identificação, abertura, resposta, triagem, limites) e `Notificacoes.Canais`;
  - Infrastructure: `Email/Entrada` (leitura do MIME com MimeKit, conversão do HTML) e a consulta revisada do código (`Persistence.EntradaEmail`);
  - Api: `/internal/email/entrada` (token, tamanho, stream) e as rotas da triagem e da opção.
- **Abertura e resposta:** um serviço único para "o contato fala", usado pelo portal e pelo e-mail: o `ComandosPortal.AbrirSolicitacaoAsync`/`ResponderAsync` e a entrada por e-mail chamam o mesmo núcleo (contrato vigente, primeira interação com os anexos, `GarantirConversaAberta`, retomada de AGUARDANDO_CLIENTE), com o canal como parâmetro. Autor da mensagem por e-mail: `contato.UsuarioId ?? contato.Id`, como na sessão de link; `AutorEhCliente = true`.
- **Título e descrição** (F15-11 a F15-13): o assunto sem prefixos `Re:`/`Fwd:`/`Enc:` repetidos só na abertura; corte em 200 e 10.000 caracteres com os limites de `LimitesTexto`. F15-33: "Em continuação à solicitação #N" seguido de linha em branco no início da descrição.
- **Nota interna dos recusados** (F15-15, F15-34): `Interacao.RegistrarNotaDoSistema`, INTERNA, canal EMAIL, autor fixo `AutoresDoSistema.EntradaEmail` (Id constante, sem conta); a conversa da equipe mostra "Acordu (e-mail recebido)". O aviso de recebida lê os recusados do `EmailRecebido` da abertura. Na resposta, o evento novo `AnexosDoEmailRecusados` gera o aviso `aviso-anexos-recusados` ao autor (o F15-56 continua: a mensagem dele não gera aviso).
- **Identificação** (F15-10, F15-24, F15-25): pelo `From` (um endereço; vários → desconhecido), normalizado como o e-mail dos contatos; consulta dos contatos com aquele e-mail no tenant, depois a regra "exatamente um ativo de cliente ativo". O envelope serve só para o automático.
- **Configuração da opção** (F15-03 a F15-07): rota própria `PUT /configuracoes/recebimento-email` `{ "ligado": true }` → `200` com `{ enderecoEntrada, recebimentoEmail }`, com `configuracao.gerenciar`; o `GET /configuracoes/empresa` ganha os dois campos (qualquer pessoa da equipe, F15-02). Sem `EmailEntrada__Dominio`, `enderecoEntrada` vem nulo e ligar dá `422 transicao-invalida` ("recebimento por e-mail indisponível"). Trilha `configuracao.recebimento-email` com antes e depois.
- **Rotas da triagem** (`interno.json`): `GET /triagem-email` (paginada, do mais novo), `GET /triagem-email/resumo` (`{ pendentes }`, para a casca), `GET /triagem-email/{id}`, `GET /triagem-email/{id}/anexos/{anexoId}` (URL pré-assinada, como os anexos), `POST /triagem-email/{id}/conversao` `{ clienteId, contatoId?, cadastrarRemetente? }` → `201 { solicitacaoId }`, `POST /triagem-email/{id}/juncao` `{ solicitacaoId, contatoId }` → `201 { interacaoId }`, `DELETE /triagem-email/{id}` → `204`. Erros de campo em `400`; solicitação encerrada → `409 solicitacao-encerrada`; item disputado → `409 triagem-ja-tratada` (tipo novo).
- **Webhook:** o corpo vai direto do stream da requisição ao parser, com o limite de tamanho do Kestrel por rota; os anexos válidos sobem ao storage antes do commit e são descartados se a gravação falhar (como no portal). `MensagensDaRequisicao` despacha a outbox no fim da própria requisição, como as outras rotas (o aviso de recebida sai antes do `202`).
- **Worker:** TypeScript, `wrangler`, `compatibility_date` fixa, `global_fetch_strictly_public` como a vigia, testes do repasse e do veredito na CI (job **Worker (email-entrada)**), segredo `TOKEN_ENTRADA` e variáveis `DESTINO` (URL da rota) e `TAMANHO_MAXIMO`. Nada do conteúdo no log do Worker; só o resultado do repasse.
- **Configuração nova:** `EmailEntrada__Dominio` (obrigatória no `deploy.yml` a partir do F15b, variável `EMAIL_ENTRADA_DOMINIO`), `EmailEntrada__Token` (secret `API_EMAIL_ENTRADA_TOKEN`, a partir do F15d; sem ele a rota responde `404` e o deploy avisa no log), `EmailEntrada__TamanhoMaximoBytes`, `EmailEntrada__DiasTriagem`, `EmailEntrada__NovasPorRemetentePorHora`, `EmailEntrada__PorEmpresaPorDia`, `EmailEntrada__RespostasPorSolicitacaoPorHora`. Valor presente e inválido impede a subida.
- **Diário:** passo `expurgo-email-entrada` depois de `expurgo-notificacoes`: `EmailRecebido` com mais de 7 dias (F15d) e triagem com mais de `EmailEntrada__DiasTriagem` (F15h); conta em `emailsRecebidosRemovidos` e `triagemRemovida`. O `ApagadorDeEmpresa` apaga os anexos da triagem no storage como os outros.
- **Testes de arquitetura e integração:** a consulta global do código só em `Persistence.EntradaEmail`; as três entidades novas classificadas (`ClassificacaoSomenteLeitura`, `ClassificacaoDoExpurgo`); isolamento entre tenants (F15-09, F15-20, F15-21, F15-40, F15-78) e por cliente (F15-28, F15-39) em integração e API; um teste que manda o mesmo MIME para dois destinos.
- **Front:** `canal` em cada mensagem (rótulo discreto: Acordu, Portal, E-mail); Configurações › Empresa com o endereço, o botão de copiar e o interruptor (só com `configuracao.gerenciar` e `podeGravar`); Triagem como item do menu para quem tem `solicitacao.criar`, com o número de pendentes na casca, lido do resumo ao abrir a casca e ao focar a janela (sem polling).

## 2. Impacto nos documentos

Cada documento muda no PR que muda o comportamento.

- **ERS:** nenhum além do que a spec já trouxe (RF-SOL-11, RF-SOL-14, RF-SOL-15, RF-NOT-06, RNF-SEG-11). Se o G2 escolher outra alternativa numa pergunta, o RF correspondente muda no mesmo PR de spec e design.
- **Estratégia (§8 e §14):** "Entrada por e-mail" sai da v2 e entra no v1, com a linha no registro de decisões (feito neste PR).
- **Modelo de domínio:**
  - §4.5: `Canal` e a invariante (F15a); nota do sistema (F15e);
  - §4.11.5 novo, recebimento por e-mail no `Tenant` (F15b);
  - §4.3: origem EMAIL e `ConversaPorEmail` (F15c, F15e);
  - §4.15 novo, entrada por e-mail: `EmailRecebido` (F15d) e `ItemTriagemEmail` (F15f, F15h);
  - §7: `AnexosDoEmailRecusados` (F15g).
- **Contrato e OpenAPI:**
  - §6: `enderecoEntrada`, `recebimentoEmail` e `PUT /configuracoes/recebimento-email` (F15b); o "responder para" deixa de ir nos avisos com o recebimento ligado (F15c);
  - §7 e §12: `canal` nas mensagens (F15a); origem EMAIL na lista e no detalhe (F15e);
  - §11a: `configuracao.recebimento-email` (F15b), `triagem.*` (F15h);
  - §12a: Reply-To da conversa, `Auto-Submitted` (F15c); assunto `Re: … (#N)` e fio na origem EMAIL (F15e); aviso de anexos recusados (F15g);
  - §13 e §14: `triagem-ja-tratada` (409, F15h);
  - §15: a entrada por e-mail deixa as "entradas previstas para a v2" e entra como rotas novas e campos aditivos; WhatsApp continua previsto (F15d);
  - §18 novo, entrada por e-mail: o contrato do Worker com a API, fora do OpenAPI (F15d), e as rotas da triagem (F15h);
  - `openapi/interno.json` e `openapi/portal.json` nos PRs que mudam rotas ou DTOs (F15a, F15b, F15h).
- **Read side:** conversa da equipe e do portal com o canal (F15a); seção nova "Triagem e entrada por e-mail": lista da triagem, resumo e as contagens dos limites pelo `EmailRecebido` (F15h, F15i).
- **Segurança:**
  - §3.1: triagem com `solicitacao.criar` (juntar com `solicitacao.comentar`), sem permissão nova (F15h);
  - §6: retenção da triagem (30 dias) e do registro de entrada (7 dias), mensagem bruta não guardada, endereços e conteúdo fora do log (F15d, F15h);
  - §7: anexos do e-mail, um por um, na mesma política, e a chave da triagem (F15e, F15f);
  - §8: ações novas da trilha (F15b, F15h);
  - §9: entrada por e-mail (token, borda, tamanho, autenticidade, automáticos, laço, *backscatter*, limites, isolamento destino → tenant; ADR 0016), no F15d e completada no F15i;
  - política de privacidade (`features/legal`): item sobre os e-mails recebidos e a triagem (F15j). Se os textos já estiverem publicados (#194), é versão nova (D10 do F14).
- **Deploy e CI:**
  - §2: topologia com Email Routing → Worker `acordu-email-entrada` → borda → API; §2.1b novo, o Worker (F15d);
  - §3: job **Worker (email-entrada)** e a área no `ci-local.sh` (o filtro `infra/cloudflare/` já cobre; F15d);
  - §4: passo do `wrangler deploy` do Worker com o segredo, depois da borda, sem segurar o deploy como a vigia (F15d);
  - §5: passo `expurgo-email-entrada` no diário (F15d, F15h);
  - §7: variáveis e secrets da §1.2, cada um no PR que o usa.
- **Setup da infra:** Parte D, passo 5 novo (F15d): subdomínio `entrada` no Email Routing (MX e SPF que ele propõe), regra *catch-all* do subdomínio → Worker, e a conferência com e-mail real; tabela de segredos (`EMAIL_ENTRADA_DOMINIO` no F15b, `API_EMAIL_ENTRADA_TOKEN` no F15d); ordem resumida e checklist. O token do Cloudflare do `deploy.yml` já tem *Workers Scripts: Edit*; a regra do Email Routing é feita à mão no painel (conferir se o `wrangler` pede outra permissão).
- **Convenções:**
  - §1: identificador de roteamento × segredo de uso único (F15c); consulta global do código como ponto revisado de `IgnoreQueryFilters` (F15d);
  - §3: rota `/internal/email/*` com token, como os jobs, e nenhuma resposta que diferencie o motivo do descarte (F15d);
  - §5: e-mail recebido só como texto; conteúdo e endereços recebidos nunca no log (F15d).
- **Estrutura do repositório:** `infra/cloudflare/email-entrada/`, `Application/EntradaEmail`, `Application/Notificacoes/Canais`, `Infrastructure/Email/Entrada` (F15c, F15d); `features/triagem` no front (F15j). `Directory.Packages.props` ganha o MimeKit (F15d).
- **Fluxo BPMN** (`scripts/gerar-fluxo-bpmn.py`, depois `--conferir`):
  - F15e: a raia do contato vira "Contato do cliente (portal ou e-mail)", a tarefa "Abrir solicitação no portal ou por e-mail", e o início da equipe deixa de citar o e-mail ("Cliente pede por telefone, WhatsApp");
  - F15g: "Responder no portal ou por e-mail";
  - F15h: tarefa "Tratar e-mail não identificado (triagem)" na raia da equipe, ligada ao protocolo.

  Este PR não muda o fluxo: o passo muda quando o código entra.
- **ADRs:** 0016 proposto neste PR; aceito no G2, com o `CLAUDE.md` passando a citar 0001–0016. Nenhum ADR aceito muda de texto: o 0010 ganha um componente pelo 0016, sem mudar a regra de nenhum SDK; o 0002 vale pelo escopo de tenant; o 0015 vale pela guarda e pelo retorno antes de gravar.
- **Manual e ajuda:** canal na conversa (F15a); Configurações › Empresa, Triagem e "como o cliente abre por e-mail" (F15j); nota de Novidades em cada PR que muda tela.
- **`docs/specs/README.md` e `docs/estado.md`:** a coluna Design do F15 e o estado (orquestrador).

## 3. Divisão em PRs

Cada PR contra a `main`, sem empilhar, com testes e CI verde.

| PR | Entrega | Cenários |
|---|---|---|
| F15a | API e front: canal de cada mensagem (D8), migration com as anteriores, `canal` nas conversas da equipe e do portal | F15-60, F15-61, F15-62 |
| F15b | API: código de entrada e opção no `Tenant` (D2, D9), migration com o código das existentes, `PUT /configuracoes/recebimento-email`, campos no `GET /configuracoes/empresa`, trilha | F15-01, F15-03, F15-04, F15-06, F15-07, F15-09 |
| F15c | API: porta de canal (D7), `CanalEmail`, `ConversaPorEmail` com o token (D3), Reply-To da conversa, `Auto-Submitted`, marcadora da citação (D13) | F15-51, F15-52, F15-53, F15-54, F15-55 |
| F15d | Worker `acordu-email-entrada` e `/internal/email/entrada` (D1, D5), leitura do MIME, `EmailRecebido` (D6), destino → tenant, descartes (desligado, só leitura, desconhecido, automático, laço, filtro, tamanho), expurgo do registro | F15-05, F15-19, F15-22, F15-85, F15-87, F15-88, F15-89 |
| F15e | API: abertura por e-mail (identificação, autenticidade, título, texto, anexos um por um, nota dos recusados), aviso de recebida no fio (D4), origem na lista | F15-10, F15-11, F15-12, F15-13, F15-14, F15-15, F15-16, F15-17, F15-18, F15-21, F15-27, F15-28, F15-29, F15-50 |
| F15f | API: chegada à triagem (`ItemTriagemEmail`, motivos, anexos na chave da triagem) | F15-20, F15-24, F15-25, F15-26, F15-70 |
| F15g | API: resposta na conversa (token, remetente do cliente, citação, status, encerrada → nova, recusados), idempotência nos três caminhos, resposta automática ao aviso | F15-23, F15-30, F15-31, F15-32, F15-33, F15-34, F15-35, F15-36, F15-37, F15-38, F15-39, F15-40, F15-41, F15-42, F15-43, F15-44, F15-56, F15-86 |
| F15h | API: ações da triagem (converter, cadastrar o remetente, juntar, descartar), `409 triagem-ja-tratada`, expurgo de 30 dias, isolamento | F15-73, F15-74, F15-75, F15-76, F15-77, F15-78, F15-79, F15-80, F15-81 |
| F15i | API: limites por remetente, por empresa e por solicitação (D12), aviso no sino | F15-90, F15-91, F15-92 |
| F15j | Front: Configurações › Empresa (endereço, copiar, interruptor), Triagem (lista, detalhe, ações, contagem na casca, menu); ajuda, política de privacidade e Novidades | F15-02, F15-08, F15-71, F15-72 |

Ordem e dependências:

- **Independentes:** F15a e F15b.
- **Avisos:** F15c depende do F15b (opção e código).
- **Entrada:** F15d depende do F15b; F15e do F15c e do F15d; F15f do F15e; F15g do F15f (a resposta de quem não é contato vai à triagem, F15-39); F15h do F15g (juntar usa a resposta); F15i do F15g.
- **Front:** F15j depende do F15b e do F15h.
- **Recebimento em produção:** o interruptor só aparece no F15j. Entre o F15d e o F15j, o Worker e a regra do Email Routing já estão no ar, mas toda empresa está com o recebimento desligado (padrão), e tudo é ignorado; o recebimento real começa quando uma empresa liga, depois do F15j.

Provisionamento manual do Daniel:

- **Antes do merge do F15b:** criar a variável `EMAIL_ENTRADA_DOMINIO` = `entrada.acordu.app.br` no GitHub (o `deploy.yml` passa a exigi-la).
- **Antes do merge do F15d:**
  - gerar o secret `API_EMAIL_ENTRADA_TOKEN` (`openssl rand -base64 48`);
  - no painel do Cloudflare, adicionar o subdomínio `entrada` ao Email Routing da zona `acordu.app.br`, aceitando os registros MX e SPF propostos, sem tocar nos do raiz nem nos de `send`.
- **Depois do primeiro deploy do F15d:** criar a regra *catch-all* do subdomínio → Worker `acordu-email-entrada` (o Worker precisa existir) e mandar um e-mail de teste de Gmail e de Outlook. Conferir no log do Worker se veio o carimbo de autenticação (D11) e no da API a contagem "desligado". As premissas do D2 e do D11 são conferidas aqui.
- **No primeiro deploy do F15c e do F15e:** conferir num e-mail real o Reply-To, o `Auto-Submitted` e o fio no Gmail e no Outlook (D4).

## 4. Registro

Uma linha por PR: o que entrou e as decisões não óbvias que surgiram na implementação.

- 25/11 — rascunho. Design sobre a spec em Rascunho, com as recomendações da §4. ADR 0016 proposto. Estratégia §8 e §14 com a entrada por e-mail no v1. Correção de forma na spec: títulos das tabelas com os PRs. Devolvido com uma dúvida de cenário: o F15-79 pede `404` para o contato na triagem, mas as rotas da equipe respondem `403` ao contato (ADR 0002, item 4; contrato §14). Premissas em `notas/arquiteto.md`.
