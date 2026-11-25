# /plano do F15 — 1ª conferência (25/11)

Parecer: reprovado (graves 1 a 5). Decisão do Daniel: "siga com todos" (corrigir todos os itens).

## Graves
1. F15-74 (spec.md ~120; design.md ~80, D10): "cadastrar o remetente como contato" só com `solicitacao.criar` é escalonamento — criar contato exige `cliente.gerenciar` (contrato `POST /clientes/{id}/contatos`; segurança §3.1). A ação exige também `cliente.gerenciar`; cenário de negação (403, nada muda).
2. F15-71/F15-72 no PR errado (design ~221/223): as rotas da triagem nascem no F15h; o 403 do Leitor e a lista pela API vão para o F15h. O F15j fica com menu e casca.
3. F15-81 (spec ~127; design ~77): dois resultados (409 `triagem-ja-tratada` na corrida do xmin; 404 depois de apagado). Decidir e escrever os dois casos. As rotas novas não usam `If-Match` (contrato §3) — dizer por quê.
4. Design tira `Re:`/`Fwd:`/`Enc:` do assunto (design ~145), mas F15-11 diz que o assunto vira o título. Alinhar (recomendado: o cenário ganha a regra).
5. Faltam cenários de erro das ações da triagem: cliente/contato inativo; contato que não é do cliente; e-mail do remetente já de contato ativo do cliente (RF-CLI-02, 422); juntar a FECHADA/CANCELADA (409 `solicitacao-encerrada`); ids de cliente/solicitação de outra empresa ou de outro cliente (404).

## Menores
6. F15-79 já está em 403 na spec; o design (F15h ~221, histórico ~246) e `notas/arquiteto.md` ainda o tratam como pendente.
7. F15-38 (colega do mesmo cliente responde) é exceção ao RNF-SEG-02 (ERS §6.2: contato PROPRIAS só vê as próprias). Marcar a exceção na ERS (ou ajustar conforme a pergunta 8).
8. Idempotência: F15-23 só no F15g; cada caminho (abertura no F15e, triagem no F15h) precisa do seu cenário de reenvio no PR que o cria.
9. D8 (design ~56) "equipe só ACORDU" contradiz a nota do sistema com canal EMAIL (design ~146, `AutoresDoSistema`). Abrir exceção para o autor do sistema; cenário de que essa nota nunca aparece no portal nem em aviso.
10. Comportamento no design sem cenário: marcadora "— Escreva sua resposta acima desta linha —" (D13, F15c); `422 transicao-invalida` ao ligar sem `EmailEntrada__Dominio`; sino no retido pelo limite diário (F15-91 ambíguo).
11. F15-33 ambíguo: a nova solicitação (encerrada → nova) é do respondente (colega do F15-38) ou do contato original?
12. RF-AUT-06: dizer o que acontece com o aviso de recebida na conversão (F15-73) e na junção (F15-75) quando a empresa não está liberada; e se o aviso interno do F15-17 respeita liberação e preferências (RF-NOT-04).
13. RF-EMP-02 e jobs: o passo `expurgo-email-entrada` (design ~153) roda em empresa só leitura? Cenário em F15-77.
14. Requisitos sem cenário: trilha `configuracao.recebimento-email` e `triagem.*` (F15-03/04, F15-73–76); RF-SOL-09 (resposta por e-mail em RESOLVIDA zera o prazo?); cabeçalho da spec sem RNF-SEG-08, RNF-OPS-01, RF-NOT-03, RF-NOT-04; RNF-SEG-11 (log sem endereço) só no F15-87.
15. Testabilidade: F15-16/F15-50 ("mesma conversa no programa de e-mail") → verificar por cabeçalhos `In-Reply-To`/`References`, Gmail à mão; F15-89 é teste do Worker (413), dizer no cenário; F15-26/27 dependem do carimbo de autenticação do Email Routing (risco do D11; spike recomendado antes do G2, fica com o Daniel).
16. PRs grandes: F15e (14 cenários), F15g (18), F15d (Worker+endpoint+MIME+EmailRecebido+provisionamento+expurgo). Sugestão: F15e em abertura e anexos; F15g em resposta e encerrada→nova; aliviar o F15d. A cadeia d→e→f→g→h→i/j é em série: dizer que cada um mergeia antes do próximo.
17. F15-02/F15-08 só no F15j: o `GET /configuracoes/empresa` com os campos e a ausência do endereço na demonstração nascem no F15b sem cenário de API.
18. Migration do F15b sorteia o código com `random()` (não criptográfico) e o texto promete "não se adivinha": gerar em C# ou ajustar o texto.
19. Candidatos a ADR/convenção: autor do sistema na `Interacao` (`AutoresDoSistema`); rotas `/internal/*` com 202 sem motivo; limites e retenção do registro de entrada; CLAUDE.md cita o ADR 0016 depois do G2.
20. Mistura spec × design: a spec traz mecanismo (F15-26 cita SPF/DKIM/DMARC; ~28 lista cabeçalhos que o D12 repete e já diverge; F15-55 nomeia `Auto-Submitted`); o design traz comportamento observável (404 do item tratado, 422 de domínio ausente, marcadora) que deve virar cenário.
