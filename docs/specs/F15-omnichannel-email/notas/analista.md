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

## Devolução de 25/11 (F15-79)

- F15-79 trocado de `404` para `403`, "como as demais rotas da equipe (ADR 0002, item 4)". Motivo: decisão do Daniel no chat ("Troca para 403"), a partir da dúvida do arquiteto (`notas/arquiteto.md`); as rotas da equipe já respondem `403` ao contato (ADR 0002 item 4, contrato §14).
- Conta e link valem igual, por isso o texto é um só: a sessão de link também recebe `403` nas rotas da equipe (contrato §6d3, "rotas sem ✓L → 403"; §14). No código, a política `Politicas.Equipe` recusa qualquer token com `cliente_id`, que o de link também tem; `PortalLinkTests` confere `/api/v1/clientes` → `403` com a sessão de link.

## Devolução do /plano, 1ª (25/11, "siga com todos")

Itens da spec do `notas/plano-1.md`; os de design (2, 6, 16, 18, 19) ficam com o arquiteto. Numeração mantida; números novos: F15-45, F15-46, F15-57, F15-82 a F15-84, F15-93 a F15-109.

- 1: F15-74 exige também `cliente.gerenciar` (contrato §4.2, `POST /clientes/{id}/contatos`); F15-82 é a negação (`403`, nada muda, converter com contato existente segue liberado). RF-SOL-15 da ERS ganhou a permissão.
- 3: F15-81 é a corrida (`409 triagem-ja-tratada`, como no D10 do design); F15-83 é o item que já saiu (`404`). Sem `If-Match` é do design.
- 4: F15-11 tira os prefixos `Re:`/`Fwd:`/`Enc:` repetidos (alinhado ao design §1.2); F15-12 cobre o assunto que fica vazio depois disso.
- 5: F15-84 (cliente inativo, `422 transicao-invalida`, como na reativação de contato), F15-99 (e-mail já de contato ativo, `422` do RF-CLI-02), F15-100 (`409 solicitacao-encerrada`), F15-101 (cliente ou solicitação de outra empresa, `404`), F15-102 (contato inativo ou de outro cliente, `400` no campo, como no link por solicitação, contrato §7).
- 7: RNF-SEG-02 da ERS ganhou a exceção (a confirmar com a pergunta 8), e a linha de teste do RNF-SEG-02 diz que responder não dá visão; F15-38 cita.
- 8: F15-23 ficou só com a resposta; F15-97 (abertura) e F15-103 (chegada à triagem).
- 9: termo "nota do sistema" e F15-98 (nunca no portal nem em aviso; a equipe a vê como do sistema). F15-15 passa a dizer "nota do sistema".
- 10: F15-57 (marcadora no aviso, só com o recebimento ligado) e F15-46 (resposta acima da marcadora); F15-94 (`422` sem domínio configurado); F15-90 a F15-92: todo retido conta para o mesmo aviso no sino, no máximo um por hora, a quem tem `solicitacao.criar` (como o D12).
- 11: decidido pelo mais coerente, como recomendado no handoff: a solicitação nova do F15-33 é de **quem respondeu** (o contato da solicitação ou o colega do F15-38), porque é quem escreveu e quem recebe o aviso de recebida. RF-SOL-11 e pergunta 3 alinhados. O Daniel confirma no G2 com a pergunta 3.
- 12: F15-17 diz sino sempre, e-mail interno com preferências (RF-NOT-04) e liberação (RF-AUT-06, só o criador antes). F15-105 (conversão sem liberação: sem aviso de recebida) e F15-106 (junção: nada ao contato; equipe como no F15-30).
- 13: F15-107 e F15-108: os expurgos por prazo rodam também na empresa só leitura, como os que já existem (trilha, sino, atividade: `ExpurgoAtividades` apaga de todos os tenants; segurança §5, "gravações fora do change tracker que seguem no só leitura: … os expurgos"). ADR 0015 item 4 tira dos jobs por tenant só recorrências, alertas, cronômetros e fechamento automático; RF-EMP-02 cita esses. A ERS não mudou.
- 14: F15-93 e F15-104 (trilha); F15-45 (RF-SOL-09: a resposta não mexe no prazo do fechamento, contado de `ResolvidaEm`, modelo §4.3); cabeçalho com RF-SOL-09, RF-NOT-03, RF-NOT-04, RNF-SEG-05, RNF-SEG-08, RNF-OPS-01 e ADR 0008; F15-109 (log sem endereço nem conteúdo, API e adaptador).
- 15: F15-16 e F15-50 verificados pelos cabeçalhos de resposta em teste, Gmail e Outlook à mão no primeiro deploy; F15-89 verificado no teste do adaptador de entrada e na API; F15-26 e F15-27 dizem o que se observa (item na triagem com o motivo; solicitação com o contato original) e como se verifica (resultado simulado em teste, e-mail real no primeiro deploy). O spike do carimbo antes do G2 fica com o Daniel.
- 17: F15-95 (o `GET` da configuração traz endereço e opção a qualquer pessoa da equipe) e F15-96 (demonstração sem endereço; ligar `422 transicao-invalida`, como o design D9).
- 20: saíram da spec SPF/DKIM/DMARC (F15-26, termo "remetente conhecido"), a lista de cabeçalhos do "e-mail automático" e o `Auto-Submitted` (F15-55). Termo novo "registro de entrada", sem dizer o que guarda nem o prazo. O RNF-SEG-11 da ERS ainda cita SPF/DKIM/DMARC e `Auto-Submitted`: não mexi (não estava no pedido).
- F15-108 depende do termo "registro de entrada"; o prazo (7 dias no D6) é do design.

## Devolução do /plano, 2ª conferência (26/11)

Três ajustes de uma linha, sem mudar comportamento:

- F15-06: saiu o parêntese "a tela não oferece o controle" (o controle nasce no F15o, o cenário é do F15b). Nenhum cenário de tela dizia isso, então o F15-02 (F15o) ganhou: o controle de ligar e desligar só aparece a quem tem `configuracao.gerenciar`. O F15-07 não tinha o parêntese.
- F15-81: `409`, recusa de que o item já foi tratado; o nome do tipo fica no design (D10).
- F15-16: assunto `Re: {assunto do e-mail dele} (#N)` e texto começando por "Recebemos sua solicitação #N", marcado como resposta ao e-mail dele (como o F15-50), sem nomear cabeçalho; coerente com o D4 do design. Verificação: assunto, texto e cabeçalhos de resposta por teste; Gmail e Outlook à mão.
