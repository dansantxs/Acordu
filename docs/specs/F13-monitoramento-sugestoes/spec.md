# Spec — F13: Monitoramento pelo Discord e "Sugerir melhoria"

| | |
|---|---|
| **Status** | Em implementação |
| **Data** | 09/11/2026 |
| **Requisitos** | RNF-OPS-01, RNF-OPS-02, RNF-OPS-03, RF-SUG-01 (ERS). Tocados: RNF-SEG-08, RNF-POR-01, RNF-PRIV-01 |
| **ADRs** | 0010 (hospedagem; o monitoramento muda), 0012 (novo: avisos de operação) |

## 1. Objetivo

Antes do primeiro deploy, a Nebrys fica sabendo pelo Discord, sem abrir painel nenhum, quando:

- aparece um erro novo, na API ou no app;
- um job falha ou a API cai;
- um deploy ou um backup termina;
- alguém da equipe de um prestador sugere uma melhoria.

Os erros também ficam no Error Reporting do Google para investigar. A pessoa da equipe sugere uma melhoria pelo menu da conta, e a sugestão fica guardada fora dos dados da empresa dela.

## 2. Cenários de aceite

### Logs estruturados (F13a)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F13-01 | API com `Logs__Formato=json` | qualquer log | sai uma linha JSON por entrada, com `severity`, `message`, `time`, `categoria`, `eventId` e `traceId` | `FormatadorLogJsonTests › Write_Informacao_UmaLinhaComOsCamposDoCloudLogging`; `… Write_DentroDeUmaAtividade_TraceIdDela`; `… Write_CadaNivel_SeveridadeDoCloudLogging`; `ComposicaoLogsTests › AddLogsAcordu_ComJson_ConsoleUsaOFormatadorComAVersao` |
| F13-02 | idem | requisição termina em 500 | a entrada tem `severity` `ERROR`, a pilha (`stack_trace`) e `serviceContext` (`acordu-api` e a versão de `App__Versao`), formato que o Error Reporting agrupa sem SDK [D1] | `FormatadorLogJsonTests › Write_Erro_PilhaEContextoDoServico`; `LogDoErroTests › Erro500_ComDadosNaRequisicao…`; o agrupamento é conferido no primeiro deploy |
| F13-03 | API sem `Logs__Formato` (desenvolvimento, testes) | qualquer log | sai o console de texto de hoje | `ComposicaoLogsTests › AddLogsAcordu_SemFormato_ConsoleDeTexto` |
| F13-04 | erro 500 numa rota com dados na query e no corpo | o erro vai para o log | a entrada tem a rota como modelo (`/api/v1/tarefas/{id}`), o método, `tenantId` e `usuarioId`; não tem corpo, query string, cabeçalhos, e-mail nem nome (segurança §6) | `LogDoErroTests › Erro500_ComDadosNaRequisicao_UmaLinhaComRotaModeloEIdentificadoresSemOsDados`; `FormatadorLogJsonTests › Write_DosEscopos_SoOsCamposDoContexto` |

### Avisos de erro no Discord (F13b)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F13-05 | webhook de #alertas configurado | acontece um 500 com uma impressão digital nunca vista (tipo da exceção, primeiro quadro do código do Acordu e rota modelo) | sai uma mensagem "Erro novo" em #alertas com o tipo, o quadro, a rota, a versão e o link para o Error Reporting; sem a mensagem da exceção [D1, D2] | `AvisosDeErroTests › Erro500_Novo_AvisaComAImpressaoSemAMensagemEComMencoesDesligadas`; `QuadroDoAcorduTests` |
| F13-06 | erro já avisado há menos de 24 h | ele se repete | nada sai; a contagem sobe | `AvisosDeErroTests › Erro500_RepetidoEm24h_NaoAvisaEConta` |
| F13-07 | erro avisado há 24 h ou mais | ele se repete | sai "Erro de novo: N vezes desde o último aviso" [D2] | `AvisosDeErroTests › Erro500_RepetidoDepoisDe24h_AvisaDeNovoComAContagem` |
| F13-08 | 20 avisos já enviados na última hora | chega um erro novo | não sai na hora; o próximo aviso depois da hora diz "N avisos segurados" [D2] | `AvisosDeErroTests › Erro500_TetoDaHora_SeguraEResumeNoProximoAviso` |
| F13-09 | Discord fora do ar ou lento | a API tenta avisar | a resposta ao usuário não muda nem espera mais que 3 s; a falha vai para o log como aviso, sem a URL do webhook | `AvisosDeErroTests › Erro500_DiscordLento_RespostaNaoEsperaMaisQueOPrazo`; `… Erro500_DiscordFora_RespostaIgualEFalhaNoLogSemAUrl`; `AvisosDeErroSemBancoTests` |
| F13-10 | sem webhook configurado (desenvolvimento, testes) | qualquer erro | nada sai e nada falha | `AvisosDeErroTests › Erro500_SemWebhook_NadaSaiENadaGrava` |
| F13-11 | qualquer mensagem da API ao Discord | é enviada | vai com menções desligadas (`allowed_mentions` vazio): texto nenhum aciona `@everyone` ou `@here` | `AvisosDeErroTests › Erro500_Novo_AvisaComAImpressao…` (as mensagens das sugestões e dos jobs entram nos PRs delas) |
| F13-12 | ocorrências de erro gravadas | qualquer pessoa, de qualquer empresa, usa a API | nenhuma rota devolve as ocorrências: elas ficam fora dos tenants e só a operação as vê no banco e no Discord | `OperacaoTests › OcorrenciaErro_SoAOperacaoDaInfrastructureUsa` (arquitetura) |

### Jobs (F13c)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F13-13 | tick ou diário com `falhas > 0` | o job termina | sai em #alertas "tick: N falhas em <passo>", uma vez por passo e por dia | — |
| F13-14 | diário terminou sem falhas | o job termina | sai em #deploy o resumo do dia (fechamentos, expurgos), uma vez por dia: a falta dele avisa que o agendamento parou | — |

### Erros do app no navegador (F13d)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F13-15 | app em produção (`VITE_VERSAO` definido) | uma tela cai na tela de erro | o app manda `POST /api/v1/erros-web` com mensagem, pilha, rota modelo (ids viram `:id`, sem query), versão e navegador; a API responde 204 e registra como `ERROR` do serviço `acordu-web` | — |
| F13-16 | idem | erro não tratado (`error` ou `unhandledrejection` da janela) | idem, uma vez por erro e por carga de página | — |
| F13-17 | idem | o erro é resposta da API (`ErroApi`) ou falha de rede | nada é enviado: a API já registra os dela, e sem rede não é bug | — |
| F13-18 | erro do app já avisado | o mesmo erro chega de novo | segue as regras de F13-05 a F13-08 (impressão digital: mensagem sem números, primeiro quadro sem o hash do arquivo e rota) | — |
| F13-19 | rota pública [D6] | mais de 10 envios por minuto do mesmo IP | `429`; corpo acima de 16 KB → `413`; campos longos são cortados | — |
| F13-20 | portal do cliente | erro numa tela do portal | envia igual, sem id de contato, cliente ou link | — |
| F13-21 | desenvolvimento (`pnpm dev`) ou testes | qualquer erro | nada é enviado | — |

### Sugerir melhoria (F13e e F13f)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F13-22 | pessoa da equipe, qualquer papel [D5] | abre o menu da conta > "Sugerir melhoria" | abre a janela com o texto (10 a 2.000 caracteres) e o aviso "Não escreva dados dos seus clientes: a sugestão vai para a equipe do Acordu" | — |
| F13-23 | idem | envia | `POST /api/v1/sugestoes` grava a sugestão (texto, tela de onde veio, empresa e pessoa como identificadores) e responde 201; o app mostra "Obrigado! A sugestão foi para a equipe do Acordu." | — |
| F13-24 | texto com menos de 10 ou mais de 2.000 caracteres | envia | `400` com `errors.texto` | — |
| F13-25 | sessão do portal (contato do cliente) | chama `POST /api/v1/sugestoes` | `403`; o portal não mostra a opção | — |
| F13-26 | sem login | chama `POST /api/v1/sugestoes` | `401` | — |
| F13-27 | sugestões gravadas | qualquer pessoa, de qualquer empresa, usa a API | nenhuma rota lista ou devolve sugestões: ficam fora dos tenants (sem `TenantId` de filtro, só o id como referência) | — (teste de arquitetura, como F13-12) |
| F13-28 | webhook de #sugestoes configurado | a sugestão é gravada | sai em #sugestoes com o número, o texto, a tela e o nome da empresa; sem nome nem e-mail da pessoa; menções desligadas (F13-11) [D4] | — |
| F13-29 | Discord fora do ar | a sugestão é gravada | a pessoa vê o sucesso igual; a sugestão fica pendente e o próximo tick reenvia | — |
| F13-30 | 5 sugestões da mesma pessoa na última hora | envia mais uma | `429` com "Muitas sugestões em pouco tempo. Tente de novo mais tarde." | — |

### Deploy e backup (F13g)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F13-31 | deploy terminou | com sucesso | sai em #deploy "Deploy ok" com o commit; com falha, sai em #alertas com o link da execução [D3] | conferência manual no primeiro deploy |
| F13-32 | backup diário terminou | com sucesso | sai em #deploy o heartbeat; com falha, em #alertas | conferência manual |
| F13-33 | secret do canal ausente | o workflow termina | o aviso daquele canal é pulado, sem falhar o workflow | — (`deploy.yml`, `backup.yml`) |

### API fora do ar (F13h)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F13-34 | vigia ligado [D3] | `GET /api/health` pela borda falha (ou passa de 15 s) em duas checagens seguidas, de 5 em 5 min | sai em #alertas "API fora do ar desde HH:MM", uma vez por queda | — |
| F13-35 | queda avisada | a checagem volta a passar | sai "API voltou, fora por N min" | — |
| F13-36 | uma checagem falha e a seguinte passa (partida a frio) | — | nada sai | — |

## 3. Fora de escopo

- Sentry, OpenTelemetry, traces e métricas de desempenho.
- Tela de administração da Nebrys para ler sugestões ou erros (lê no Discord e no banco).
- Responder a sugestão para a pessoa, votar ou acompanhar o andamento dela.
- Sugestão pelo portal do cliente.
- Alerta de orçamento no Discord: continua por e-mail (A1 do setup, já ativo). O Google não manda alerta de orçamento para webhook sem Pub/Sub e código.
- Página de status pública.
- Avisos de SLA ou de clientes no Discord: são do prestador e seguem no sino e no e-mail.

## 4. Decisões que dependem do Daniel

1. **Como o aviso de erro chega ao Discord.**
   - (a) **Recomendado:** a própria API avisa por uma porta de "avisos de operação". A Infrastructure posta no webhook por HTTP, sem SDK. Os logs JSON vão para o Error Reporting, para investigar.
     - Vale em qualquer hospedagem (RNF-POR-01), roda nos testes e não depende de configurar alerta no Google.
     - Custo: o que derruba a API inteira (não sobe, falta de memória) não vira aviso de erro. Isso fica com a vigia (F13-34) e com a conferência de saúde do deploy, que já existe.
   - (b) Error Reporting → canal de notificação do Cloud Monitoring → webhook. Exige um repasse que converta para o formato do Discord, na API ou num Worker.
     - Pega também a queda na partida.
     - Mas é configuração manual no Google, que não se testa na CI, e prende o alerta ao provedor.
   - (c) Sentry no plano gratuito. É SDK de terceiro, contra o "sem SDK" do ADR 0010.
2. **Deduplicação e teto.**
   - (a) **Recomendado:** tabela global de ocorrências (fora dos tenants, migration aditiva):
     - avisa na primeira vez, cala por 24 h e depois avisa "de novo" com a contagem;
     - teto de 20 avisos por hora;
     - o diário apaga as ocorrências sem repetição há 90 dias.
     - Sobrevive ao Cloud Run desligar a instância, que no nível 0 acontece várias vezes por dia.
   - (b) Só em memória: cada partida a frio repetiria o aviso de todos os erros.
   - (c) Sem deduplicação: um erro num laço inunda o canal.
3. **Canais e segredos.**
   - (a) **Recomendado:** três canais, cada um com webhook próprio:
     - **#alertas:** erros, jobs, queda, deploy e backup que falharam;
     - **#deploy:** deploy ok, heartbeat do backup e resumo do diário;
     - **#sugestoes.**
     - Os segredos ficam só em variáveis de ambiente: `Discord__Alertas`, `Discord__Deploy` e `Discord__Sugestoes` na API; `DISCORD_ALERTAS` e `DISCORD_DEPLOY` no GitHub e no Worker. Dá para silenciar a rotina e manter os alertas com notificação no celular.
   - (b) Um canal só, como o `DISCORD_WEBHOOK` de hoje: simples, mas o heartbeat diário afoga o alerta.
   - **Queda:**
     - (a) **Recomendado:** um Worker "vigia" com Cron Trigger de 5 min (gratuito), guardando o estado no KV (só escreve quando o estado muda). Fica sob o nosso código, com testes, e substitui o UptimeRobot.
     - (b) UptimeRobot com a integração do Discord. As fontes divergem: uma diz que o plano gratuito tem Discord e outra que só tem e-mail. Também não está claro se o plano gratuito permite uso comercial (restrição citada desde dez/2024). Só dá para conferir no cadastro.
     - (c) UptimeRobot só por e-mail, fora da "central" no Discord.
4. **O que da sugestão vai ao Discord.** A segurança §6 diz "nada de dado de cliente em log e no Discord".
   - (a) **Recomendado:** texto, tela e nome da empresa, sem o nome nem o e-mail da pessoa, com o aviso na janela (F13-22). A segurança §6 ganha a exceção: o texto é escrito pela pessoa para a Nebrys, e a empresa é cliente da Nebrys, não dado do prestador.
   - (b) Só "Sugestão nova #N". Você leria o texto no banco, porque não há tela de administração.
   - (c) Também o nome da pessoa: ajuda a responder, mas expõe mais num serviço de terceiro.
5. **Quem pode sugerir.**
   - (a) **Recomendado:** qualquer pessoa da equipe, sem permissão nova: é para ouvir todo mundo, e o limite de 5 por hora segura abuso.
   - (b) Permissão nova `sugestao.enviar`, que o Dono distribui.
6. **Rota dos erros do app.**
   - (a) **Recomendado:** pública, com limite por IP e corpo pequeno (F13-19). Pega erros na tela de entrar, no cadastro e no portal por link, que são onde o primeiro contato acontece.
   - (b) Só com login: menos exposta, mas cega antes do login.

## 5. Impacto nos documentos

- **ERS (já atualizada no PR da spec):**
  - **RNF-OPS-01:** erros com contexto e sem dado pessoal, nos logs estruturados.
  - **RNF-OPS-02:** a operação é avisada de erro novo, falha de job, queda, deploy e backup, com deduplicação.
  - **RNF-OPS-03:** os erros do app no navegador são registrados, sem dado pessoal.
  - **RF-SUG-01:** "Sugerir melhoria" para a equipe, guardada fora dos tenants.
  - A seção 4 ganha a vigia e o Discord no diagrama.
- **ADR 0012 (novo):** avisos de operação pelo Discord. Trata da porta na API, da deduplicação, dos canais, da vigia no lugar do UptimeRobot e dos logs JSON no formato do Error Reporting, sem SDK. Emenda o §monitoramento do ADR 0010.
- **Contrato da API:** a seção nova "Operação" com `POST /api/v1/erros-web` e `POST /api/v1/sugestoes`; o §1 lista a rota pública nova; os jobs não mudam de resposta.
- **Modelo de domínio:** `Sugestao`, global, fora dos tenants, com o id da empresa e o da pessoa só como referência. A ocorrência de erro fica só na Infrastructure: não é domínio.
- **Segurança:**
  - §6: a exceção da sugestão, se for D4(a); erros do app sem dado pessoal.
  - §9: o limite da rota de erros e o das sugestões.
  - Tenant excluído (RNF-PRIV-01): as sugestões dele perdem o id da pessoa.
- **Deploy e CI:**
  - §9 reescrito;
  - secrets novos no lugar de `DISCORD_WEBHOOK`;
  - `App__Versao` e `Logs__Formato` na API;
  - `VITE_VERSAO` no build do SPA;
  - o Worker da vigia no deploy.
- **Setup da infra:** a parte E reescrita (canais, webhooks, KV da vigia, sem UptimeRobot se for D3(a)); a checklist muda.
- **Convenções:** log estruturado (sem dado pessoal, rota modelo) e "aviso de operação" (porta, menções desligadas, falha nunca derruba a requisição).
- **Read side:** nenhum.

## 6. Divisão em PRs

Cada PR contra a `main`, sem empilhar, com testes e CI verde. F13a vem primeiro; os outros dependem só do que já estiver na `main`.

| PR | Entrega | Cenários |
|---|---|---|
| F13a | Formatador de log JSON (Api), contexto da requisição no erro, `Logs__Formato` e `App__Versao`; ADR 0012 e convenção de log | F13-01 a F13-04 |
| F13b | Porta de avisos (Application), adaptador do Discord (Infrastructure), tabela de ocorrências (migration), aviso no 500, expurgo no diário | F13-05 a F13-12 |
| F13c | Avisos dos jobs: falhas do tick e do diário, resumo do diário | F13-13, F13-14 |
| F13d | `POST /api/v1/erros-web` (API) e captura no app (tela de erro, janela, portal) | F13-15 a F13-21 |
| F13e | Sugestões na API: entidade global, migration, rota, limite, aviso e reenvio no tick | F13-23 a F13-30 (API) |
| F13f | Sugestões no app: item do menu, janela, ajuda e Novidades | F13-22, F13-23 e F13-25 (front) |
| F13g | Workflows: canais do deploy e do backup, secrets novos, `VITE_VERSAO` e `App__Versao` no deploy | F13-31 a F13-33 |
| F13h | Worker da vigia (`infra/cloudflare/vigia`), testes e deploy | F13-34 a F13-36 |

## 7. Registro

- 09/11 — rascunho.
- 10/11 — o Daniel aprovou todas as recomendações (D1 a D6) e pediu o PR da spec.
- 10/11 — F13b: a porta é `IMensageiroOperacao` (canal e texto) e `IAvisosDeErro` (Application/Abstractions/Operacao); `MensageiroDiscord` e `AvisosDeErro` na Infrastructure. A deduplicação usa um escopo próprio, porque o `DbContext` da requisição que falhou pode estar numa transação abortada. Sem banco, o aviso sai deduplicado só em memória, com a nota "sem banco" (o erro pode ser o banco, e o `/api/health` não o consulta). Os avisos resumidos pelo teto contam como avisados (quietos por 24 h), mas não entram no teto da hora seguinte. O log do HttpClient do Discord fica desligado: ele registra a URL, que é o segredo. Webhook que não é https impede a API de subir. 4xx não avisa. Os webhooks entram no `deploy.yml` no F13g (sem eles, produção não avisa; não há deploy ainda).
- 10/11 — F13a: ADR 0012 aberto. No modo JSON, o diagnóstico do `ExceptionHandlerMiddleware` é suprimido, para o mesmo 500 não contar duas vezes no Error Reporting; com isso os 4xx de regra de negócio também deixam de ser erro (`LogDoErroTests › ErroDeRegra_4xx_NaoViraErroNoLog`). A pilha vai em `stack_trace` (o F13-02 dizia "na `message`"; mesmo efeito no Error Reporting, com a mensagem limpa). A correlação com o trace do Cloud Logging (`logging.googleapis.com/trace`) fica para conferir no primeiro deploy. `Logs__Formato` e `App__Versao` já entram no `deploy.yml` aqui (estavam previstos no F13g): sem eles o F13a não vale em produção. `Logs__Formato` com outro valor que não `json` impede a API de subir (`ComposicaoLogsTests › AddLogsAcordu_FormatoDesconhecido_NaoSobe`).
