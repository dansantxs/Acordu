# Spec — F15: omnichannel A, solicitação por e-mail e situação pelo canal de origem

O **quê** o marco entrega. O **como** fica em [design.md](design.md). Processo: `docs/processo.md` §3.2.

| | |
|---|---|
| **Status** | Rascunho |
| **Data** | 25/11/2026 |
| **Requisitos** | RF-SOL-11 (sai da v2 para o v1, reescrito, 25/11); RF-SOL-14, RF-SOL-15, RF-NOT-06, RNF-SEG-11 (novos, 25/11); RF-SOL-03, RF-SOL-06, RF-SOL-10, RF-SOL-13, RF-NOT-01, RF-NOT-02, RF-POR-02, RF-POR-04, RF-POR-05, RF-AUT-06, RF-EMP-02, RF-NEB-02, RF-CLI-02, RNF-SEG-01 a RNF-SEG-03, RNF-SEG-07 (já existentes, tocados) |
| **ADRs** | 0001, 0002, 0010, 0012, 0015; 0016 (proposto no design) |
| **Issue** | #183 |
| **Design** | [design.md](design.md) |

## 1. Objetivo

O cliente do prestador abre e acompanha uma solicitação só por e-mail, sem entrar no portal: o e-mail que ele manda ao endereço da empresa vira solicitação com protocolo, a resposta dele a um aviso entra na conversa certa, e os avisos da situação chegam pelo canal por onde a solicitação veio. A equipe vê em cada mensagem por onde ela chegou e trata numa triagem o que não dá para atribuir a um contato.

## 2. Cenários de aceite

Termos usados nos cenários:

- **Endereço de entrada**: o endereço de e-mail que o Acordu dá a cada empresa para receber solicitações (pergunta 1).
- **Recebimento por e-mail**: a opção da empresa que liga ou desliga o endereço de entrada (pergunta 1).
- **Aviso**: os e-mails ao contato do RF-NOT-01 (recebida, mensagem externa da equipe, aguardando você, resolvida).
- **Endereço da conversa**: o endereço para onde vai a resposta a um aviso, que aponta para uma solicitação só (pergunta 2). Como ele é formado e como a resposta é reconhecida (cabeçalhos, token) é do design.
- **Remetente conhecido**: o endereço do remetente, normalizado como o e-mail dos contatos, é o de **exatamente um** contato ativo, de cliente ativo, da empresa do endereço de entrada, e o e-mail passa na conferência de autenticidade (F15-26). Os demais (ninguém, mais de um contato, contato ou cliente inativo, pessoa da equipe que não é contato, autenticidade que falha) são **remetente não identificado**.
- **Triagem**: a lista da empresa com os e-mails de remetente não identificado, para a equipe decidir o que fazer (RF-SOL-15, pergunta 6).
- **E-mail automático**: resposta automática (ausência, férias), aviso de entrega (devolução, *bounce*) ou mensagem em massa, reconhecido pelos cabeçalhos padrão (`Auto-Submitted` diferente de `no`, `Precedence: bulk/junk/list`, remetente vazio ou de sistema de entrega) ou pelo filtro do provedor.
- **Canal** de uma mensagem: por onde ela entrou no Acordu: **Acordu** (equipe, pelo app), **Portal** (contato, com conta ou link) ou **E-mail** (contato, pelo endereço de entrada ou pela resposta a um aviso). WhatsApp entra no #184.
- **Só leitura**: empresa VENCIDA ou CANCELADA (RF-EMP-02).
- "Pergunta N" aponta para a seção 4; enquanto o G2 não decide, os cenários seguem a recomendação. Tipos de erro novos, nomes de campo, rotas, provedor de entrada e formato do webhook ficam para o design.

### Endereço de entrada e configuração (F15b; F15-05 no F15d; F15-02 e F15-08 no F15j)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F15-01 | qualquer empresa, exceto a de demonstração | ela é criada (ou o F15 entra no ar, para as que já existem) | ela tem um endereço de entrada próprio, diferente do de qualquer outra empresa, que não é formado só pelo nome dela (pergunta 1), com o recebimento por e-mail desligado | — |
| F15-02 | empresa com o endereço de entrada | qualquer pessoa da equipe abre Configurações › Empresa | vê o endereço de entrada, com o botão de copiar, e se o recebimento está ligado | — |
| F15-03 | empresa com o recebimento desligado | alguém com `configuracao.gerenciar` liga o recebimento | o recebimento passa a valer para os e-mails que chegarem dali em diante | — |
| F15-04 | empresa com o recebimento ligado | alguém com `configuracao.gerenciar` desliga | os e-mails que chegarem dali em diante seguem o F15-05; o que já entrou não muda | — |
| F15-05 | empresa com o recebimento desligado | chega um e-mail ao endereço de entrada dela, de qualquer remetente | nada é gravado (nem solicitação, nem mensagem, nem triagem) e nada é respondido | — |
| F15-06 | pessoa da equipe sem `configuracao.gerenciar` | tenta ligar ou desligar o recebimento | `403 sem-permissao`; nada muda (a tela não oferece o controle) | — |
| F15-07 | empresa só leitura | alguém com `configuracao.gerenciar` tenta ligar ou desligar o recebimento | recusado como gravação da empresa (`422 empresa-somente-leitura`, depois da permissão); nada muda | — |
| F15-08 | empresa de demonstração (RF-NEB-02) | um Dono abre Configurações › Empresa | não há endereço de entrada nem controle de recebimento; e-mail a um endereço que não é de empresa nenhuma segue o F15-22 | — |
| F15-09 | empresas A e B | uma pessoa com vínculo nas duas lê a configuração em cada uma | em cada empresa vê só o endereço e a opção dela; ligar em A não liga em B (isolamento entre tenants) | — |

### Nova solicitação por e-mail (F15e; F15-19 e F15-22 no F15d; F15-20 e F15-24 a F15-26 no F15f; F15-23 no F15g)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F15-10 | recebimento ligado e remetente conhecido | ele manda ao endereço de entrada um e-mail que não é resposta a um aviso | a empresa ganha uma solicitação ABERTA com o próximo protocolo, origem EMAIL, o cliente e o contato do remetente, prioridade MÉDIA, sem responsável e com o contrato como no portal (o único ATIVO vigente do cliente; senão sem SLA) | — |
| F15-11 | o mesmo do F15-10 | o assunto tem até 200 caracteres | o assunto vira o título da solicitação; acima de 200, o título é o assunto cortado em 200 | — |
| F15-12 | o mesmo do F15-10 | o e-mail chega sem assunto (ou só com espaços) | o título é "E-mail sem assunto" e a solicitação é criada | — |
| F15-13 | o mesmo do F15-10 | o e-mail tem corpo em texto, em HTML ou nos dois | a descrição é o texto do corpo (o HTML vira texto, sem marcação nem script), cortada em 10.000 caracteres; corpo vazio deixa a descrição vazia | — |
| F15-14 | o mesmo do F15-10 | o e-mail traz anexos dentro da política (RNF-SEG-07: até 5, até 20 MB cada, tipos permitidos detectados pelo conteúdo), inclusive imagem embutida no corpo (pergunta 4) | os anexos viram a primeira mensagem EXTERNA do contato na solicitação, sem texto, no canal E-mail, como na abertura pelo portal | — |
| F15-15 | o mesmo do F15-10 | parte dos anexos está fora da política (tipo não permitido, acima de 20 MB, além do 5º) | a solicitação é criada com os anexos válidos; uma nota INTERNA lista os recusados com o motivo; o aviso de recebida diz ao contato quais arquivos não entraram (pergunta 4) | — |
| F15-16 | solicitação aberta como no F15-10, empresa liberada para enviar (RF-AUT-06) | o aviso de recebida sai | o contato recebe "Recebemos sua solicitação #N" como resposta ao e-mail dele (na mesma conversa do programa de e-mail), com o protocolo | — |
| F15-17 | solicitação aberta como no F15-10 | a solicitação é gravada | a equipe recebe o aviso interno de nova solicitação (RF-NOT-02) e o sino, como na abertura pelo portal | — |
| F15-18 | empresa **não** liberada para enviar (RF-AUT-06) e remetente conhecido | ele manda um e-mail novo | a solicitação é criada como no F15-10 (receber não é enviar a terceiros), mas o aviso de recebida não sai, como os demais avisos ao contato hoje | — |
| F15-19 | empresa só leitura com o recebimento ligado | chega qualquer e-mail ao endereço de entrada | nada é gravado (nem solicitação, nem mensagem, nem triagem) e nada é respondido, sem revelar a situação da empresa ao remetente (RF-EMP-02) | — |
| F15-20 | o contato com o e-mail X é da empresa B e não é contato da empresa A | ele manda um e-mail ao endereço de entrada de A | nada é criado em B; em A, o e-mail vai à triagem (F15-70) (isolamento entre tenants) | — |
| F15-21 | um e-mail endereçado ao mesmo tempo aos endereços de entrada de A e de B, de remetente conhecido nas duas | ele chega | cada empresa tem a sua solicitação, independente, com o próprio protocolo; nada de uma aparece na outra | — |
| F15-22 | um endereço do domínio de entrada que não é de nenhuma empresa (nunca existiu, ou a empresa foi expurgada) | chega um e-mail a ele | nada é gravado em empresa nenhuma | — |
| F15-23 | o mesmo e-mail entregue duas vezes ao Acordu (reenvio do provedor) | as duas entregas chegam | só uma solicitação é criada (ou uma mensagem, se for resposta, ou um item de triagem) | — |
| F15-24 | o e-mail do remetente é o de contatos ativos de **dois clientes** da mesma empresa | ele manda um e-mail novo | nenhuma solicitação é criada sozinha: o e-mail vai à triagem, marcado como "contato em mais de um cliente", com os clientes possíveis (pergunta 5) | — |
| F15-25 | o remetente é contato **inativo**, ou contato de cliente inativo | ele manda um e-mail novo | nenhuma solicitação é criada: o e-mail vai à triagem | — |
| F15-26 | e-mail cujo remetente declarado é um contato, mas que falha na conferência de autenticidade do domínio do remetente (SPF, DKIM e DMARC, pelo resultado do provedor) | ele chega | nunca vira solicitação nem mensagem sozinho: vai à triagem marcado como "remetente não confirmado" (pergunta 6) | — |
| F15-27 | e-mail do contato que a caixa da própria empresa (ex.: `atendimento@prestador.com.br`) redireciona automaticamente ao endereço de entrada, mantendo o remetente original | ele chega e passa na conferência do F15-26 | é tratado como enviado pelo contato (F15-10) | — |
| F15-28 | solicitação aberta por e-mail pelo contato C1, de escopo PROPRIAS, e o colega C2 do mesmo cliente, também PROPRIAS | os dois abrem o portal | C1 vê a solicitação; C2 não; um contato TODAS_DO_CLIENTE do cliente vê; contato de outro cliente nunca vê (`404` no detalhe) (isolamento por cliente) | — |
| F15-29 | solicitação aberta por e-mail | a equipe abre a lista e o detalhe | a origem aparece como "E-mail" | — |

### Resposta por e-mail na conversa, threading (F15g)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F15-30 | solicitação EM_ANDAMENTO, recebimento ligado | o contato responde, pelo programa de e-mail, a um aviso dela | a resposta entra na conversa como mensagem EXTERNA do contato, no canal E-mail, e o responsável e os seguidores são avisados como numa resposta pelo portal | — |
| F15-31 | solicitação AGUARDANDO_CLIENTE | o contato responde a um aviso por e-mail | a mensagem entra e a solicitação volta a EM_ANDAMENTO, como no portal (RF-POR-05) | — |
| F15-32 | solicitação RESOLVIDA, dentro do prazo do fechamento automático | o contato responde a um aviso por e-mail | a mensagem entra na conversa (RF-SOL-13) e a solicitação continua RESOLVIDA, como uma resposta pelo portal: reabrir é reprovar, com motivo, pelo link do aviso (pergunta 3) | — |
| F15-33 | solicitação FECHADA ou CANCELADA | o contato responde a um aviso dela por e-mail | nada entra nela (RF-SOL-13); abre-se uma solicitação nova para o mesmo contato e cliente, como no F15-10, com "Em continuação à solicitação #N" no início da descrição, e o aviso de recebida da nova sai (pergunta 3) | — |
| F15-34 | solicitação aberta à conversa | a resposta traz anexos | os anexos entram na mensagem com a política do F15-14; os de fora seguem o F15-15 (nota interna e o contato avisado) | — |
| F15-35 | resposta que cita o e-mail anterior (o trecho "Em ..., ... escreveu:" e o texto citado) | ela entra na conversa | a mensagem guarda só o texto novo, sem a citação; a assinatura do contato fica (pergunta 7) | — |
| F15-36 | resposta só com a citação, sem texto novo nem anexo | ela chega | nenhuma mensagem é criada | — |
| F15-37 | e-mail que não é resposta a um aviso do Acordu, mas tem "#N" no assunto | ele chega de remetente conhecido | não entra na solicitação #N: segue as regras de e-mail novo (F15-10) | — |
| F15-38 | solicitação do cliente K, e o colega C2, contato ativo de K, em cópia de um aviso dirigido a C1 | C2 responde por e-mail | a mensagem entra na conversa como do C2, no canal E-mail; os avisos continuam indo só para o contato da solicitação (pergunta 8) | — |
| F15-39 | solicitação do cliente K | chega resposta ao endereço da conversa dela de um remetente que não é contato ativo de K (contato de outro cliente da empresa, pessoa desconhecida ou que falha no F15-26) | nada entra na solicitação; o e-mail vai à triagem com a indicação da solicitação a que ele respondeu (isolamento por cliente) | — |
| F15-40 | endereço da conversa adulterado, vencido por expurgo da solicitação ou que aponta para uma solicitação de outra empresa | chega um e-mail a ele | nada entra em solicitação nenhuma de outra empresa; na empresa do endereço, o e-mail segue as regras de e-mail novo ou de triagem (isolamento entre tenants) | — |
| F15-41 | empresa não liberada para enviar (RF-AUT-06) | o contato responde por e-mail | a mensagem entra; nenhum aviso sai ao contato, como hoje | — |
| F15-42 | empresa só leitura | o contato responde por e-mail | nada é gravado e nada é respondido (F15-19) | — |
| F15-43 | recebimento desligado depois de um aviso enviado com o recebimento ligado | o contato responde a esse aviso | nada é gravado (F15-05) | — |
| F15-44 | mensagem do contato que entrou por e-mail | o contato abre a solicitação no portal (conta ou link) | vê a mensagem como dele, com os anexos, no canal E-mail (F15-61) | — |

### Avisos pelo canal de origem (F15c; F15-50 no F15e; F15-56 no F15g)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F15-50 | solicitação de origem EMAIL | sai qualquer aviso do RF-NOT-01 | o aviso vai por e-mail ao contato, na mesma conversa do e-mail que abriu a solicitação, com o protocolo no assunto, e responde para o endereço da conversa | — |
| F15-51 | solicitação de origem INTERNA ou PORTAL e recebimento ligado | sai um aviso | o aviso vai por e-mail, como hoje, e responde para o endereço da conversa: responder a ele cai no F15-30 | — |
| F15-52 | recebimento desligado | sai um aviso de qualquer solicitação | o aviso sai exatamente como hoje (o Reply-To é o "responder para" da empresa, se houver; pergunta 2) | — |
| F15-53 | aviso de mensagem da equipe | ele sai | leva só o texto da mensagem EXTERNA; nota INTERNA nunca gera aviso nem aparece em e-mail ao contato (RNF-SEG-03) | — |
| F15-54 | contato sem conta no portal | sai um aviso de solicitação de origem EMAIL | o aviso traz o link de acesso sem senha, como hoje (renovado a cada e-mail), para quem quiser aprovar ou acompanhar pelo portal | — |
| F15-55 | qualquer aviso ao contato | ele sai | vai marcado como mensagem automática (`Auto-Submitted`), para que respostas automáticas do outro lado não voltem como mensagem (F15-86) | — |
| F15-56 | a resposta do contato que entrou por e-mail | ela é gravada | o próprio contato não recebe aviso dela, como na resposta pelo portal | — |

### Canal na conversa (F15a)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F15-60 | solicitação com mensagens da equipe, do portal e do e-mail | a equipe abre a conversa | cada mensagem mostra o canal (Acordu, Portal ou E-mail); a API da conversa da equipe traz o canal de cada mensagem | — |
| F15-61 | a mesma solicitação | o contato abre a conversa no portal | cada mensagem EXTERNA mostra o canal; nenhuma INTERNA aparece (RF-POR-04, pergunta 11) | — |
| F15-62 | mensagens gravadas antes do F15 | a conversa abre | as do contato aparecem no canal Portal e as da equipe no canal Acordu | — |

### Triagem dos e-mails não identificados (F15h; F15-70 no F15f; F15-71 e F15-72 no F15j)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F15-70 | recebimento ligado e remetente não identificado (F15-20, F15-24 a F15-26, F15-39) | o e-mail chega | entra na triagem da empresa com remetente, assunto, data, o texto e os anexos dentro da política, e o motivo; nada é respondido ao remetente | — |
| F15-71 | itens na triagem | alguém com `solicitacao.criar` abre a triagem | vê os itens da empresa, do mais novo, com o motivo de cada um; a casca mostra quantos aguardam | — |
| F15-72 | pessoa da equipe sem `solicitacao.criar` (ex.: Leitor) | tenta ler ou tratar a triagem | `403 sem-permissao`; o menu não mostra a triagem | — |
| F15-73 | item na triagem | alguém com `solicitacao.criar` o converte em solicitação, escolhendo um cliente ativo e um contato ativo dele | nasce a solicitação como no F15-10 (origem EMAIL, assunto, corpo e anexos do e-mail, contato escolhido), o item sai da triagem e o aviso de recebida vai ao contato escolhido | — |
| F15-74 | item na triagem com remetente que não é contato do cliente escolhido | quem converte pede para cadastrar o remetente como contato do cliente | o contato nasce com o nome e o e-mail do remetente (regras do RF-CLI-02: e-mail único entre os ativos do cliente) e a solicitação sai com ele; os próximos e-mails dele são de remetente conhecido | — |
| F15-75 | item na triagem | alguém com `solicitacao.criar` e `solicitacao.comentar` o junta a uma solicitação aberta à conversa, de um cliente ativo, escolhendo o contato | o texto e os anexos entram como mensagem EXTERNA desse contato, no canal E-mail, com os efeitos do F15-30 e do F15-31; o item sai da triagem | — |
| F15-76 | item na triagem | alguém com `solicitacao.criar` o descarta | o item e os anexos dele são apagados; nada é respondido ao remetente | — |
| F15-77 | item na triagem há 30 dias sem ser tratado (pergunta 9) | roda o diário | o item e os anexos dele são apagados | — |
| F15-78 | itens na triagem das empresas A e B | uma pessoa de A lista, abre, converte ou descarta pelo id de um item de B | a lista de A não mostra os de B; pelo id, `404` (isolamento entre tenants) | — |
| F15-79 | contato do portal (conta ou link) | tenta alcançar a triagem | `403`, como as demais rotas da equipe (ADR 0002, item 4) | — |
| F15-80 | empresa só leitura | alguém com a permissão lê a triagem / converte, junta ou descarta | a leitura responde como antes; converter, juntar e descartar são recusados como gravação da empresa (`422 empresa-somente-leitura`), sem mudar nada | — |
| F15-81 | duas pessoas tratam o mesmo item ao mesmo tempo | as duas confirmam | só uma ação vale; a outra recebe a recusa de que o item já foi tratado, sem segunda solicitação nem mensagem | — |

### Respostas automáticas, spam e limites (F15d; F15-86 no F15g; F15-90 a F15-92 no F15i)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F15-85 | e-mail automático (resposta automática, aviso de entrega, mensagem em massa) | ele chega ao endereço de entrada | é ignorado: nem solicitação, nem mensagem, nem triagem, e nada é respondido | — |
| F15-86 | aviso enviado ao contato, que tem resposta automática de férias ligada | a resposta automática chega ao endereço da conversa | é ignorada como no F15-85: a conversa não ganha mensagem e nenhum novo aviso sai (sem laço) | — |
| F15-87 | e-mail que o filtro do provedor de entrada classifica como spam ou com vírus | ele chega | é descartado sem entrar na triagem (pergunta 6); só a contagem fica no log, sem endereço nem conteúdo (RNF-OPS-01) | — |
| F15-88 | e-mail cujo remetente é um endereço do próprio Acordu (o remetente dos avisos ou um endereço de entrada) | ele chega | é ignorado, nada é respondido (sem laço entre empresas) | — |
| F15-89 | e-mail acima do tamanho máximo total (pergunta 10) | ele chega | nada é gravado; o remetente recebe a recusa de entrega do servidor de e-mail, sem mensagem do Acordu | — |
| F15-90 | remetente que já abriu, nesta hora, o máximo de solicitações por e-mail na empresa (pergunta 10) | ele manda mais um e-mail novo | o excedente não abre solicitação nem vai à triagem; a equipe é avisada uma vez no sino de que houve e-mails retidos pelo limite | — |
| F15-91 | empresa que já recebeu, no dia, o máximo de e-mails (pergunta 10) | chega mais um | é descartado como no F15-90; as outras empresas recebem normalmente (isolamento entre tenants) | — |
| F15-92 | solicitação que já recebeu, nesta hora, o máximo de respostas por e-mail (pergunta 10) | chega mais uma | a resposta não entra e nenhum aviso sai | — |

## 3. Fora de escopo

- WhatsApp como canal de entrada e de aviso: é o #184, que aproveita o canal da mensagem (F15-60) e os avisos pelo canal de origem (RF-NOT-06).
- Ler uma caixa existente do prestador por IMAP ou POP, ou caixa compartilhada: a entrada é só pelo endereço do Acordu (a caixa do prestador pode redirecionar para ele, F15-27).
- Enviar os avisos com o domínio do prestador no "De" (domínio próprio verificado): o "De" continua no domínio do Acordu, com o nome do prestador.
- Trocar ou personalizar o endereço de entrada (pergunta 1).
- A equipe responder por e-mail (aviso interno respondido vira mensagem): a equipe responde pelo app.
- Pessoas em cópia virarem participantes da solicitação ou receberem os avisos: os avisos continuam indo só para o contato da solicitação (F15-38).
- Guardar e mostrar o e-mail original completo (HTML, cabeçalhos, citação): a mensagem guarda o texto novo e os anexos (F15-35).
- Remover a assinatura do contato (F15-35).
- Regras de roteamento por assunto, categoria ou responsável a partir do e-mail; prioridade lida do e-mail.
- Aprovar ou reprovar a entrega respondendo ao e-mail: continua pelo link do aviso (F15-32).

## 4. Perguntas ao Daniel (G2)

1. **Endereço de entrada.** Recomendo **um endereço por empresa, gerado pelo Acordu** num subdomínio de entrada (ex.: `k7m2qd@entrada.acordu.app.br`), com uma parte aleatória curta e não derivada do nome, para não ser adivinhado por quem quer mandar spam ou se passar por cliente; **desligado por padrão**, ligado por quem tem `configuracao.gerenciar` (F15-01 a F15-05), para quem já usa o "responder para" não ter a mudança sem pedir. Trocar o endereço fica fora (quem vazar pede à Nebrys). Opções: endereço legível pelo nome (`padaria-bom-grao@...`, mais fácil de ditar, adivinhável); ligado para todas as empresas desde o início; botão "gerar outro endereço".
2. **"Responder para" com o recebimento ligado.** Hoje os avisos vão com o Reply-To no "responder para" da empresa (F5a), e a resposta do contato cai na caixa do prestador. Recomendo: **ligado o recebimento, os avisos respondem para o endereço da conversa** (a resposta entra na solicitação, F15-51) e o "responder para" continua só nos convites e no link do portal; desligado, nada muda (F15-52). Opção: manter o "responder para" e pôr o endereço da conversa em cópia (a resposta vai aos dois, e o prestador pode responder de fora do Acordu, sem registro).
3. **Resposta a solicitação RESOLVIDA, FECHADA ou CANCELADA.** Recomendo: **RESOLVIDA** recebe a mensagem e não reabre, como no portal (F15-32): reabrir é reprovar com motivo; **FECHADA ou CANCELADA** abre uma **solicitação nova** para o mesmo contato, citando a anterior (F15-33), porque o cliente que responde a um e-mail velho costuma ter um assunto novo e não deve ficar sem resposta. Opções: na RESOLVIDA, a resposta por e-mail reabre (o texto vira o motivo); na FECHADA ou CANCELADA, recusar com um e-mail automático dizendo para escrever um e-mail novo, ou mandar à triagem.
4. **Anexos fora da política e imagens embutidas.** Recomendo **aceitar o e-mail com os anexos válidos**, registrar os recusados numa nota interna e dizer ao contato no aviso de recebida quais não entraram (F15-15), e tratar **imagem embutida no corpo como anexo comum** (pode ser a captura de tela do problema), mesmo que traga junto o logotipo da assinatura. Opções: recusar o e-mail inteiro quando um anexo está fora; ignorar imagens embutidas pequenas (abaixo de um tamanho) para não trazer logotipos.
5. **Contato em mais de um cliente** (o RF-CLI-02 só exige e-mail único dentro do cliente). Recomendo **mandar à triagem** com os clientes possíveis (F15-24), e a equipe escolhe; a resposta a um aviso não tem ambiguidade, porque o endereço da conversa já diz a solicitação. Opções: abrir no cliente da solicitação mais recente desse contato; impedir o mesmo e-mail em dois clientes da empresa (mudaria o RF-CLI-02 e os cadastros que já existem).
6. **Remetente não identificado, autenticidade e spam.** Recomendo **triagem** para o remetente não identificado (desconhecido, contato inativo, mais de um cliente, autenticidade que falha; F15-70), sem resposta automática a ele (evita mandar e-mail a endereços forjados, o *backscatter*), e **descarte sem triagem** só para spam e vírus pelo filtro do provedor (F15-87). Opções: criar o contato sozinho num cliente "a identificar"; descartar o desconhecido (o e-mail se perde sem o prestador saber); responder ao desconhecido com "use o portal ou fale com o prestador"; mandar spam à triagem com a marca.
7. **Citação e assinatura.** Recomendo **tirar a citação do e-mail anterior** da resposta (F15-35; senão cada mensagem repete a conversa inteira) e **manter a assinatura** (não há marca confiável, e cortar errado perde texto do cliente); resposta só com citação não vira mensagem (F15-36). Opções: guardar o texto inteiro, com a citação; tentar tirar também a assinatura.
8. **Colega do mesmo cliente respondendo.** Recomendo **aceitar a resposta de qualquer contato ativo do cliente da solicitação** (F15-38; é comum o colega em cópia responder), com o nome dele na mensagem, e mandar à triagem o resto (F15-39). Isso deixa um contato de escopo PROPRIAS escrever numa solicitação que no portal ele não vê, sem passar a vê-la. Opções: só o contato da solicitação (o colega cai na triagem); qualquer contato do cliente só se ele for TODAS_DO_CLIENTE.
9. **Retenção da triagem.** Recomendo **30 dias** sem tratamento e o diário apaga com os anexos (F15-77): é dado pessoal de quem talvez nem seja cliente. Opções: 7 ou 90 dias; sem prazo.
10. **Limites.** Recomendo: e-mail de até **25 MB** no total (F15-89); por remetente, até **10 solicitações novas por hora** em cada empresa (F15-90); por empresa, até **300 e-mails por dia** (F15-91, folga no plano gratuito do provedor e contra laço); por solicitação, até **30 respostas por e-mail por hora** (F15-92). Excedente descartado, com um aviso no sino por hora (F15-90), sem responder ao remetente. Opções: valores diferentes; mandar o excedente à triagem.
11. **Canal no portal.** Recomendo **mostrar o canal também ao contato** (F15-61): ele entende por que a mensagem que mandou por e-mail aparece ali. Opção: só a equipe vê o canal.
