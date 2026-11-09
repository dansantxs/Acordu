# Spec — F11: fluxo sem retrabalho entre solicitação, tarefas e projeto

| | |
|---|---|
| **Status** | Em implementação |
| **Data** | 06/11/2026 |
| **Requisitos** | RF-SOL-03, RF-SOL-06, RF-SOL-07, RF-SOL-08, RF-SOL-10, RF-SOL-13 (novo), RF-TAR-01, RF-TAR-02, RF-PRJ-01, RF-POR-05 (ERS) |
| **ADRs** | 0011 |

## 1. Objetivo

Solicitação, tarefas e projeto passam a andar juntos. Quem conclui a última tarefa pode resolver a solicitação na mesma ação, e quem resolve vê as tarefas que ficaram abertas. A resposta ao cliente vira o padrão. Solicitação encerrada não recebe mais conversa nem anexo, e um projeto nasce da solicitação com as tarefas dela. Pedido do Daniel em 06/11, depois de usar o sistema.

## 2. Situação hoje (lida no código em 06/11)

- **Conversa:** a nova interação começa como "Nota interna" (`Conversa.tsx`), como manda o RF-SOL-06 ("padrão: INTERNA").
- **Resolver com tarefas abertas:** `Solicitacao.Resolver` não olha as tarefas. Dá para resolver com tarefas abertas, sem aviso.
- **Última tarefa concluída:** concluir a última tarefa de uma solicitação não sugere nada. O evento `TarefaConcluida` existe, mas não tem quem o trate para a solicitação.
- **Conversa em encerradas:** `RegistrarComentarioAsync` (equipe) e `ResponderAsync` (portal) aceitam interação e anexo em qualquer status, inclusive FECHADA e CANCELADA. A `PrimeiraRespostaERetomada` só deixa de mudar o status.
- **Projeto:** a solicitação só cria tarefa (`JanelaNovaTarefa` com origem). Não há "projeto a partir da solicitação", e `Tarefa` não tem método para trocar de projeto depois de criada (`ProjetoId` só na criação).
- **Outros pontos do mesmo tipo:**
  - criar tarefa, iniciar o cronômetro ou responder ao cliente não tira a solicitação de ABERTA;
  - iniciar o cronômetro não tira a tarefa de A_FAZER;
  - responder e pôr em AGUARDANDO_CLIENTE são duas ações;
  - cancelar a solicitação deixa as tarefas abertas;
  - concluir o projeto não olha as tarefas, e concluir a última tarefa do projeto não sugere concluí-lo.

## 3. Cenários de aceite

As letras entre colchetes ligam o cenário à decisão da §5 de que ele depende. Os cenários seguem a recomendação.

### Conversa

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F11-01 | solicitação aberta e quem tem `solicitacao.comentar` | abre a caixa de conversa | "Resposta ao cliente" vem marcada, e o botão diz "Enviar ao cliente" [D1] | `conversa.test › abre em "Resposta ao cliente"` |
| F11-02 | caixa em "Nota interna" | a pessoa escreve | o fundo e o botão mudam ("Salvar nota interna"), para não haver dúvida sobre quem vai ler [D1] | `conversa.test › registra nota interna com menção` (botão "Salvar nota interna"; fundo âmbar do `destaque`) |
| F11-03 | a pessoa trocou para "Nota interna" e enviou | a caixa volta vazia | volta para "Resposta ao cliente": o padrão não muda conforme o último uso [D1] | `conversa.test › registra nota interna com menção` (volta a "Resposta ao cliente") |
| F11-04 | solicitação ABERTA, EM_ANDAMENTO ou AGUARDANDO_TERCEIRO | a pessoa escolhe "Enviar e aguardar o cliente" | a resposta é registrada e a solicitação vai para AGUARDANDO_CLIENTE numa só ação, com o SLA pausado como no RF-SOL-05 [D7-c] | `SolicitacoesTests › Interacoes_EnviarEAguardar_*` (3); `AvisosAoContatoTests › EnviarEAguardar_UmSoEmailQuePedeORetorno`; `conversa.test › "Enviar e aguardar o cliente"` |
| F11-05 | solicitação ABERTA | a equipe envia a primeira resposta ao cliente | a primeira resposta é registrada (RF-SOL-07) e a solicitação passa a EM_ANDAMENTO [D7-a] | `SolicitacoesTests › Interacoes_PrimeiraRespostaEmAberta_PassaParaEmAndamento` |

### Solicitação encerrada

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F11-06 | solicitação FECHADA ou CANCELADA | a equipe tenta enviar resposta ao cliente ou anexo pela API | 409 `solicitacao-encerrada`, e nada é gravado (nem o arquivo) [D4] | `AnexosTests › Comentario_SolicitacaoEncerrada_SoNotaInternaSemAnexo`; `SolicitacaoTests › GarantirConversaAberta_Encerrada_SoNotaInternaSemAnexo` |
| F11-07 | solicitação FECHADA ou CANCELADA | a equipe registra nota interna sem anexo | entra (registro posterior, como "o cliente ligou agradecendo") e não muda o status [D4] | `AnexosTests › Comentario_SolicitacaoEncerrada_SoNotaInternaSemAnexo` |
| F11-08 | solicitação FECHADA ou CANCELADA | o contato responde pelo portal (conta ou link) | 409 `solicitacao-encerrada`; o portal esconde a caixa e mostra "Esta solicitação foi encerrada. Para continuar, abra uma nova." com o botão de nova solicitação [D4] | `PortalLinkTests › Responder_SolicitacaoEncerrada_409`; `PortalTests › Responder_ComContaEAnexo_SolicitacaoEncerrada_409SemGuardarArquivo`; `detalhe.test › encerrada não tem caixa de mensagem`, `no link, encerrada orienta a falar com a equipe` |
| F11-09 | solicitação FECHADA ou CANCELADA no detalhe da equipe | a tela abre | a caixa mostra só "Nota interna", sem anexo, com a frase "Solicitação encerrada: a conversa com o cliente acabou." [D4] | `conversa.test › encerrada: só nota interna, sem anexo, com o aviso` |
| F11-10 | solicitação RESOLVIDA (ainda dentro do prazo de reabertura) | equipe ou cliente escrevem | continua permitido, como hoje: o cliente pode reabrir pelo RF-SOL-10 [D4] | `SolicitacoesTests › Interacoes_Resolvida_ContinuaAbertaAConversa`; `SolicitacaoTests › GarantirConversaAberta_Resolvida_Permite`; `detalhe.test › resolvida, para quem não aprova, ainda tem a caixa` |

### Solicitação e tarefas

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F11-11 | solicitação com tarefas A_FAZER ou FAZENDO | a pessoa abre "Resolver" | a janela lista as tarefas abertas e oferece "Concluir as tarefas abertas junto" (marcado) e "Deixar abertas" [D2] | `detalhe.test › lista as abertas e conclui junto, marcado por padrão` |
| F11-12 | F11-11 com "Concluir junto" | confirma | numa só transação, as tarefas abertas vão para CONCLUIDA (as A_FAZER passam por FAZENDO, como no quadro) e a solicitação para RESOLVIDA; se uma falhar, nada muda [D2] | `SolicitacaoETarefasTests › Resolver_ConcluirTarefasJunto_TudoMuda`; `Cancelar_SolicitacaoRecusaDepoisDasTarefas_NadaMuda` (atomicidade) |
| F11-13 | F11-11 com "Deixar abertas" | confirma | a solicitação resolve e as tarefas ficam como estão, como hoje [D2] | `SolicitacaoETarefasTests › Resolver_ManterTarefas_ComoHoje`; `detalhe.test › desmarcado, mantém as tarefas` |
| F11-14 | quem resolve não tem `tarefa.editar` | abre "Resolver" | vê a lista como aviso, sem a opção de concluir junto, e pode resolver [D2] | `SolicitacaoETarefasTests › Resolver_ConcluirSemTarefaEditar_403ENadaMuda`; `detalhe.test › sem tarefa.editar, só avisa` |
| F11-15 | solicitação ABERTA, EM_ANDAMENTO ou AGUARDANDO_* com mais de uma tarefa, só uma aberta | a pessoa conclui essa última tarefa (painel ou quadro) | aparece "Era a última tarefa da solicitação #N. Resolver a solicitação agora?" com o botão "Resolver", que abre a janela de resolução já com a solução para preencher [D3] | `painel.test › sugere resolver, e o botão leva à janela de resolução`; `painel.test › com outra tarefa ainda aberta…`; `tarefas.test › concluir no quadro a última tarefa…`; `detalhe.test › com ?resolver=true…` |
| F11-16 | F11-15 | a pessoa ignora o aviso | nada muda na solicitação [D3] | `painel.test › sugere resolver…` (nenhum POST /resolucao; o aviso só navega se clicado) |
| F11-17 | F11-15 e quem conclui não tem `solicitacao.editar` | conclui | o aviso não aparece [D3] | `painel.test › sem editar a solicitação (F11-17), não sugere`; `… com ela já resolvida, não sugere` |
| F11-18 | solicitação ABERTA | uma tarefa é criada nela | a solicitação passa a EM_ANDAMENTO [D7-a] | `SolicitacaoETarefasTests › CriarTarefa_SolicitacaoAberta_PassaAEmAndamento` |
| F11-19 | tarefa A_FAZER | o cronômetro é iniciado nela | a tarefa passa a FAZENDO e, se for de solicitação ABERTA, a solicitação passa a EM_ANDAMENTO [D7-b] | `SolicitacaoETarefasTests › IniciarCronometro_TarefaAFazer_PassaAFazendo`; `AndamentoPelaTarefaTests › TarefaAFazendoNaAberta_PassaAEmAndamento`; `cronometro.test › iniciar numa tarefa a fazer recarrega a tarefa…` |
| F11-20 | solicitação com tarefas abertas | a pessoa cancela a solicitação | a janela lista as tarefas abertas e oferece "Cancelar as tarefas junto" (marcado), na mesma transação; sem `tarefa.editar`, só o aviso [D7-d] | `SolicitacaoETarefasTests › Cancelar_CancelarTarefasJunto`; `detalhe.test › cancelar oferece cancelar as tarefas junto` |

### Projeto

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F11-21 | solicitação não encerrada e quem tem `projeto.gerenciar` | escolhe "Criar projeto" no detalhe | abre a janela de novo projeto com cliente e contrato da solicitação e o título dela como nome [D5] | — |
| F11-22 | F11-21 e a solicitação tem tarefas abertas sem projeto | a janela abre | lista essas tarefas com "Levar para o projeto" (marcadas); ao criar, elas passam a ser do projeto e continuam ligadas à solicitação [D5] | — |
| F11-23 | tarefa não cancelada, sem projeto ou em outro projeto, e quem tem `tarefa.editar` | escolhe "Mover para projeto" no painel da tarefa | pode escolher um projeto EM_ANDAMENTO do mesmo cliente, ou "Sem projeto" [D6] | `MoverTarefaProjetoTests › Mover_DeSolicitacaoParaProjetoTrocarETirar`; `TarefaTests › MoverParaProjeto`; `painel.test › mover para projeto…` |
| F11-24 | F11-23 com um projeto de outro cliente (pela API) | envia | 422 `projeto-de-outro-cliente`; a tarefa não muda [D6] | `MoverTarefaProjetoTests › Mover_ProjetoDeOutroCliente_422ENadaMuda` |
| F11-25 | F11-23 com projeto CONCLUIDO ou CANCELADO | envia | 422 `projeto-encerrado` [D6] | `MoverTarefaProjetoTests › Mover_ProjetoEncerrado_422` |
| F11-26 | tarefa com apontamentos que muda de projeto | a mudança é salva | os apontamentos já feitos mantêm o contrato que consumiram; o progresso dos dois projetos (RF-PRJ-02) passa a contar as horas pela tarefa; a mudança vai para a atividade da tarefa [D6] | `MoverTarefaProjetoTests › Mover_ApontamentosMantemOContrato_ProgressoSegueATarefa_EVaiParaAAtividade` |
| F11-27 | projeto com tarefas abertas | a pessoa conclui o projeto | a janela lista as tarefas abertas e oferece concluir ou cancelar junto, como no F11-11 [D7-e] | — |
| F11-28 | projeto EM_ANDAMENTO | conclui a última tarefa aberta dele | aparece "Era a última tarefa do projeto. Concluir o projeto agora?", só para quem tem `projeto.gerenciar` [D7-e] | — |

### Isolamento e permissão

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F11-29 | tarefa ou projeto de outro tenant | qualquer ação deste marco cita o Id | solicitação ou projeto de outro tenant: 404, como em todo o resto (ADR 0002); tarefa de outro tenant na lista da ação conjunta: a lista não confere e dá 409 (F11-31), sem revelar nada | `SolicitacaoETarefasTests › Resolver_TarefaDeOutroTenantNaLista_409SemRevelar`; `MoverTarefaProjetoTests › Mover_ProjetoDeOutroTenant_404` |
| F11-30 | contato do portal | tenta qualquer rota de equipe deste marco | 403/404 como hoje; no portal, só o F11-08 se aplica | `AutenticacaoTests › Cliente_403NaRotaInterna_200NoPortal_ETrocaDeTenant` (sem mudança neste marco) |
| F11-31 | ação conjunta (F11-12, F11-20, F11-27) com uma tarefa alterada por outra pessoa no meio | confirma | `409 conflito-versao`, nada muda, e a janela recarrega a lista | `SolicitacaoETarefasTests › Resolver_ListaDeTarefasMudou_409ENadaMuda`; `detalhe.test › lista mudou no meio…` |

## 4. Fora de escopo

- Trocar a **solicitação** de uma tarefa depois de criada: mexe em portal, SLA e extrato do cliente. Fica para depois, se fizer falta.
- Reabrir as tarefas ao reabrir a solicitação: a equipe cria ou reabre a tarefa que precisar.
- Resolver a solicitação automaticamente, sem perguntar: a solução é obrigatória e avisa o cliente (D3).
- Ligar a solicitação ao projeto por um campo próprio: a ligação vem pelas tarefas (D5).
- Automação configurável pelo tenant (regras do tipo "se… então…").

## 5. Decisões que dependem do Daniel

1. **D1 — Padrão da caixa de conversa.**
   - (a) **Recomendado:** "Resposta ao cliente" como padrão, com botão e fundo diferentes para a nota interna (F11-01 a F11-03). A maior parte do que se escreve numa solicitação é para o cliente. O risco é mandar uma nota interna ao cliente sem querer, e ele cai porque o botão diz para quem vai. Muda o RF-SOL-06.
   - (b) Lembrar a última escolha de cada pessoa. É menos previsível: quem acabou de escrever uma nota interna manda a próxima também como interna.
   - (c) Manter "Nota interna" como padrão (hoje).

2. **D2 — Resolver com tarefas abertas.**
   - (a) **Recomendado:** avisar e oferecer concluir junto, sem bloquear (F11-11 a F11-14). Isso resolve o "mexer em um lugar e depois no outro", e quem tem um motivo para deixar a tarefa aberta (um acompanhamento, por exemplo) ainda consegue.
   - (b) Bloquear até as tarefas fecharem. É rígido, porque obriga a cancelar tarefa que só ficou de lembrete.
   - (c) Permitir sem aviso (hoje).

3. **D3 — Concluir a última tarefa da solicitação.**
   - (a) **Recomendado:** oferecer "Resolver agora" logo depois de concluir, abrindo a janela de resolução (F11-15 a F11-17). A resolução precisa de solução escrita e avisa o cliente, por isso pede uma confirmação.
   - (b) Resolver automaticamente. Exigiria uma solução padrão ("Tarefas concluídas") que o cliente receberia por e-mail. É arriscado.
   - (c) Nada (hoje).

4. **D4 — Conversa em solicitação encerrada.**
   - (a) **Recomendado:** em FECHADA ou CANCELADA, bloquear a resposta ao cliente e o anexo para a equipe e para o portal, mantendo a nota interna sem anexo. RESOLVIDA segue aberta, porque ainda pode ser reaberta (F11-06 a F11-10). O cliente que quer continuar é levado a abrir uma nova solicitação, que tem SLA próprio.
   - (b) Bloquear tudo, inclusive a nota interna. Perde-se o registro posterior.
   - (c) Bloquear também em RESOLVIDA. Contradiz o RF-SOL-10, porque o cliente precisa falar para reabrir.

5. **D5 — Projeto a partir da solicitação.**
   - (a) **Recomendado:** "Criar projeto" pré-preenchido, que leva as tarefas abertas da solicitação para o projeto (F11-21, F11-22). A ligação fica pelas tarefas, que já podem ter solicitação e projeto ao mesmo tempo (RF-TAR-01), e não precisa de migration.
   - (b) Campo `ProjetoId` na solicitação, com o projeto mostrado no detalhe e no portal. Muda o modelo e o contrato, e ganha pouco além do (a).

6. **D6 — Mover tarefa para projeto depois de criada.**
   - (a) **Recomendado:** permitir vincular, trocar ou tirar o projeto, desde que seja do mesmo cliente e o projeto esteja EM_ANDAMENTO (F11-23 a F11-26). As horas já apontadas mantêm o contrato que consumiram, e o progresso dos projetos acompanha a tarefa.
   - (b) Só vincular a quem não tem projeto, sem trocar nem tirar. É mais simples, mas o engano de projeto não tem conserto.

7. **D7 — Outras automações do mesmo tipo** (escolha quais entram; todas são recomendadas):
   - (a) primeira resposta ao cliente ou tarefa criada tiram a solicitação de ABERTA para EM_ANDAMENTO (F11-05, F11-18);
   - (b) cronômetro iniciado tira a tarefa de A_FAZER (e a solicitação de ABERTA) (F11-19);
   - (c) "Enviar e aguardar o cliente" numa ação (F11-04);
   - (d) cancelar a solicitação oferece cancelar as tarefas abertas (F11-20);
   - (e) projeto e suas tarefas, com as mesmas regras do D2 e do D3 (F11-27, F11-28).

## 6. Impacto nos documentos

- **ERS:**
  - RF-SOL-06 passa a "padrão: EXTERNA" (D1).
  - RF-SOL-03 ganha as transições automáticas para EM_ANDAMENTO (D7-a e D7-b).
  - Requisito novo proposto, RF-SOL-13: "Solicitação FECHADA ou CANCELADA não recebe resposta ao cliente nem anexo; nota interna sim" (D4).
  - RF-TAR-01 ganha "mover entre projetos do mesmo cliente" (D6).
  - RF-PRJ-01 ganha "criar a partir de uma solicitação" (D5).
- **Contrato da API:**
  - erro `solicitacao-encerrada` (409);
  - resolução e cancelamento da solicitação e conclusão do projeto aceitam `tarefas: "CONCLUIR" | "CANCELAR" | "MANTER"`;
  - interação aceita `aguardarCliente: true`;
  - `PUT /tarefas/{id}/projeto` (ou o campo no PATCH da tarefa), com os erros `projeto-de-outro-cliente` e `projeto-encerrado`;
  - criar projeto aceita `tarefaIds`.
  
  Tudo com OpenAPI regenerado.
- **Modelo de domínio:**
  - `Tarefa.MoverParaProjeto`;
  - `Solicitacao.GarantirConversaAberta`;
  - as transições automáticas (§3.1);
  - as ações conjuntas como casos de uso de Application, sem que uma entidade mexa na outra.
- **Read side:** o detalhe da solicitação e o do projeto passam a trazer, nas tarefas, o que a janela de confirmação precisa (status e versão).
- **Segurança:** nenhuma mudança de papel; as ações conjuntas exigem as permissões das duas pontas.
- **Convenções:** registrar o padrão "ação conjunta com confirmação que lista o que muda, numa transação" (§4).
- **Ajuda e Novidades:** telas Solicitações, Tarefas e Projetos.

## 7. Divisão em PRs

| PR | Entrega | Cenários |
|---|---|---|
| F11a | Conversa: padrão "Resposta ao cliente", visual da nota interna, "Enviar e aguardar" | F11-01 a F11-05 |
| F11b | Solicitação encerrada: bloqueio na API (equipe e portal) e na tela | F11-06 a F11-10, F11-30 |
| F11c | Resolver e cancelar com as tarefas abertas; aviso da última tarefa; transições automáticas | F11-11 a F11-20, F11-29, F11-31 |
| F11d | Mover tarefa entre projetos | F11-23 a F11-26 |
| F11e | Criar projeto a partir da solicitação; projeto e suas tarefas | F11-21, F11-22, F11-27, F11-28 |

O F11a e o F11b são independentes e podem sair primeiro. O F11e depende do F11d (mesma regra de mover).

## 8. Registro

- 06/11 — rascunho a partir do pedido do Daniel (quatro pontos e "verifique outras situações").
- 06/11 — o Daniel aprovou todas as recomendações (D1 a D7) e pediu o PR da spec.
- 06/11 — F11a: na API, sem `visibilidade`, continua INTERNA (compatível); o padrão novo é da tela. "Enviar e aguardar" manda um só e-mail ao contato (o da mensagem, com assunto de espera), decidido pelo pedido da própria ação (`InteracaoRegistrada.PedeRetorno`), não pelo status lido depois. Encerrada (FECHADA/CANCELADA) já abre em nota interna; o bloqueio completo vem no F11b. A transição automática gera, como qualquer mudança de status, o aviso aos seguidores e a linha no histórico.
- 09/11 — F11b: no portal, RESOLVIDA aparece como "Concluída" para quem não aprova, e o status externo não a separa da FECHADA; o detalhe do portal ganhou `encerrada`, e a caixa de mensagem fica aberta na RESOLVIDA (antes sumia), como pede o F11-10. "Nova solicitação" aparece para quem tem `portal.solicitacao.criar` (a sessão de link não tem); no link, a orientação é falar com a equipe. Limitação conhecida: a checagem de encerrada e a gravação da interação não travam a versão da solicitação, então um cancelamento no mesmo instante pode deixar passar uma resposta (risco baixo, sem tratamento).
- 09/11 — F11c: a lista desatualizada (F11-31) responde `409 conflito-versao`, como a versão divergente do resto da API, e não 412 como estava escrito. No F11-29, um id de outro tenant na lista não aparece como 404, e sim como lista diferente (409), sem revelar nada; a solicitação de outro tenant continua 404. A sugestão do F11-15 vale também com uma tarefa só (é a última aberta). Os eventos `TarefaCriada` e `TarefaStatusAlterada` passaram a levar o `SolicitacaoId` (opcional, compatível com a outbox). Efeitos automáticos não pedem a permissão do alvo: `tarefa.criar` ou `apontamento.criar` bastam para tirar a solicitação de ABERTA, e `apontamento.criar` põe a tarefa em FAZENDO (segurança §3.3). Concluir junto uma tarefa A_FAZER gera duas mudanças de status (FAZENDO e CONCLUIDA), com dois avisos e duas linhas no histórico, como no quadro. Limitações conhecidas: criar tarefa e iniciar o cronômetro agora gravam também a solicitação, então duas ações ao mesmo tempo na mesma solicitação ABERTA podem dar 409 (risco baixo); numa solicitação já resolvida com lista velha, a resposta é 409 e não 422 (nada é gravado).
- 09/11 — F11d: mover é `PUT /tarefas/{id}/projeto` (fora do `PATCH`, como o contrato da solicitação). Tarefa sem cliente não entra em projeto (`projeto-de-outro-cliente`): ganhar cliente mexeria no portal e no extrato. Sair de um projeto já encerrado pode; tarefa CANCELADA não muda. A linha `TAREFA_PROJETO` do feed guarda os ids dos projetos nas colunas de/para já existentes, sem migration, e a leitura mostra os nomes atuais. No painel, o campo Projeto aparece para tarefa com cliente, quando a pessoa tem `tarefa.editar` e `projeto.ver`, e salva na hora com desfazer.
