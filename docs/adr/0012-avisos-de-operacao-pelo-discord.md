# ADR 0012 — Avisos de operação pelo Discord e logs estruturados

- **Status:** Aceito
- **Data:** 10/11/2026
- **Contexto do projeto:** Acordu (marco F13, antes do primeiro deploy). Emenda o monitoramento do ADR 0010.

## Contexto

Até o F12, o ADR 0010 e o deploy-ci previam três fontes de aviso:

- os logs de texto do Cloud Run, que só se leem abrindo o console;
- o Discord, para deploy e backup;
- o e-mail, para os jobs (Cloud Monitoring), o orçamento e a queda (UptimeRobot).

O monitoramento de erros de aplicação ficou para decisão própria (spec do F8, decisão 3), porque o Sentry é SDK de fornecedor.

Antes do primeiro cliente, a Nebrys precisa saber, sem abrir painel, de erro novo, job que falhou, queda, deploy e backup. Também precisa receber as sugestões das equipes. Tudo dentro dos planos gratuitos e sem SDK de provedor (RNF-POR-01).

## Decisão

1. **Logs estruturados, sem SDK** (RNF-OPS-01):
   - Com `Logs__Formato=json`, a API escreve uma linha JSON por entrada. Os campos que o Cloud Logging lê do stdout são `severity`, `message` e `time`.
   - O erro sai no formato que o Error Reporting agrupa: a pilha em `stack_trace`, o `@type` de evento de erro e o `serviceContext`, com o serviço e a versão de `App__Versao`. A `message` fica limpa.
   - Dos escopos, só entram a rota modelo, o método e os identificadores de tenant e usuário.
   - O ASP.NET também grava um log para cada exceção tratada pelo handler. No modo JSON esse diagnóstico é suprimido (`SuppressDiagnosticsCallback`): o mesmo 500 não conta duas vezes, e os 4xx de regra de negócio não viram erro. A falha do próprio handler continua no log.
   - A mensagem de exceção não carrega dado pessoal: `Include Error Detail` (Npgsql) e `EnableSensitiveDataLogging` (EF) ficam proibidos fora do desenvolvimento.
   - Sem a variável, sai o console de texto de sempre. O Error Reporting é o lugar de investigar, e não manda aviso.
2. **A própria aplicação avisa** (RNF-OPS-02), por uma porta de "avisos de operação" na Application. A Infrastructure implementa a porta postando no webhook do Discord por HTTP.
   - Tem deduplicação: uma tabela global de ocorrências, fora dos tenants; um aviso por erro a cada 24 h; teto de 20 por hora.
   - As menções vão desligadas.
   - A falha do aviso nunca muda a resposta ao usuário.
   - Sem webhook configurado, nada sai.
   - O que derruba a API inteira não passa por aqui. Isso fica com a vigia (item 4) e com a conferência de saúde do deploy.
3. **Três canais, cada um com webhook próprio** em variável de ambiente:
   - **#alertas:** erros, jobs, queda, deploy e backup que falharam;
   - **#deploy:** deploy ok, heartbeat do backup e resumo do diário;
   - **#sugestoes.**
4. **Vigia no lugar do UptimeRobot:** um Worker do Cloudflare com Cron Trigger de 5 min, que guarda o estado no KV.
   - Avisa a queda depois de duas checagens seguidas falhando, e avisa a volta.
   - O ping de 5 min continua aquecendo a instância como antes (ADR 0010 §7).
5. **Fica como estava:** o orçamento, por e-mail. O alerta de não-2xx do Scheduler, por e-mail, passa a ser só a reserva.

## Alternativas consideradas

- **Error Reporting → Cloud Monitoring → webhook com repasse:** pega a queda na partida. Mas é configuração manual no Google, não se testa na CI e prende o alerta ao provedor.
- **Sentry gratuito:** SDK de terceiro, contra o ADR 0010.
- **Deduplicação em memória:** o Cloud Run desliga a instância várias vezes por dia, e cada partida repetiria todos os avisos.
- **UptimeRobot com Discord:** as fontes divergem sobre a integração e sobre o uso comercial no plano gratuito (dez/2024).

## Consequências

- O Discord vira a central de operação, e os webhooks são segredos: não entram em log nem no repositório.
- A tabela de ocorrências e a de sugestões são as primeiras tabelas globais de negócio, fora dos tenants. Nenhuma rota as lê (teste de arquitetura).
- Trocar de hospedagem não muda os avisos. Os campos do log seguem a convenção do Google, mas são só JSON no stdout: outro coletor lê igual.
- O Error Reporting acrescenta `serviceContext` à cota de logs, sem custo à parte no volume do nível 0.
