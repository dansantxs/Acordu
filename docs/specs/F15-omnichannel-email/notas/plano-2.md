# /plano do F15 — 2ª conferência (26/11)

Parecer: aprovado com ressalvas, sem graves. Itens do `plano-1.md`: todos resolvidos (2 e 16 de outro jeito, ver design; 19 com as edições do orquestrador no PR do G2).

Corrigidos antes do G2 (analista, 26/11): menores 1 a 3 (F15-06 sem o parêntese de tela, que foi para o F15-02; F15-81 sem o nome do tipo; F15-16 com assunto e texto).

Ficam como ressalvas para os PRs de código (cada um cai no PR que trata o assunto; o arquiteto revisa ao abrir o PR):
4. F15h: os testes leem o registro de entrada pelo repositório (a lista pela API só vem no F15k).
5. `aviso-anexos-recusados`: vai ao autor (contradiz o F15-38 quando o autor é o colega?), se respeita a empresa não liberada (F15-41) e entrar no termo "Aviso" da spec.
6. Casos sem cenário: corpo inválido da conversão (sem contato e sem `cadastrarRemetente`, ou com os dois → 400); junção a cliente inativo; baixar anexo da triagem de outro tenant/item ou de item já tratado; MIME malformado (a rota não pode responder 5xx, senão o servidor de origem reenvia por dias); vários `From`; resposta de contato de cliente inativo a solicitação aberta.
7. Limites: teto de volume/bytes da triagem de remetente não identificado; `IGNORADO` conta nos 300/dia?; F15-91 diz "nada gravado" mas o D6 grava o registro RETIDO.
8. `PUT /configuracoes/recebimento-email`: `If-Match` opcional como os outros PUT de configuração? Ligar o que já está ligado?
9. F15f e F15i com 13 cenários cada (os maiores).
10. ERS: RF-SOL-11 cita "perguntas 1 a 10" (são 11); RF-SOL-14 mostra o canal ao contato (pergunta 11) sem "a confirmar no G2".
11. Entre o F15i e o F15j a resposta guarda a citação, que pode conter o link de acesso sem senha do aviso (só a empresa com o recebimento ligado).
