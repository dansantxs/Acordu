# Notas do desenvolvedor (F14d, etapa 4)

Premissas técnicas tomadas no F14d. Nenhuma muda cenário nem decisão do design.

- **Jobs por tenant:** o filtro do `ExecutorPorTenant` (só as graváveis) entrou no F14b. Ele cobre, no F14-22, recorrências, alertas de SLA e cronômetros esquecidos (tick) e o fechamento automático (diário). Aqui ficaram os testes do cenário inteiro, inclusive a outra empresa seguindo normalmente e `falhas` em 0.
- **O que passa pela outbox:** só eventos de domínio do tenant, gravados pela `UnitOfWork` na transação do caso de uso (avisos ao contato e à equipe, convites, feed, sino). Não passam por ela:
  - os e-mails da conta (confirmação e redefinição de senha), que o `ServicoAutenticacao` envia logo após o commit;
  - o link por solicitação, que sai na própria requisição e que a guarda já barra no só leitura (F14-17);
  - os avisos de situação aos Donos (F14j), que pelo design D7 saem do passo do diário logo após o commit.

  Por isso o descarte por situação no `ProcessadorOutbox` não pega nenhum deles. Teste: `SomenteLeituraJobsTests › EmailsDaConta_EmpresaSoLeitura_SaemMesmoComAFilaDescartada` (empresa CANCELADA com fila pendente: a fila é descartada e a confirmação e a redefinição saem).
- **Descarte:** uma consulta por lote lê quais tenants das mensagens estão só leitura (`SituacaoEmpresaExtensions.SituacoesSomenteLeitura`, com `IgnoreQueryFilters` só do filtro de tenant, porque a fila não tem contexto). A mensagem desses tenants recebe `MensagemOutbox.Descartar`: `processada_em` = agora e `erro` = "descartada: empresa só leitura" (`ProcessadorOutbox.MotivoSomenteLeitura`). Não é falha (não conta tentativa nem `falhou_em`) e não volta à fila: não há envio retroativo (ADR 0015, item 5). A quantidade vai ao log (`Information`, EventId 300). Ela entra em `outboxProcessadas` do tick, que conta as mensagens tratadas.
- **`PodeAvisarAsync`:** devolve falso, e registra a categoria bloqueada, quando a empresa está só leitura no momento da checagem, mesmo para o criador. É uma segunda barreira: com o executor e o descarte, nenhum caminho de hoje chega até ele numa empresa só leitura.
- **`Tenant.GravavelDesde`:** coluna `gravavel_desde` (`timestamptz`, anulável), migration `GravavelDesdeDaEmpresa` gerada com `dotnet ef`, sem preenchimento: nula para todas, porque nenhuma empresa voltou do só leitura até aqui. Não há método de domínio que a grave: as transições `Ativar`, `Prorrogar` e `Reativar` do F14l a gravam com o instante da volta. Nos testes, o estado é gravado direto no banco.
- **Recorrências sem retroativo (F14-24):** a `GeracaoRecorrencias` lê o `Tenant` (`ITenantAtual`) uma vez por execução. Com `GravavelDesde`, as datas devidas anteriores ao dia dele no fuso do tenant passam sem gerar, e a recorrência avança, como já acontece com cliente inativo. O dia da volta em diante é gerado normalmente, com a antecedência da regra.
- **SLA na volta (F14-111):** nada mudou. Os alertas avaliam a situação do SLA no instante do tick, e o prazo é calculado sem pausa (decisão 19 do G2, ADR 0003 intacto). Na volta sai o alerta de violado; o de risco, que caiu no só leitura, não sai depois, porque o alerta usa a situação do momento.
- **Testes com o relógio adiantado:** o access token do registro vence em 60 min do relógio falso. Para ler ou gravar no instante da volta, o teste entra de novo (`/auth/login`).
- **Rodada do revisor (documentos):**
  - modelo de domínio, § da outbox: o descarte tira o evento inteiro da fila (inclusive o feed e o sino ainda não gerados). A perda fica na janela entre o commit e o vencimento, e o descartado não volta se a empresa voltar a gravar;
  - deploy-ci §5: em `outbox_mensagens`, falha é `falhou_em` (com `tentativas`) e descarte é `processada_em` com `erro` começando por `descartada:`. Consulta que conte falhas usa `falhou_em`, nunca `erro IS NOT NULL`.
