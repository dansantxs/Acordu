# ADR 0016 — Entrada de mensagens pela borda e avisos pelo canal de origem

- **Status:** Proposto (design do F15, D1, D5, D7 e D8; aguarda o G2 do F15)
- **Data:** 25/11/2026
- **Contexto do projeto:** Acordu (marco F15, RF-SOL-11, RF-SOL-14, RF-SOL-15, RF-NOT-06, RNF-SEG-11; serve ao #184, WhatsApp)
- **Complementa, sem mudar o texto:** ADR 0010 (componentes do nível 0, nenhum SDK de provedor), ADR 0002 (isolamento), ADR 0012 (logs sem dado pessoal) e ADR 0015 (guarda de só leitura).

## Contexto

Até o F14, tudo entra no Acordu por uma sessão: a equipe pelo app, o contato pelo portal (conta ou link). O F15 traz a primeira entrada **sem sessão**: um e-mail mandado ao endereço da empresa vira solicitação, e a resposta a um aviso entra na conversa. O #184 vai trazer a segunda (WhatsApp), com os mesmos problemas:

- o Cloud Run não recebe SMTP, e a API não pode depender do formato de um provedor (ADR 0010 §10);
- a mensagem chega sem tenant: o destino precisa dizer a empresa, e nada pode vazar entre empresas nem entre clientes (ADR 0002);
- quem manda pode ser forjado, automático ou abusivo, e responder a ele pode virar laço ou *backscatter*;
- os avisos ao contato (RF-NOT-01) hoje saem por e-mail, fixos; eles passam a sair pelo canal por onde a solicitação veio.

## Decisão

1. **Entrada pela borda, API neutra.** O e-mail é recebido pelo **Cloudflare Email Routing** num subdomínio próprio, entregue a um **Email Worker** (`infra/cloudflare/email-entrada`), que repassa o MIME **bruto** a um endpoint interno da API (`POST /internal/email/entrada`), pela borda (`api.acordu.app.br`, com o token de origem do ADR 0010 §3). O Worker só acrescenta o envelope (remetente e destinatário) e um **veredito neutro** (autenticidade do remetente e filtro do provedor), lidos do que o provedor carimba. A API não conhece o provedor: lê MIME (biblioteca de protocolo, como o `AWSSDK.S3` para o S3) e o veredito. Trocar o provedor é escrever outro adaptador que faça o mesmo `POST`.
2. **Autenticação do endpoint** por token compartilhado em cabeçalho, comparado em tempo constante, como os jobs (`/internal/jobs/*`): `404` sem token válido, fora do OpenAPI. Falha transitória da API (`5xx`) faz o Worker falhar, o provedor devolve erro temporário ao servidor de origem, e a entrega é repetida por ele; por isso a API é **idempotente** por mensagem e destino.
3. **Destino → tenant.** O endereço da empresa carrega um **código aleatório**, único entre todas as empresas e não derivado do nome; o endereço de uma conversa acrescenta um **token aleatório** da solicitação. A API resolve o código numa consulta global revisada da Infrastructure (convenções §1) e processa a mensagem num escopo com o tenant fixado, como os jobs (`ExecutorPorTenant`): filtros globais e guardas de gravação (isolamento e só leitura) valem como em qualquer requisição. O token da conversa é procurado **dentro** do tenant do código: token de outra empresa não existe ali. Código e token são identificadores de roteamento, não credenciais: entrar numa solicitação exige ainda que o remetente seja contato ativo do cliente dela e passe na autenticidade.
4. **Quem não é identificado vai à triagem**, uma lista por empresa com retenção curta, tratada por pessoas da equipe. Nada é respondido automaticamente a quem não é contato identificado (sem *backscatter*); mensagens automáticas, do próprio Acordu, spam e excesso dos limites são descartadas com contagem no log.
5. **Conteúdo só como texto.** A mensagem guarda texto e anexos dentro da política de anexos (RNF-SEG-07); HTML recebido vira texto no servidor e nunca é guardado nem exibido. Conteúdo e endereços recebidos não vão ao log (ADR 0012); a mensagem bruta não é guardada.
6. **Canal por mensagem.** Toda interação guarda o canal por onde entrou (`ACORDU`, `PORTAL`, `EMAIL`, `WHATSAPP`), independente da origem da solicitação.
7. **Avisos pelo canal de origem por uma porta na Application.** Os manipuladores de aviso (outbox) decidem **o quê** avisar e montam o conteúdo; uma porta de canal (`ICanalDeAviso`) entrega, e um seletor escolhe o canal pela origem da solicitação, com o e-mail como padrão e como reserva. A outbox não muda. Um canal novo é uma implementação da porta e uma linha no seletor.

## Alternativas consideradas

- **Inbound do Resend (webhook):** concentra envio e recebimento num provedor só, mas o conteúdo e os anexos vêm por chamadas à API dele depois do webhook (formato próprio na API), a assinatura é a do serviço de webhooks dele e a cota do plano gratuito pode ser dividida com o envio. Fica como troca possível pelo item 1 (outro adaptador).
- **Worker que interpreta o MIME e manda JSON:** tira o parser da API, mas leva regra (corpo, anexos, citação) para um código do provedor, gasta CPU no Worker (10 ms no plano gratuito) e espalha o tratamento em duas linguagens.
- **SMTP próprio:** o Cloud Run não aceita conexão na porta 25, e um servidor dedicado contraria o custo zero e o "sem servidor para administrar".
- **HMAC do corpo com carimbo de tempo** no lugar do token: protege contra reenvio, que a idempotência já absorve, e exige ler o corpo inteiro no Worker para assinar.
- **Endereço legível pelo nome da empresa:** fácil de ditar, mas adivinhável (spam e quem se passa por cliente).
- **Manipuladores de aviso por canal**, cada um assinando os eventos: repetiria as regras de quem avisar (contato ativo, liberação do envio, exemplos, autoria) em cada canal.

## Consequências

**Positivas**
- A API segue sem SDK e sem formato de provedor; o Worker é pequeno e testado na CI como os outros da borda.
- O mesmo caminho (destino → tenant, idempotência, triagem, canal por mensagem, porta de canal) serve ao WhatsApp.
- Empresa só leitura, isolamento e trilha valem sem código novo, porque o processamento corre num escopo de tenant comum.

**Negativas / riscos**
- A autenticidade depende do que o provedor carimba na mensagem; sem o carimbo, todo remetente cai na triagem (o design do F15 confere isso no primeiro PR da entrada).
- O plano gratuito não tem antivírus nem nota de spam exposta ao Worker: o filtro do provedor é o que ele recusa antes de entregar, e os anexos só passam pela lista de tipos detectados pelo conteúdo (segurança §7, antivírus fora do v1).
- Um componente a mais na borda (Worker, regra do Email Routing, DNS do subdomínio, token) para provisionar e acompanhar; as requisições do Worker contam na cota diária dos Workers.
- Tamanho máximo da mensagem preso ao do provedor (25 MiB no Email Routing).
