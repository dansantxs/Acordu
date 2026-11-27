# Notas do desenvolvedor — F15e (etapa 4)

Premissas técnicas do PR dos descartes automáticos, do laço, do filtro do provedor e do expurgo do registro de entrada (F15-85, F15-87, F15-88, F15-108; design D6, D11, D12 e D16; ADR 0016, itens 9 e 11; ADR 0015, itens 3 e 4). Sessão sem SDK .NET: o backend foi lido com cuidado, mas só compila e roda na CI.

## Descartes (Application)

- **Onde:** `DescarteDeEntrada.Avaliar` (função pura, `Application.EntradaEmail`), chamada pelo `ComandosEntradaEmail` depois da conferência de repetição e antes de gravar. Ordem do D12: automático → laço → filtro (o log mostra o primeiro que casar). A repetição vem antes: o automático repetido continua `Repetida`, sem segunda gravação.
- **Descarte registrado:** os três gravam o `EmailRecebido` como `IGNORADO` (D6 já previa esse resultado para automático, laço e filtro) e devolvem os desfechos novos `Automatica`, `Laco` e `Filtrada`, que só aparecem no log (`Entrada por e-mail: {Desfecho}.`). A rota responde o mesmo `202` sem corpo. Até o F15f o e-mail comum também fica `IGNORADO`; a diferença observável neste PR é o desfecho no log, e é isso que os testes da Api conferem junto com "nada criado, nada respondido".
- **Sinais do automático** (lista do D12, única fonte): `LeitorMime` preenche `SinaisDeAutomatico` (`Auto-Submitted`, `Precedence`, presença de `List-Id`/`List-Unsubscribe` e de `X-Autoreply`/`X-Autorespond`, tipo MIME do corpo). `Auto-Submitted` vale pelo valor antes de um `;`; vazio conta como ausente; `no` segue.
- **"noreply de sistema de entrega":** li como a parte local `noreply` ou `no-reply` (sem diferenciar maiúsculas), no envelope ou no `From`, ao lado de `mailer-daemon` e `postmaster`. Ninguém escreve de um `noreply` esperando resposta; `do-not-reply` e variações ficaram de fora para não ir além da lista do design.
- **Envelope:** vem de `X-Email-Remetente` (o Worker já manda). Presente e vazio, ou `<>`, é devolução. Ausente (cabeçalho que não veio) não conta como vazio: sem o sinal. A Api distingue as duas coisas por `TryGetValue`.
- **Laço:** domínio do `From` ou do envelope igual (sem diferenciar maiúsculas) ao `EmailEntrada__Dominio` ou ao domínio de `Email__Remetente` (padrão `nao-responda@send.acordu.app.br`). Comparação exata, sem subdomínios: o domínio raiz (`acordu.app.br`, o do `contato@`) não é laço. O domínio dos avisos entrou em `OpcoesEmailEntrada.DominioDosAvisos`, lido na composição com o mesmo padrão do envio; remetente sem domínio reconhecível deixa só a conferência do domínio de entrada, sem impedir a subida (a variável não é desta configuração).
- **Filtro:** `X-Email-Filtro` `spam` ou `virus`, aparado e sem diferenciar maiúsculas; outro valor ou nenhum segue. O Worker continua mandando sempre `ok` (D11, risco aceito no G2); o Worker não mudou neste PR.
- **F15-86** (resposta automática ao endereço da conversa) é do F15i, mas a mesma função já a descarta: o descarte vem antes de olhar o token do destino.

## Expurgo (Infrastructure e Api)

- `Persistence.EntradaEmail.ExpurgoEmailEntrada`: `ExecuteDelete` sobre `EmailRecebido` com `IgnoreQueryFilters([Tenant, Cliente])`, `RecebidoEm < agora − DiasRegistro`, num escopo próprio, todos os tenants de uma vez, fora do `ExecutorPorTenant` (D16). Não passa pela guarda de só leitura (é `ExecuteDelete`), que é o que o F15-108 pede. Teste de arquitetura novo: `IgnoreQueryFilters<EmailRecebido>` só em `Persistence.EntradaEmail`.
- **Registro:** o expurgo depende de `OpcoesEmailEntrada` (registrada na `ComposicaoApi`), então foi registrado lá, ao lado do `RecebedorDeEmail`, e não em `AddInfrastructure` (o `ValidateOnBuild` dos testes de integração falharia sem as opções). O teste de integração o constrói à mão.
- **Posição no diário:** "depois de `expurgo-notificacoes`" do design; pus logo depois de `expurgo-atividades`, que é o par do feed do mesmo endpoint (o deploy-ci descreve os dois como um passo). Contagem `emailsRecebidosRemovidos` na resposta e "e-mails recebidos" no resumo do #deploy; os índices das contagens seguintes andaram uma posição. O F15k acrescenta a triagem no mesmo passo (`triagemRemovida`).
- **`EmailEntrada__DiasRegistro`:** padrão 7, de 1 a 90 (teto meu: é retenção curta de dado sobre quem escreveu); valor presente e inválido impede a subida. Opcional, sem mudança no `deploy.yml` (como o `TamanhoMaximoBytes`).
- **Sem migration:** a remoção por `recebido_em` varre a tabela, que guarda só 7 dias. O índice por `(tenant_id, recebido_em)` fica para o F15n, se as contagens dos limites pedirem.

## Documentos

- Contrato §18 (descartes, ordem, retenção, desfechos no log); deploy-ci §5 (passo e contagem) e configuração (`EmailEntrada__DiasRegistro`); segurança §3 (expurgo entre as gravações que seguem no só leitura), §6 e §9; convenções §1 (expurgo de retenção de dado da empresa em todos os tenants, D16) e §3/§5 (descartes); modelo de domínio §4.15; estrutura do repositório; `estado.md`; coluna Teste da spec.
- ADR 0010 (tabela histórica do diário) não muda: ADR aceito. Read-side: nada (nenhuma consulta nova). Fluxo BPMN: nenhum passo, papel ou status do fluxo muda (o diário não é modelado passo a passo). OpenAPI e tipos do front: a rota de entrada e os jobs estão fora do OpenAPI; nenhum DTO mudou.

## Menores do `/plano` (`plano-2.md`)

- Nenhum dos 4 a 11 é do F15e (triagem, limites, configuração, ERS e resposta). O 7 ("`IGNORADO` conta nos 300/dia?") fica para o F15n: o registro dos descartes deste PR é a base dessa contagem.
