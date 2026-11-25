# Notas do analista — F15 (etapa 2)

Premissas usadas na spec (25/11/2026). O corpo da issue #183 não foi lido pelo `gh` (fora do papel); valeu o resumo do handoff.

- Hoje todos os avisos ao contato já saem por e-mail, qualquer que seja a origem (`AvisosAoContato`), com Reply-To no "responder para" da empresa (`Tenant.ResponderPara`, F5a). "Aviso pelo canal de origem" no F15 é, na prática: origem EMAIL segue no fio do e-mail original, e com o recebimento ligado todo aviso responde para o endereço da conversa. O mecanismo por canal (interface para o #184) é do design; na ERS ficou como RF-NOT-06.
- A abertura pelo portal é o modelo da abertura por e-mail: prioridade MEDIA, sem responsável, contrato = único ATIVO vigente do cliente, anexos como primeira interação EXTERNA do contato sem texto (`ComandosPortal.AbrirSolicitacaoAsync`).
- Resposta pelo portal em RESOLVIDA só acrescenta mensagem (não reabre); a resposta por e-mail segue igual (pergunta 3).
- O e-mail do contato é único só dentro do cliente (modelo de domínio §3.3), então o mesmo endereço pode estar em dois clientes da empresa: daí a pergunta 5.
- Autor da interação vinda por e-mail: o contato (como na sessão de link, em que o autor é o Id do contato). Detalhe de modelagem fica para o design.
- Triagem visível a quem tem `solicitacao.criar` (Dono e Membro; Leitor não), por ser a decisão de abrir solicitação.
- Empresa de demonstração não tem endereço de entrada (RF-NEB-02: não envia para fora; receber também não faz sentido sem contatos reais).
- Clientes de exemplo (F14r) têm contatos em `.invalid`: nenhum e-mail real chega deles; nenhum cenário próprio.
- Valores (25 MB, 10/h por remetente, 300/dia por empresa, 30/h por solicitação, 30 dias de triagem) são recomendação para o G2, não decisão.
- Não decidido aqui (é do arquiteto): provedor de entrada (Cloudflare Email Routing + Worker ou inbound do Resend), formato do webhook, forma do endereço da conversa (sub-endereço com token, cabeçalhos `In-Reply-To`/`References`), como se lê o resultado de SPF/DKIM/DMARC, como se tira a citação, tipos de erro e rotas.
- `docs/estrategia-produto-acordu.md` §8 ainda lista "Entrada por e-mail" na v2: não mexi (não é documento de requisito); o orquestrador ou o arquiteto podem ajustar no mesmo PR. O contrato da API (§15, "entradas previstas para a v2") também fica para o design.
