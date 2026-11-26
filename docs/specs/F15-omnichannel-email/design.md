# Design — F15: omnichannel A, solicitação por e-mail e situação pelo canal de origem

Spec: [spec.md](spec.md). Processo: `docs/processo.md` §3.3. ADRs: 0001, 0002, 0003, 0007, 0008, 0010, 0012, 0013, 0015; proposto por este design: [0016](../../adr/0016-entrada-pela-borda-e-avisos-pelo-canal-de-origem.md) (entrada pela borda, respostas da rota, destino → tenant, triagem, canal por mensagem, porta de canal, registro de entrada, limites, autor do sistema e expurgos por prazo).

Os comportamentos estão nos cenários; aqui fica o **como**. Enquanto o G2 não decide as perguntas da spec §4, valem as recomendações; a §1.1 diz que D muda com cada resposta. Os valores numéricos (limites, retenções, tamanhos) ficam em configuração, com o padrão recomendado.

## 1. Decisões

Proposto. O G2 (merge do PR de spec e design) registra a escolha de cada D; "seguindo as recomendações" vale para todas.

1. **D1 — Por onde o e-mail entra** (ADR 0016, itens 1 e 2).
   - (a) **Recomendado.** **Cloudflare Email Routing** no subdomínio de entrada (D2), com uma regra *catch-all* que entrega a um **Email Worker** novo (`acordu-email-entrada`, em `infra/cloudflare/email-entrada/`, como a borda e a vigia). O Worker faz `POST https://api.acordu.app.br/internal/email/entrada` pela borda (que acrescenta o `X-Origem-Token`), com o MIME **bruto** em stream no corpo (`Content-Type: message/rfc822`) e cabeçalhos próprios: `X-Email-Token` (D5), `X-Email-Remetente` e `X-Email-Destinatario` (envelope), `X-Email-Autenticidade` e `X-Email-Filtro` (veredito neutro, D11). A API lê o MIME com **MimeKit** (biblioteca de protocolo, licença MIT, sem conta nem serviço; ADR 0010 §10 trata assim o `AWSSDK.S3`). Por quê: custo zero (o Email Routing é gratuito, e cada e-mail gasta duas requisições de Worker das 100 mil diárias, divididas com a borda e a vigia); o Email Routing já está provisionado na zona para `contato@` (setup Parte D); o Worker é a mesma peça que a borda já usa; e a API fica neutra: outro provedor é outro adaptador que faça o mesmo `POST`.
     - **Respostas ao Worker** (ADR 0016, item 2): `202` sem corpo para tudo o que foi tratado ou ignorado de propósito, sem dizer qual; `413` acima do tamanho (D12); `404` sem token. `5xx` ou rede fora: o Worker lança erro, o Email Routing devolve falha temporária ao servidor de origem e ele reenvia mais tarde; a idempotência (D6) absorve a repetição (F15-23, F15-97, F15-103).
     - **Limites do provedor:** mensagem de até 25 MiB no Email Routing; o Worker recusa antes (`setReject`) acima do limite configurado (F15-89). A API limita o corpo da rota ao mesmo valor, com folga para os cabeçalhos.
   - (b) **Inbound do Resend** (webhook `email.received`): um provedor só para enviar e receber, MX do subdomínio no Resend. Contra: o conteúdo e os anexos vêm por chamadas à API dele depois do webhook (formato próprio dentro da API, contra a neutralidade do ADR 0010), assinatura no formato do serviço de webhooks dele, e a cota do plano gratuito (100 e-mails por dia, conferida em 26/10 para o envio) pode ser dividida com o recebimento: conferir antes de escolher.
   - (c) Worker que já interpreta o MIME (`postal-mime`) e manda JSON: tira o parser da API, mas leva regra para o código do provedor e gasta a CPU do Worker (10 ms no plano gratuito) num e-mail de 25 MiB.
   - Descartado: SMTP próprio (o Cloud Run não abre a porta 25; servidor dedicado contraria o custo zero).

2. **D2 — Domínio e endereço da empresa** (pergunta 1).
   - (a) **Recomendado.** Subdomínio **`entrada.acordu.app.br`** (configuração `EmailEntrada__Dominio`), separado do `send.acordu.app.br` do Resend (SPF, DKIM e MX de retorno próprios) e do raiz (`contato@`). Endereço `{codigo}@entrada.acordu.app.br`, com `codigo` de **8 caracteres** num alfabeto minúsculo sem os ambíguos (`23456789abcdefghjkmnpqrstuvwxyz`, 31 símbolos, ~39 bits), único entre todas as empresas (índice único global), guardado em claro no `Tenant` (`CodigoEntrada`), não derivado do nome. A empresa de demonstração não ganha código (F15-08, F15-96). Por quê: o subdomínio isola a reputação e as regras de recebimento; o código curto se copia e não se adivinha, e com os limites do D12 a força bruta não compensa.
     - **Empresa nova:** o código é sorteado no domínio com `RandomNumberGenerator`, por uma porta que a Application repete em colisão do índice.
     - **Empresas que já existem** (F15-01): a migration preenche no fim do `Up` (texto em `ScriptsDeDados`, convenções §1), com aleatoriedade **criptográfica do próprio Postgres**: `gen_random_uuid()` (núcleo do PostgreSQL 13+, que usa `pg_strong_random`, sem extensão), um byte aleatório do UUID por caractere (fora dos bits de versão e variante), reduzido ao alfabeto; colisão do índice repete o sorteio (bloco `DO` com `unique_violation`). O viés de módulo (256 = 8·31 + 8) tira menos de 0,1 bit por caractere, irrelevante para um identificador de roteamento. Nunca `random()`, que não é criptográfico. Um teste de integração roda o mesmo texto e confere formato, alfabeto e unicidade.
     - Alternativa do preenchimento: um passo único em C# (`RandomNumberGenerator`) depois da migration, no diário. Contra: a empresa só leitura não grava (ADR 0015), então ficaria sem endereço até voltar a ATIVA, contra o F15-01, e o endereço das demais só existe a partir do diário seguinte.
   - (b) Raiz com subendereço (`entrada+{codigo}@acordu.app.br`): dispensa DNS novo, mas mistura com `contato@` e depende do subendereçamento do Email Routing.
   - (c) Endereço legível pelo nome (`padaria-bom-grao@…`): alternativa da pergunta 1; adivinhável.
   - **Conferir no provisionamento do F15d:** que o Email Routing aceita o subdomínio com regra *catch-all* própria (o painel tem "subdomínios" no Email Routing). Se não aceitar, cai no (b) e o formato do endereço volta ao G2.

3. **D3 — Endereço da conversa e reconhecimento da resposta** (pergunta 2).
   - (a) **Recomendado.** `{codigo}+{token}@entrada.acordu.app.br`, com `token` de **16 caracteres** no mesmo alfabeto (~78 bits), sorteado no domínio uma vez por solicitação, guardado em claro numa entidade nova `ConversaPorEmail` (D9). A resposta é reconhecida **só pelo destinatário**: o código diz a empresa, e o token é procurado dentro dela (F15-40). O "#N" do assunto nunca conta (F15-37). `In-Reply-To` e `References` servem só para o fio no programa do contato (D4). Por quê: o destinatário é o único dado que o Acordu controla e que todo programa de e-mail respeita ao responder; o token não é credencial, porque entrar na conversa exige ainda remetente contato ativo do cliente e autenticidade (F15-38, F15-39).
   - (b) Token derivado por HMAC do Id da solicitação (sem tabela): o endereço fica longo (o Id precisa ir junto) e a chave nunca pode girar.
   - (c) Só os cabeçalhos (`In-Reply-To` com o `Message-ID` do aviso): depende de o Resend aceitar um `Message-ID` próprio e de o programa do contato manter o cabeçalho; respostas encaminhadas ou por outro programa se perdem.
   - Guardado em claro porque vai em todo aviso (o hash obrigaria a trocar o endereço a cada e-mail). Convenções §1 ganham a distinção entre segredo de uso único (só o hash) e identificador de roteamento (em claro, índice único), no F15c.

4. **D4 — Assunto e fio dos avisos de solicitação aberta por e-mail** (F15-16, F15-50).
   - (a) **Recomendado.** Na origem EMAIL, todo aviso leva `In-Reply-To` e `References` com o `Message-ID` do e-mail que abriu a solicitação (guardado na `ConversaPorEmail`) e o assunto `Re: {assunto original} (#N)`; o texto de cada aviso (hoje no assunto) abre o corpo, por exemplo "Recebemos sua solicitação #N". Nas origens INTERNA e PORTAL, os assuntos de hoje (F15-51). Por quê: o Gmail agrupa pelo assunto além dos cabeçalhos, então um assunto novo abriria outra conversa no programa do contato.
   - (b) Assuntos de hoje com os cabeçalhos: Outlook e Apple Mail agrupam; o Gmail não.
   - **Conferir no primeiro deploy do F15f** com Gmail e Outlook que o sufixo `(#N)` mantém a conversa. Se o Gmail separar, a saída é o assunto original sem o número (o protocolo vai no corpo), o que muda o "protocolo no assunto" do F15-50 e volta ao G2.

5. **D5 — Autenticação do endpoint de entrada** (ADR 0016, item 2).
   - (a) **Recomendado.** Token compartilhado (`EmailEntrada__Token` na API; segredo `TOKEN_ENTRADA` no Worker; os dois do secret `API_EMAIL_ENTRADA_TOKEN`), em `X-Email-Token`, comparado em tempo constante, `404` sem ele, rota fora do OpenAPI, como `/internal/jobs/*`. Sem token configurado, a rota inteira responde `404`. Por quê: é o padrão revisado dos jobs; a borda já exige o token de origem; repetir uma requisição autêntica só reprocessa o que a idempotência descarta.
   - (b) HMAC do corpo com carimbo de tempo: protege contra reenvio, que já é inofensivo, e obriga o Worker a ler os 25 MiB para assinar (CPU do plano gratuito).

6. **D6 — Idempotência e registro de entrada** (ADR 0016, itens 8 e 11).
   - (a) **Recomendado.** Entidade por tenant `EmailRecebido` (Domain, raiz sem cliente), o "registro de entrada" da spec:
     - `ChaveEntrada` = SHA-256 de (`Message-ID` normalizado, endereço de destino), ou do MIME inteiro quando não há `Message-ID`, com índice único `(tenant_id, chave_entrada)`;
     - `RemetenteHash` (SHA-256 do e-mail normalizado), `RecebidoEm`, `Resultado` (SOLICITACAO, MENSAGEM, TRIAGEM, IGNORADO, RETIDO), `SolicitacaoId`/`InteracaoId`/`ItemTriagemId` quando houver e a lista dos anexos recusados (nome e motivo). Nada do assunto, do texto nem do endereço em claro.

     Gravado na mesma transação do efeito; a repetição esbarra no índice e responde `202` sem fazer nada (F15-23, F15-97, F15-103). É também a base dos limites (D12). Na empresa desconhecida, só leitura ou com o recebimento desligado nada é gravado, nem este registro (F15-05, F15-19, F15-22).
     - **Retenção:** **7 dias** (`EmailEntrada__DiasRegistro`; o reenvio dos servidores de origem costuma parar em 5, e a janela mais longa dos limites é de 1 dia), pelo passo `expurgo-email-entrada` do diário (F15-108).
     - **Classificação:** da empresa (`ClassificacaoSomenteLeitura.DaEmpresa`) e dado no expurgo do cadastro.
   - (b) Chave só pelo `Message-ID`, global: o mesmo e-mail para as empresas A e B viraria uma entrega só (contra o F15-21).

7. **D7 — Interface de canal para os avisos ao contato** (RF-NOT-06; ADR 0016, item 7).
   - (a) **Recomendado.** Em `Acordu.Application.Notificacoes.Canais`:
     - `ICanalDeAviso { CanalAviso Canal; Task<bool> EnviarAsync(AvisoAoContato aviso, CancellationToken) }`;
     - `AvisoAoContato`: categoria, contato (nome e e-mail; o telefone entra no #184), prestador, assunto, texto, link, solicitação (Id, número, origem) e o fio (endereço da conversa e `Message-ID` de origem, quando houver);
     - `SeletorDeCanal`: escolhe pela `OrigemSolicitacao` (INTERNA, PORTAL e EMAIL → e-mail; WHATSAPP → WhatsApp quando houver implementação e o contato tiver número, senão e-mail);
     - `CanalEmail`: monta a `MensagemEmail` com o Reply-To (endereço da conversa com o recebimento ligado; senão o "responder para", F15-52), o cabeçalho `Auto-Submitted: auto-generated` (RFC 3834; F15-55), a linha marcadora no topo do texto com o recebimento ligado (D13, F15-57) e, na origem EMAIL, o assunto e os cabeçalhos do D4.

     O `AvisosAoContato` segue como manipulador pós-commit dos mesmos eventos, com as regras de quem avisar (contato ativo, exemplos, `PodeAvisarAsync`, link): monta o conteúdo e entrega ao canal escolhido. A outbox não muda. `MensagemEmail` ganha `Cabecalhos` opcionais, e o `EnviadorEmailResend` os manda no campo `headers` (o SMTP de desenvolvimento, direto). Por quê: um ponto só decide o quê e para quem; o WhatsApp é uma implementação e uma linha no seletor.
   - (b) Um manipulador por canal, cada um assinando os eventos: repetiria as regras de destinatário em cada canal.
   - (c) Fila própria de avisos com canal (tabela): reentrega e estado por canal, que a outbox já dá.

8. **D8 — Canal de cada mensagem** (RF-SOL-14; F15-60 a F15-62; ADR 0016, itens 6 e 10).
   - (a) **Recomendado.** Enum novo `CanalMensagem` (`ACORDU`, `PORTAL`, `EMAIL`, `WHATSAPP`) em `Interacao.Canal`, obrigatório no `Registrar`. Invariante no domínio: pessoa da equipe só `ACORDU`; contato nunca `ACORDU`; **exceção única**, a nota do sistema (autor `AutoresDoSistema`, D15), que é INTERNA e leva o canal da entrada que a gerou (`EMAIL` no F15; F15-15, F15-98). Migration aditiva com padrão e preenchimento: do contato → PORTAL, da equipe → ACORDU (F15-62). As conversas da equipe (`interno.json`) e do portal (`portal.json`) trazem `canal` em cada mensagem (F15-61, pergunta 11). Por quê: canal da mensagem e origem da solicitação são coisas diferentes (a solicitação aberta no portal recebe resposta por e-mail), e "INTERNA" já é nome de visibilidade.
   - (b) Reusar `OrigemSolicitacao` na interação: menos um enum, com o `INTERNA` ambíguo ao lado da visibilidade.

9. **D9 — Onde ficam o código, o token e o fio.**
   - (a) **Recomendado.**
     - `Tenant.CodigoEntrada` (nulo só na demonstração) e `Tenant.RecebimentoEmail` (padrão desligado), com `LigarRecebimentoEmail()` e `DesligarRecebimentoEmail()`, que recusam a demonstração (`422 transicao-invalida`, F15-96; §4.11 do modelo);
     - `ConversaPorEmail` (raiz com cliente, uma por solicitação): `Token`, `MessageIdOrigem` e `AssuntoOrigem` (nulos fora da origem EMAIL), criada na abertura por e-mail ou, nas outras origens, no primeiro aviso com o recebimento ligado (dentro do manipulador, como o `AcessoPortal` do link);
     - classificação "da empresa" para o só leitura, e apagada com a solicitação e com os clientes de exemplo (tem `ClienteId`).

     Por quê: não incha o agregado `Solicitacao` com dado de um canal, e o WhatsApp pode ter a sua conversa ao lado.
   - (b) Colunas na `Solicitacao`: menos uma tabela, com dado do e-mail no agregado central.

10. **D10 — Triagem** (RF-SOL-15; F15-70 a F15-84, F15-99 a F15-107; perguntas 6 e 9; ADR 0016, item 4).
    - (a) **Recomendado.**
      - **Modelo:** raiz por tenant `ItemTriagemEmail`, sem cliente: remetente (endereço normalizado e nome), assunto, texto (como no F15-13), `RecebidoEm`, `Motivo` (DESCONHECIDO, CONTATO_INATIVO, MAIS_DE_UM_CLIENTE, REMETENTE_NAO_CONFIRMADO, NAO_CONTATO_DA_SOLICITACAO), os clientes possíveis (F15-24), a solicitação respondida (F15-39), os anexos válidos (filhos, chave `{tenantId}/triagem/{itemId}/{anexoId}`) e os recusados. Versão por `xmin`. O item nunca é editado: nasce na chegada e sai inteiro numa das três ações ou no expurgo.
      - **Ações** em `Acordu.Application.EntradaEmail.Triagem`:
        - converter: cliente ativo e contato ativo dele, ou contato novo com o nome e o e-mail do remetente pelas regras do RF-CLI-02 (`cadastrarRemetente`; F15-73, F15-74);
        - juntar: solicitação aberta à conversa e contato ativo do cliente dela (F15-75);
        - descartar (F15-76).

        Converter e juntar reaproveitam os mesmos serviços da abertura e da resposta por e-mail (avisos com as regras de liberação e preferências, F15-105, F15-106), copiam os anexos para a chave da solicitação (`{tenantId}/{solicitacaoId}/{anexoId}`, segurança §7) e apagam o item na mesma transação; os objetos antigos saem do storage depois do commit, em melhor esforço.
      - **Permissões** (segurança §3.1, sem permissão nova): ler, converter e descartar com `solicitacao.criar`; **converter com `cadastrarRemetente` exige também `cliente.gerenciar`**, a mesma do `POST /clientes/{id}/contatos` (contrato §4.2), porque cria um contato (F15-74; sem ela `403`, F15-82; converter com contato existente segue só com `solicitacao.criar`); **juntar exige também `solicitacao.comentar`**, a de quem escreve na conversa (F15-75). É o que diz o RF-SOL-15.
      - **Ordem das conferências** (contrato §2): permissão (`403`, inclusive o `cliente.gerenciar` pedido pelo corpo) → item (`404` se não existe no tenant, F15-78, F15-83) → cliente ou solicitação escolhidos (`404` se de outra empresa, F15-101) → campos (`400` no `contatoId` inativo ou de outro cliente, F15-102) → regras (`422 transicao-invalida` com cliente inativo, F15-84; o `422` do `EmailDuplicadoException` com o e-mail já de contato ativo, F15-99; `409 solicitacao-encerrada`, F15-100) → só leitura no `SaveChanges` (`422 empresa-somente-leitura`, F15-80) → versão no SQL.
      - **Concorrência** (F15-81, F15-83): dois tratamentos do mesmo item leem a mesma versão; o primeiro apaga o item, e o `DELETE ... WHERE xmin = ...` do segundo não acha a linha (no Postgres, o segundo espera o bloqueio do primeiro e reavalia): a `DbUpdateConcurrencyException` vira `TriagemJaTratadaException` → **`409 triagem-ja-tratada`** (tipo novo), e a transação inteira volta (nem solicitação, nem mensagem, nem contato). Quem chega **depois** do commit já não acha o item: **`404`**, como qualquer recurso que não existe (a tela diz "já tratado ou não existe mais").
      - **Por que sem `If-Match`** (contrato §1 e convenções §3 pedem `If-Match` nas transições): o item não tem estado que mude. Ele existe, intacto, ou já saiu; não há edição que alguém possa ter perdido entre a leitura e a ação. A versão lida antes não acrescenta nada: o "outro tratou antes" já sai como `409` (corrida) ou `404` (depois), sem o cliente guardar e mandar a versão. É o mesmo raciocínio das ações sobre convites da área da Nebrys (contrato §17, "sem `If-Match`: a ação sobre um convite que já mudou cai na regra"). Se um dia o item ganhar edição (anotar, atribuir), o `If-Match` entra com ela.
      - **Retenção:** o passo `expurgo-email-entrada` do diário apaga o item não tratado com **30 dias** (`EmailEntrada__DiasTriagem`, F15-77) e os seus anexos no storage (chaves lidas antes, removidas depois do commit). Roda em **todos** os tenants, inclusive os só leitura (F15-107; D16).
      - **Trilha** (F15-104): `triagem.conversao`, `triagem.juncao` e `triagem.descarte`, alvo o item, `dados` com o motivo e o resultado (solicitação, mensagem, contato cadastrado), sem o endereço nem o texto. O expurgo do diário não vai à trilha (como os demais expurgos).
      - **Classificação:** da empresa (só leitura, F15-80); dado no expurgo do cadastro.
    - (b) Solicitação num status "em triagem": exigiria cliente (não há) e entraria em fila, SLA e semelhantes.
    - (c) Sem triagem: descartar o não identificado (alternativa da pergunta 6); os PRs F15h, F15k, F15l e F15m somem, e o e-mail se perde sem o prestador saber.

11. **D11 — Autenticidade, spam e vírus** (F15-26, F15-27, F15-87; pergunta 6). Mecanismo do "domínio confirma que o e-mail saiu dele" da spec.
    - (a) **Recomendado.** O Worker lê o carimbo de autenticação que o Email Routing acrescenta (`Authentication-Results` ou `ARC-Authentication-Results` com o identificador do servidor do Cloudflare; os de outro servidor são ignorados, porque o remetente pode forjá-los) e manda `X-Email-Autenticidade`:
      - `pass`: DMARC `pass`; ou, quando o domínio do remetente não publica DMARC, DKIM `pass` alinhado ao domínio do `From` (SPF sozinho não basta: não sobrevive ao redirecionamento e não olha o `From`);
      - `fail`: o resto;
      - `none`: sem carimbo.

      A API trata como remetente conhecido só o `pass`; `fail` e `none` vão à triagem com REMETENTE_NAO_CONFIRMADO. `X-Email-Filtro` (`ok`, `spam`, `virus`) leva o que o provedor indicar; `spam` e `virus` são descartados com contagem no log (F15-87). Por quê: o provedor já fez a conferência e a API não precisa de DNS nem de chaves; o redirecionamento da caixa do prestador (F15-27) passa pelo DKIM do remetente original.
    - (b) A API confere o DKIM sozinha (MimeKit tem verificador, com consulta DNS das chaves): reserva, se o carimbo não chegar ao Worker.
    - **Riscos a aceitar no G2:**
      - **sem carimbo, ninguém é remetente conhecido** (todo e-mail vai à triagem). O F15d confere com e-mails reais (Gmail, Outlook, domínio próprio) e registra o achado; se faltar, o (b) entra antes do F15f;
      - **o plano gratuito não tem nota de spam nem antivírus** expostos ao Worker. O "filtro do provedor" do F15-87 é, na prática, o que o Email Routing recusa antes de entregar (falha de DMARC com política `reject`, origens bloqueadas) mais o veredito, testado pelo contrato do Worker. Anexos passam só pela lista de tipos detectados pelo conteúdo, e o antivírus segue fora do v1 (segurança §7).

12. **D12 — E-mails automáticos, laço e limites** (F15-85 a F15-92; pergunta 10; ADR 0016, item 9).
    - (a) **Recomendado.** Tudo na API (`Acordu.Application.EntradaEmail`), em funções puras com testes de unidade.
      - **Automático (F15-85, F15-86)**, os sinais do "e-mail automático" da spec: envelope vazio (`<>`); remetente `mailer-daemon`, `postmaster` ou `noreply` de sistema de entrega; `Auto-Submitted` diferente de `no` (RFC 3834); `Precedence: bulk/junk/list`; `List-Id` ou `List-Unsubscribe`; `X-Autoreply`/`X-Autorespond`; `Content-Type: multipart/report` (aviso de entrega). Esta lista é a única fonte: a spec só diz o que é um e-mail automático.
      - **Laço (F15-88):** remetente no domínio de entrada ou no do remetente dos avisos (`Email__Remetente`).
      - **Limites:**
        - tamanho total: `EmailEntrada__TamanhoMaximoBytes`, padrão 25 MiB, no Worker (recusa do servidor) e na API (`413`);
        - por remetente, solicitações novas por hora em cada empresa: 10 (`EmailEntrada__NovasPorRemetentePorHora`);
        - por empresa, e-mails por dia: 300 (`EmailEntrada__PorEmpresaPorDia`);
        - por solicitação, respostas por hora: 30 (`EmailEntrada__RespostasPorSolicitacaoPorHora`).

        As contagens saem do `EmailRecebido` (D6), sem tabela nova. O excedente vira `RETIDO` e não cria nada. Um aviso no sino por empresa e por hora a quem tem `solicitacao.criar` (F15-90 a F15-92), marcado pelo primeiro `RETIDO` da hora.
      - **Ordem das conferências:** token → tamanho → destino → tenant (desconhecido, demonstração, só leitura, desligado) → automático, laço e filtro → limite da empresa → identificação e resto.
      - **Log** (F15-109): só contagens por resultado e motivo, sem endereço, assunto nem conteúdo, na API e no Worker (RNF-OPS-01, ADR 0012).
    - (b) Limites no Worker com KV: o KV gratuito grava 1.000 vezes por dia, e o Worker não sabe o tenant sem a API.

13. **D13 — Citação e assinatura** (F15-35, F15-36, F15-46, F15-57; pergunta 7).
    - (a) **Recomendado.** A marcadora dos avisos (F15-57) é a linha fixa "— Escreva sua resposta acima desta linha —", procurada sem diferenciar espaços e travessão/hífen. Na resposta, o texto novo é o que vem antes da marcadora (F15-46); sem ela, valem regras conhecidas, a primeira que casar: linha "Em … escreveu:" ou "On … wrote:" (inclusive quebrada em duas linhas), "-----Mensagem original-----"/"-----Original Message-----", bloco "De:/From: … Enviado:/Sent:", e as linhas começadas por `>` no fim. No HTML sem parte em texto, os blocos `gmail_quote`, `blockquote` e `divRplyFwdMsg` saem antes da conversão. A assinatura fica. O texto original não é guardado (spec §3). Por quê: a marcadora resolve os programas comuns, e as regras pegam quem a apaga.
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

15. **D15 — Autor da nota do sistema** (F15-15, F15-98; ADR 0016, item 10).
    - (a) **Recomendado.** `Interacao.RegistrarNotaDoSistema`, sempre INTERNA, com autor fixo `AutoresDoSistema.EntradaEmail` (Id constante, sem conta nem vínculo; o #184 ganha o seu) e o canal da entrada (D8). O DTO da conversa da equipe traz `autorSistema: true` e o nome fixo "Acordu (e-mail recebido)" no lugar do nome da pessoa; a conversa do portal nunca a lê (filtro de visibilidade, como toda INTERNA), e os manipuladores de aviso ignoram interação de autor do sistema (F15-98). Por quê: ninguém da equipe agiu, e a conversa não pode dizer que agiu; um autor constante não precisa de conta e não entra em listas de pessoas.
    - (b) Autor nulo: mudaria a obrigatoriedade do autor em toda a `Interacao` e em quem a lê (feed, trilha, menções).
    - (c) Sem nota: só o `EmailRecebido` guardaria os recusados, por 7 dias, e a equipe não os veria na conversa (contra a pergunta 4).

16. **D16 — Expurgos por prazo na empresa só leitura** (F15-107, F15-108; ADR 0016, item 11; ADR 0015, itens 3 e 4).
    - (a) **Recomendado.** O passo `expurgo-email-entrada` do diário é um expurgo de retenção como `expurgo-auditoria`, `expurgo-notificacoes` e `expurgo-atividades`: `ExecuteDelete` com `IgnoreQueryFilters` num ponto revisado de `Persistence.EntradaEmail`, em todos os tenants de uma vez, **fora** do `ExecutorPorTenant`, inclusive nos só leitura. Conferido com o ADR 0015: o item 4 tira das empresas só leitura os jobs que **operam** a empresa (recorrências, alertas de SLA, cronômetros, fechamento automático) e diz que o passo que existe por outra razão tem laço próprio; o item 3 permite a gravação fora do change tracker nos pontos revisados que declaram o que tocam. Aqui o ponto declara: apaga `EmailRecebido` e `ItemTriagemEmail` (da empresa) vencidos, em qualquer situação, porque o prazo é de dado pessoal, não de operação; segurança §5 já lista "os expurgos" entre as gravações que seguem no só leitura. Nada contradiz o texto do ADR 0015, e nenhum ADR aceito muda. A convenção §1 ("gravação nova fora do change tracker … só para o que é da pessoa ou rastro, ou com a situação conferida") ganha a menção explícita a este caso no F15e.
    - (b) Pelo `ExecutorPorTenant`: só nas graváveis, e o dado pessoal de quem talvez nem seja cliente ficaria até o expurgo da empresa (30 dias depois do só leitura, RF-EMP-03), contra o prazo prometido na política de privacidade.

### 1.1 Perguntas da spec §4 e as D que dependem delas

As perguntas ficam com o Daniel. O que cada resposta move:

1. **Endereço:** D2. Legível pelo nome: o código vira `slug-xxxx` (sufixo sorteado contra colisão) e o F15-01 muda. Ligado para todas: muda só o padrão do `Tenant.RecebimentoEmail` e o preenchimento da migration (o F15-52 deixa de valer nas existentes). "Gerar outro endereço": uma ação a mais no F15b, que guarda o código anterior para as respostas aos avisos antigos (senão `{antigo}+{token}` se perde).
2. **Responder para:** D7 (`CanalEmail`). Opção do Cc: o Reply-To leva os dois endereços (o Resend aceita lista), e a resposta do prestador por fora fica sem registro.
3. **RESOLVIDA, FECHADA, CANCELADA:** F15i e F15j. Reabrir pela resposta: `Solicitacao.Reabrir` com o texto como motivo, e o F15-32 e o F15-45 mudam. Recusar com e-mail: um aviso novo ao contato conhecido (sem *backscatter*, mas é mais um e-mail automático). Triagem: motivo novo no D10.
4. **Anexos:** D14 e D15. Recusar o e-mail inteiro: nada é criado, e o contato precisa saber (aviso novo); some a nota do sistema. Ignorar imagens embutidas pequenas: um limite em configuração (`EmailEntrada__ImagemEmbutidaMinimaBytes`).
5. **Contato em mais de um cliente:** identificação do F15f e motivo do D10. "O mais recente": consulta pela última solicitação do contato; o F15-24 muda. Impedir o e-mail repetido: muda o RF-CLI-02 e pede migration que resolva os cadastros existentes; fora deste marco.
6. **Não identificado, autenticidade e spam:** D10 e D11. Descartar o desconhecido: some a triagem (F15h, F15k, F15l, F15m e a parte dela no F15o). Responder ao desconhecido: e-mail a endereço não verificado, contra o RF-AUT-06 e a reputação do domínio; não recomendado. Cliente "a identificar": cadastro sem dono, que entra em listas e relatórios. Spam na triagem: o `X-Email-Filtro` vira um motivo.
7. **Citação e assinatura:** D13.
8. **Colega do cliente:** conferência do remetente no F15j. "Só o contato da solicitação" ou "só TODAS_DO_CLIENTE": o F15-38 muda, o colega cai na triagem e a exceção do RNF-SEG-02 sai da ERS.
9. **Retenção:** valor de `EmailEntrada__DiasTriagem`. "Sem prazo" tira o passo do diário e pede nota na política de privacidade.
10. **Limites:** valores em configuração (D12). Acima de 25 MiB não cabe no Email Routing (D1). Excedente à triagem: o `RETIDO` vira item, e os limites passam a proteger só a criação de solicitações.
11. **Canal no portal:** D8. Só a equipe: o DTO do portal não leva `canal`, e o F15-61 muda.

### 1.2 Decisões de implementação (sem portão)

- **Camadas:**
  - Domain: `CanalMensagem`, `Interacao.Canal` e `RegistrarNotaDoSistema`, `AutoresDoSistema`, `Tenant` (código e opção), `ConversaPorEmail`, `EmailRecebido`, `ItemTriagemEmail`, as regras de citação e de e-mail automático como serviços puros;
  - Application: `Acordu.Application.EntradaEmail` (recebimento, identificação, abertura, resposta, triagem, limites) e `Notificacoes.Canais`;
  - Infrastructure: `Email/Entrada` (leitura do MIME com MimeKit, conversão do HTML), a consulta revisada do código e o expurgo (`Persistence.EntradaEmail`);
  - Api: `/internal/email/entrada` (token, tamanho, stream) e as rotas da triagem e da opção.
- **Abertura e resposta:** um serviço único para "o contato fala", usado pelo portal e pelo e-mail: o `ComandosPortal.AbrirSolicitacaoAsync`/`ResponderAsync` e a entrada por e-mail chamam o mesmo núcleo (contrato vigente, primeira interação com os anexos, `GarantirConversaAberta`, retomada de AGUARDANDO_CLIENTE, sem mexer em `ResolvidaEm`, F15-45), com o canal como parâmetro. Autor da mensagem por e-mail: `contato.UsuarioId ?? contato.Id`, como na sessão de link; `AutorEhCliente = true`.
- **Título e descrição** (F15-11 a F15-13): na abertura, os prefixos saem por uma expressão do começo do assunto (`^\s*((re|res|fw|fwd|enc|tr|aw|wg)\s*(\[\d+\])?\s*:\s*)+`, sem diferenciar maiúsculas); corte em 200 e 10.000 caracteres com os limites de `LimitesTexto`. F15-33: "Em continuação à solicitação #N" seguido de linha em branco no início da descrição.
- **Recusados:** o aviso de recebida lê os recusados do `EmailRecebido` da abertura (F15-15). Na resposta, o evento novo `AnexosDoEmailRecusados` gera o aviso `aviso-anexos-recusados` ao autor (F15-34; o F15-56 continua: a mensagem dele não gera aviso).
- **Identificação** (F15-10, F15-24, F15-25): pelo `From` (um endereço; vários → desconhecido), normalizado como o e-mail dos contatos; consulta dos contatos com aquele e-mail no tenant, depois a regra "exatamente um ativo de cliente ativo". O envelope serve só para o automático.
- **Configuração da opção** (F15-03 a F15-07, F15-93 a F15-96): rota própria `PUT /configuracoes/recebimento-email` `{ "ligado": true }` → `200` com `{ enderecoEntrada, recebimentoEmail }`, com `configuracao.gerenciar`; o `GET /configuracoes/empresa` ganha os dois campos (qualquer pessoa da equipe). Sem `EmailEntrada__Dominio` (F15-94) ou na demonstração (F15-96), `enderecoEntrada` vem nulo e ligar dá `422 transicao-invalida`. Trilha `configuracao.recebimento-email` com antes e depois.
- **Rotas da triagem** (`interno.json`): `GET /triagem-email` (paginada, do mais novo), `GET /triagem-email/resumo` (`{ pendentes }`, para a casca), `GET /triagem-email/{id}`, `GET /triagem-email/{id}/anexos/{anexoId}` (URL pré-assinada, como os anexos), `POST /triagem-email/{id}/conversao` `{ clienteId, contatoId?, cadastrarRemetente? }` → `201 { solicitacaoId }`, `POST /triagem-email/{id}/juncao` `{ solicitacaoId, contatoId }` → `201 { interacaoId }`, `DELETE /triagem-email/{id}` → `204`. Política `Politicas.Equipe` (o contato, conta ou link, recebe `403`, F15-79) mais `solicitacao.criar`. Erros como no D10.
- **Webhook:** o corpo vai direto do stream da requisição ao parser, com o limite de tamanho do Kestrel por rota; os anexos válidos sobem ao storage antes do commit e são descartados se a gravação falhar (como no portal). `MensagensDaRequisicao` despacha a outbox no fim da própria requisição, como as outras rotas (o aviso de recebida sai antes do `202`).
- **Worker:** TypeScript, `wrangler`, `compatibility_date` fixa, `global_fetch_strictly_public` como a vigia, testes do repasse, da recusa por tamanho (F15-89) e do veredito na CI (job **Worker (email-entrada)**), segredo `TOKEN_ENTRADA` e variáveis `DESTINO` (URL da rota) e `TAMANHO_MAXIMO`. Nada do conteúdo nem dos endereços no log do Worker; só o resultado do repasse (F15-109).
- **Configuração nova:** `EmailEntrada__Dominio` (obrigatória no `deploy.yml` a partir do F15b, variável `EMAIL_ENTRADA_DOMINIO`), `EmailEntrada__Token` (secret `API_EMAIL_ENTRADA_TOKEN`, a partir do F15d; sem ele a rota responde `404` e o deploy avisa no log), `EmailEntrada__TamanhoMaximoBytes`, `EmailEntrada__DiasRegistro`, `EmailEntrada__DiasTriagem`, `EmailEntrada__NovasPorRemetentePorHora`, `EmailEntrada__PorEmpresaPorDia`, `EmailEntrada__RespostasPorSolicitacaoPorHora`. Valor presente e inválido impede a subida.
- **Diário:** passo `expurgo-email-entrada` depois de `expurgo-notificacoes` (D16): `EmailRecebido` com mais de `EmailEntrada__DiasRegistro` (F15e) e triagem com mais de `EmailEntrada__DiasTriagem` (F15k); conta em `emailsRecebidosRemovidos` e `triagemRemovida`. O `ApagadorDeEmpresa` apaga os anexos da triagem no storage como os outros.
- **Testes de arquitetura e integração:** a consulta global do código e o expurgo só em `Persistence.EntradaEmail`; as três entidades novas classificadas (`ClassificacaoSomenteLeitura`, `ClassificacaoDoExpurgo`); isolamento entre tenants (F15-09, F15-20, F15-21, F15-40, F15-78, F15-101) e por cliente (F15-28, F15-39, F15-102) em integração e API; um teste que manda o mesmo MIME para dois destinos; um teste que percorre todos os resultados da entrada e confere o log (F15-109).
- **Front:** `canal` em cada mensagem (rótulo discreto: Acordu, Portal, E-mail) e a nota do sistema com o nome fixo (F15a, F15g); Triagem como item do menu para quem tem `solicitacao.criar`, com o número de pendentes na casca, lido do resumo ao abrir a casca e ao focar a janela (sem polling), e a lista (F15k); detalhe e ações da triagem e Configurações › Empresa com o endereço, o botão de copiar e o interruptor (só com `configuracao.gerenciar` e `podeGravar`) no F15o.

## 2. Impacto nos documentos

Cada documento muda no PR que muda o comportamento.

- **ERS:** RNF-SEG-11 sem mecanismo (neste PR, só redação: "conferência SPF/DKIM/DMARC" → "domínio não confirma que o e-mail saiu dele"; "`Auto-Submitted`, `Precedence`, devoluções" → "respostas automáticas, avisos de entrega, mensagens em massa"); o mecanismo fica no D11 e no D12. Demais RF e RNF como a spec já trouxe (RF-SOL-11, RF-SOL-14, RF-SOL-15 com `cliente.gerenciar` e `solicitacao.comentar`, RF-NOT-06, RNF-SEG-02 com a exceção, RNF-SEG-11). Se o G2 escolher outra alternativa numa pergunta, o RF correspondente muda no mesmo PR de spec e design.
- **Estratégia (§8 e §14):** "Entrada por e-mail" sai da v2 e entra no v1, com a linha no registro de decisões (feito neste PR).
- **Modelo de domínio:**
  - §4.5: `Canal` e a invariante com a exceção do autor do sistema (F15a); nota do sistema e `AutoresDoSistema` (F15g);
  - §4.11.5 novo, recebimento por e-mail no `Tenant` (F15b);
  - §4.3: origem EMAIL e `ConversaPorEmail` (F15c, F15f);
  - §4.15 novo, entrada por e-mail: `EmailRecebido` (F15d) e `ItemTriagemEmail` (F15h, F15k);
  - §7: `AnexosDoEmailRecusados` (F15j).
- **Contrato e OpenAPI:**
  - §6: `enderecoEntrada`, `recebimentoEmail` e `PUT /configuracoes/recebimento-email` (F15b); o "responder para" deixa de ir nos avisos com o recebimento ligado (F15c);
  - §7 e §12: `canal` nas mensagens (F15a); `autorSistema` na conversa da equipe (F15g); origem EMAIL na lista e no detalhe (F15f);
  - §11a: `configuracao.recebimento-email` (F15b), `triagem.descarte` (F15k), `triagem.conversao` (F15l), `triagem.juncao` (F15m);
  - §12a: Reply-To da conversa, `Auto-Submitted`, marcadora (F15c); assunto `Re: … (#N)` e fio na origem EMAIL (F15f); recusados no aviso de recebida (F15g); aviso de anexos recusados na resposta (F15j);
  - §13 e §14: `triagem-ja-tratada` (409, F15k), com a nota de que as rotas da triagem não usam `If-Match` (D10);
  - §15: a entrada por e-mail deixa as "entradas previstas para a v2" e entra como rotas novas e campos aditivos; WhatsApp continua previsto (F15d);
  - §18 novo, entrada por e-mail: o contrato do Worker com a API, fora do OpenAPI, com as respostas do ADR 0016 item 2 (F15d), e as rotas da triagem (F15k, completadas no F15l e no F15m);
  - `openapi/interno.json` e `openapi/portal.json` nos PRs que mudam rotas ou DTOs (F15a, F15b, F15g, F15k, F15l, F15m).
- **Read side:** conversa da equipe e do portal com o canal (F15a) e a nota do sistema (F15g); seção nova "Triagem e entrada por e-mail": lista da triagem e resumo (F15k) e as contagens dos limites pelo `EmailRecebido` (F15n).
- **Segurança:**
  - §3.1: triagem com `solicitacao.criar`; cadastrar o remetente também com `cliente.gerenciar`; juntar também com `solicitacao.comentar`; sem permissão nova (F15k, F15l, F15m);
  - §5: o passo `expurgo-email-entrada` entre os expurgos que seguem no só leitura (F15e);
  - §6: retenção da triagem (30 dias) e do registro de entrada (7 dias), mensagem bruta não guardada, endereços e conteúdo fora do log (F15d, F15e, F15k);
  - §7: anexos do e-mail, um por um, na mesma política (F15g), e a chave da triagem (F15h);
  - §8: ações novas da trilha (F15b, F15k, F15l, F15m);
  - §9: entrada por e-mail (token, borda, tamanho, autenticidade, automáticos, laço, *backscatter*, isolamento destino → tenant; ADR 0016), no F15d e no F15e, e os limites no F15n;
  - política de privacidade (`features/legal`): item sobre os e-mails recebidos e a triagem (F15o). Se os textos já estiverem publicados (#194), é versão nova (D10 do F14).
- **Deploy e CI:**
  - §2: topologia com Email Routing → Worker `acordu-email-entrada` → borda → API; §2.1b novo, o Worker (F15d);
  - §3: job **Worker (email-entrada)** e a área no `ci-local.sh` (o filtro `infra/cloudflare/` já cobre; F15d);
  - §4: passo do `wrangler deploy` do Worker com o segredo, depois da borda, sem segurar o deploy como a vigia (F15d);
  - §5: passo `expurgo-email-entrada` no diário (F15e, completado no F15k);
  - §7: variáveis e secrets da §1.2, cada um no PR que o usa.
- **Setup da infra:** Parte D, passo 5 novo (F15d): subdomínio `entrada` no Email Routing (MX e SPF que ele propõe), regra *catch-all* do subdomínio → Worker, e a conferência com e-mail real; tabela de segredos (`EMAIL_ENTRADA_DOMINIO` no F15b, `API_EMAIL_ENTRADA_TOKEN` no F15d); ordem resumida e checklist. O token do Cloudflare do `deploy.yml` já tem *Workers Scripts: Edit*; a regra do Email Routing é feita à mão no painel (conferir se o `wrangler` pede outra permissão).
- **Convenções** (regras de código, no PR que traz o código; as decisões que valem além do F15 estão no ADR 0016):
  - §1: identificador de roteamento × segredo de uso único (F15c); consulta global do código como ponto revisado de `IgnoreQueryFilters` (F15d); expurgo de retenção de dado da empresa em todos os tenants, inclusive só leitura (F15e, D16); preenchimento com aleatoriedade criptográfica (`gen_random_uuid()`, nunca `random()`) quando a migration sorteia um identificador (F15b);
  - §2: autor do sistema na `Interacao` (`AutoresDoSistema`) e a exceção da invariante do canal (F15g);
  - §3: rota `/internal/<canal>/entrada` com token, como os jobs, e as respostas do ADR 0016 item 2, que não diferenciam o motivo do descarte (F15d);
  - §5: e-mail recebido só como texto; conteúdo e endereços recebidos nunca no log (F15d).
- **Estrutura do repositório:** `infra/cloudflare/email-entrada/`, `Application/EntradaEmail`, `Application/Notificacoes/Canais`, `Infrastructure/Email/Entrada`, `Persistence/EntradaEmail` (F15c, F15d); `features/triagem` no front (F15k). `Directory.Packages.props` ganha o MimeKit (F15d).
- **Fluxo BPMN** (`scripts/gerar-fluxo-bpmn.py`, depois `--conferir`):
  - F15f: a raia do contato vira "Contato do cliente (portal ou e-mail)", a tarefa "Abrir solicitação no portal ou por e-mail", e o início da equipe deixa de citar o e-mail ("Cliente pede por telefone, WhatsApp");
  - F15i: "Responder no portal ou por e-mail";
  - F15k: tarefa "Tratar e-mail não identificado (triagem)" na raia da equipe, ligada ao protocolo.

  Este PR não muda o fluxo: o passo muda quando o código entra.
- **ADRs:** 0016 proposto neste PR (itens 1 a 11, com as decisões promovidas deste design: respostas da rota, registro de entrada, limites, autor do sistema e expurgos por prazo); aceito no G2. Nenhum ADR aceito muda de texto: o 0010 ganha um componente pelo 0016, sem mudar a regra de nenhum SDK; o 0002 vale pelo escopo de tenant; o 0015 vale pela guarda, pelo retorno antes de gravar e pelos itens 3 e 4 nos expurgos (D16).
- **`CLAUDE.md`:** na tabela "Onde está cada decisão", Arquitetura passa a `docs/adr/` (0001–0016) quando o 0016 for aceito, no PR de spec e design, antes do merge (orquestrador; o arquiteto não edita o `CLAUDE.md`).
- **Manual e ajuda:** canal na conversa (F15a); Triagem (F15k); Configurações › Empresa e "como o cliente abre por e-mail" (F15o); nota de Novidades em cada PR que muda tela.
- **`docs/specs/README.md` e `docs/estado.md`:** a coluna Design do F15 e o estado (orquestrador).

## 3. Divisão em PRs

Cada PR contra a `main`, sem empilhar, com testes e CI verde. **A cadeia é em série:** um PR por vez, na ordem da tabela, cada um com a branch criada da `main` depois do merge do anterior (G3); nenhum começa antes de o anterior estar mesclado. As dependências reais estão abaixo da tabela, para quem um dia precisar reordenar.

| PR | Entrega | Cenários |
|---|---|---|
| F15a | API e front: canal de cada mensagem (D8), migration com as anteriores, `canal` nas conversas da equipe e do portal | F15-60, F15-61, F15-62 |
| F15b | API: código de entrada e opção no `Tenant` (D2, D9), migration com o código das existentes (aleatoriedade criptográfica), `PUT /configuracoes/recebimento-email`, campos no `GET /configuracoes/empresa`, demonstração e domínio ausente, trilha | F15-01, F15-03, F15-04, F15-06, F15-07, F15-09, F15-93, F15-94, F15-95, F15-96 |
| F15c | API: porta de canal (D7), `CanalEmail`, `ConversaPorEmail` com o token (D3), Reply-To da conversa, `Auto-Submitted`, marcadora (D13) | F15-51, F15-52, F15-53, F15-54, F15-55, F15-57 |
| F15d | Worker `acordu-email-entrada` e `/internal/email/entrada` (D1, D5), leitura do MIME, `EmailRecebido` (D6), destino → tenant, descartes por desligado, só leitura, desconhecido e tamanho; provisionamento | F15-05, F15-19, F15-22, F15-89 |
| F15e | API: automáticos, laço e filtro do provedor (D11, D12); passo `expurgo-email-entrada` do registro (D16) | F15-85, F15-87, F15-88, F15-108 |
| F15f | API: abertura por e-mail (identificação, autenticidade, título, texto), aviso de recebida no fio (D4), aviso à equipe, origem na lista, idempotência da abertura | F15-10, F15-11, F15-12, F15-13, F15-16, F15-17, F15-18, F15-21, F15-27, F15-28, F15-29, F15-50, F15-97 |
| F15g | API e front: anexos da abertura um por um (D14), nota do sistema dos recusados (D15) e os recusados no aviso de recebida | F15-14, F15-15, F15-98 |
| F15h | API: chegada à triagem (`ItemTriagemEmail`, motivos, anexos na chave da triagem), idempotência da triagem | F15-20, F15-24, F15-25, F15-26, F15-70, F15-103 |
| F15i | API: resposta na conversa (token, status, prazo do fechamento, liberação, só leitura, desligado, endereço adulterado), idempotência da resposta, resposta automática ao aviso | F15-23, F15-30, F15-31, F15-32, F15-37, F15-40, F15-41, F15-42, F15-43, F15-44, F15-45, F15-56, F15-86 |
| F15j | API: citação e marcadora na resposta (D13), anexos da resposta, colega do cliente, remetente que não é do cliente (triagem), encerrada → nova | F15-33, F15-34, F15-35, F15-36, F15-38, F15-39, F15-46 |
| F15k | API e front: rotas da triagem (lista, resumo, detalhe, anexo), descarte, `409 triagem-ja-tratada`, expurgo da triagem; menu, contagem na casca e a lista | F15-71, F15-72, F15-76, F15-77, F15-79, F15-107 |
| F15l | API: conversão (cliente e contato escolhidos, cadastrar o remetente com `cliente.gerenciar`), avisos da conversão, isolamento | F15-73, F15-74, F15-78, F15-82, F15-84, F15-99, F15-105 |
| F15m | API: junção (`solicitacao.comentar`), avisos da junção; regras comuns das três ações: só leitura, corrida, item que já saiu, ids de outra empresa, contato inválido, trilha | F15-75, F15-80, F15-81, F15-83, F15-100, F15-101, F15-102, F15-104, F15-106 |
| F15n | API: limites por remetente, por empresa e por solicitação (D12), aviso no sino; log de todos os resultados da entrada | F15-90, F15-91, F15-92, F15-109 |
| F15o | Front: Configurações › Empresa (endereço, copiar, interruptor), detalhe e ações da triagem (converter, cadastrar o remetente, juntar, descartar); ajuda, política de privacidade e Novidades | F15-02, F15-08 |

Notas da divisão:

- **F15-71 e F15-72 no F15k, com a tela mínima.** As rotas, o `403` do Leitor e a lista pela API nascem no F15k. Os dois cenários também dizem "a casca mostra quantos aguardam" e "o menu não mostra a triagem"; para cada cenário ficar num PR só, com o "então" inteiro testado, o item do menu, a contagem na casca e a lista (só leitura) entram no F15k. O F15o fica com o detalhe, as ações e as Configurações. Se o G2 preferir o F15k só de API, o analista separa cada um dos dois em um cenário de API e um de tela (nota em `notas/arquiteto.md`), e os de tela vão para o F15o.
- **Cenários que valem para mais de um caminho** ficam no último PR que completa o caminho: F15-80, F15-81, F15-83, F15-101, F15-102 e F15-104 (converter, juntar e descartar) no F15m; F15-109 (todos os resultados da entrada) no F15n. Os PRs anteriores já seguem a regra (o `409` do descarte nasce no F15k, o log sem dado pessoal desde o F15d pela convenção §5) e testam o próprio caminho; o cenário é fechado com o teste que percorre todos.
- **Idempotência por caminho:** abertura F15-97 no F15f, triagem F15-103 no F15h, resposta F15-23 no F15i.
- **Entre um PR e outro**, o que ainda não existe é ignorado com contagem no log: até o F15f, todo e-mail de empresa ligada é `IGNORADO`; até o F15h, o não identificado também; até o F15j, a resposta guarda o texto com a citação. Nada disso é visível, porque o recebimento está desligado em todas as empresas até o F15o (abaixo).

Dependências reais:

- F15a e F15b não dependem de nada; F15c do F15b (opção e código); F15d do F15b; F15e do F15d.
- F15f do F15c (aviso no fio) e do F15e (descartes antes da identificação); F15g do F15f; F15h do F15g (os anexos válidos da triagem usam a política um por um).
- F15i do F15h (o endereço adulterado e o remetente estranho caem na triagem, F15-40); F15j do F15i.
- F15k do F15h; F15l do F15k; F15m do F15l e do F15j (juntar usa a resposta inteira).
- F15n do F15i (respostas por solicitação) e do F15e (registro); F15o do F15b e do F15m.
- **Recebimento em produção:** o interruptor só aparece no F15o. Entre o F15d e o F15o, o Worker e a regra do Email Routing já estão no ar, mas toda empresa está com o recebimento desligado (padrão), e tudo é ignorado; o recebimento real começa quando uma empresa liga, depois do F15o. Para as conferências à mão abaixo, o Daniel liga a própria empresa pelo `PUT /configuracoes/recebimento-email`.

Provisionamento e conferências manuais do Daniel:

- **Antes do merge do F15b:** criar a variável `EMAIL_ENTRADA_DOMINIO` = `entrada.acordu.app.br` no GitHub (o `deploy.yml` passa a exigi-la).
- **Antes do merge do F15d:**
  - gerar o secret `API_EMAIL_ENTRADA_TOKEN` (`openssl rand -base64 48`);
  - no painel do Cloudflare, adicionar o subdomínio `entrada` ao Email Routing da zona `acordu.app.br`, aceitando os registros MX e SPF propostos, sem tocar nos do raiz nem nos de `send`.
- **Depois do primeiro deploy do F15d:** criar a regra *catch-all* do subdomínio → Worker `acordu-email-entrada` (o Worker precisa existir) e mandar um e-mail de teste de Gmail e de Outlook. Conferir no log do Worker se veio o carimbo de autenticação (D11) e no da API a contagem "desligado". As premissas do D2 e do D11 são conferidas aqui.
- **No primeiro deploy do F15c:** num e-mail real, o Reply-To, o `Auto-Submitted` e a marcadora.
- **No primeiro deploy do F15f:** o fio no Gmail e no Outlook (F15-16, F15-50; D4) e o redirecionamento real da caixa de um prestador (F15-27).
- **No primeiro deploy do F15h:** um e-mail forjado e um legítimo do mesmo domínio (F15-26).

## 4. Registro

Uma linha por PR: o que entrou e as decisões não óbvias que surgiram na implementação.

- 25/11 — rascunho. Design sobre a spec em Rascunho, com as recomendações da §4. ADR 0016 proposto. Estratégia §8 e §14 com a entrada por e-mail no v1. Correção de forma na spec: títulos das tabelas com os PRs. Devolvido com uma dúvida de cenário (F15-79 em `404`); o Daniel decidiu `403` e o analista reescreveu o cenário.
- 25/11 — devolução do `/plano` (1ª, `notas/plano-1.md`; o Daniel: "siga com todos"). D10 com as permissões do RF-SOL-15 (`cliente.gerenciar` para cadastrar o remetente, `solicitacao.comentar` para juntar), a ordem das conferências, `409` na corrida e `404` depois, e por que sem `If-Match`. D8 com a exceção da nota do sistema; D15 (autor do sistema) e D16 (expurgos no só leitura, conferido com o ADR 0015 itens 3 e 4) novos. D2: migration com `gen_random_uuid()` em vez de `random()`. ADR 0016 ganha os itens 8 a 11 (registro de entrada, limites, autor do sistema, expurgos) e as respostas da rota no item 2. ERS: RNF-SEG-11 sem SPF/DKIM/DMARC nem `Auto-Submitted` (redação). D11 e D12 ficam só com o mecanismo; os comportamentos (marcadora, `422` sem domínio, `404` do item tratado) estão nos cenários. Divisão refeita em 15 PRs em série (o antigo F15d aliviado em F15d e F15e; abertura em F15f e F15g; resposta em F15i e F15j; triagem em F15k, F15l e F15m), com F15-71 e F15-72 no PR das rotas, cada cenário novo num PR e a idempotência no PR de cada caminho. F15-79 em `403`, sem pendência. Títulos das tabelas da spec com os PRs novos.
