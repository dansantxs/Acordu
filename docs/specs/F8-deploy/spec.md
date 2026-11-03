# Spec — F8: deploy no nível 0 (Cloud Run + Neon + Cloudflare)

| | |
|---|---|
| **Status** | Rascunho |
| **Data** | 03/11/2026 |
| **Requisitos** | RNF-POR-01, RNF-DIS-01, RNF-SEG-08, RNF-SEG-09, RNF-AUD-01 (IP real), RNF-LOC-01 (fuso na imagem) |
| **ADRs** | 0007, 0008, 0009, 0010, 0011 |

## 1. Objetivo

O Acordu no ar em `acordu.app.br` e `api.acordu.app.br`, no nível 0 do ADR 0010: o código pronto para a topologia serverless, a imagem da API, a borda no Worker, os workflows de deploy e de backup e o provisionamento feito pelo runbook. Ao fim, uma empresa consegue se cadastrar e usar o sistema e o portal em produção, com backup diário testado.

## 2. Cenários de aceite

A coluna **Teste** é preenchida nos PRs de código. Os cenários de provisionamento e de primeiro deploy são verificados à mão, pelo checklist do `deploy-ci-acordu-v1.md` §10.

### CI (consumo de minutos)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F8-01 | um PR aberto | chega um push na branch dele | a CI roda uma vez (pelo PR), não duas (push e PR) | verificação no Actions |
| F8-02 | um PR que só muda `docs/` ou `*.md` | a CI roda | backend, front, E2E e imagem ficam como pulados (contam como verdes) | verificação no Actions |
| F8-03 | um PR que só muda `apps/web/` | a CI roda | backend e imagem são pulados; front e E2E rodam | verificação no Actions |
| F8-04 | um PR que muda `src/`, `tests/` ou a solução | a CI roda | backend, E2E e imagem rodam; front roda só se `apps/web/` ou `openapi/` mudou | verificação no Actions |
| F8-05 | um push na `main` (merge) | a CI roda | roda tudo, sem filtro, como base para o deploy | verificação no Actions |

### API

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F8-06 | `Jobs__Token` configurado | `POST /internal/jobs/diario` com o token | roda fechamento automático, expurgo da auditoria e expurgo das notificações, nessa ordem, e responde a soma (`fechadas`, `auditoriaRemovida`, `notificacoesRemovidas`, `atividadesRemovidas`, `falhas`) | — |
| F8-07 | `Jobs__Token` configurado | `POST /internal/jobs/diario` sem token ou com token errado | 404 | — |
| F8-08 | um dos três passos do diário falha | o diário roda | os outros passos rodam mesmo assim e a falha entra em `falhas` | — |
| F8-09 | `Proxy__TokenOrigem` configurado | chega qualquer requisição sem `X-Origem-Token` (inclusive `/api/health` e `/internal/jobs/*`) | 404, sem passar por autenticação nem rate limit | — |
| F8-10 | `Proxy__TokenOrigem` configurado | chega uma requisição com o token certo | segue normalmente; o cabeçalho não chega aos logs | — |
| F8-11 | token de origem ativo, `Proxy__ConfiarEncaminhamento=true` e `Proxy__Saltos=2` | chega `X-Forwarded-For: <cliente>, <worker>` | o IP usado na auditoria e no rate limit é o `<cliente>` | — |
| F8-12 | `Proxy__Saltos=2` **sem** token de origem | a API sobe | vale 1 salto (o cliente não forja o IP pulando a borda) | — |
| F8-13 | `App__UrlApi` configurado | a equipe gera o link da agenda (ICS) | o link usa `App__UrlApi`; sem a chave, usa `App__UrlPublica` como hoje | — |
| F8-14 | `TempoReal__Sse=false` | `GET /api/v1/eventos` | 404; sem a chave, o SSE funciona como hoje | — |
| F8-15 | `Cors__Origem=https://acordu.app.br` | preflight de `https://acordu.app.br` para `/api/v1/auth/refresh` | permitido com credenciais; de outra origem, sem os cabeçalhos de CORS | — |

### Imagem

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F8-16 | `docker/api.Dockerfile` | a CI constrói a imagem `linux/amd64` | o container sobe como não-root na porta 8080, responde `200` em `/api/health` e resolve `America/Sao_Paulo` | job de fumaça da imagem |

### SPA

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F8-17 | `VITE_URL_API=https://api.acordu.app.br` no build | o SPA chama a API, renova a sessão ou abre o SSE | usa essa base; sem a variável, a mesma origem (dev com o proxy do Vite) | — |
| F8-18 | base da API em outra origem | o SPA chama `/api/v1/auth/*` | envia `credentials: 'include'` (o cookie `acordu_refresh` vai e volta) | — |
| F8-19 | `VITE_TEMPO_REAL=polling` | a pessoa abre qualquer tela | nenhuma conexão SSE é aberta | — |
| F8-20 | `VITE_TEMPO_REAL=polling` e a aba visível | a Agenda ou o Quadro de tarefas está aberto | os dados recarregam a cada 30 s; com a aba escondida, param | — |
| F8-21 | — | a pessoa abre o login, o cadastro ou o portal | o SPA dispara `GET /api/health` sem esperar a resposta (pré-aquecimento) | — |
| F8-22 | o build publicado no Pages | o navegador pede qualquer rota do SPA | recebe o `index.html` com CSP (`connect-src 'self' https://api.acordu.app.br https://viacep.com.br`) e HSTS | — |

### Borda (Worker)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F8-23 | o Worker com `ORIGEM` e `TOKEN_ORIGEM` | chega uma requisição em `api.acordu.app.br` | repassa método, caminho, query, cabeçalhos e corpo em stream para a origem, com `X-Origem-Token` | — |
| F8-24 | o cliente manda `X-Forwarded-For` próprio | o Worker repassa | o cabeçalho vira só o `CF-Connecting-IP` (o valor do cliente é descartado) | — |
| F8-25 | qualquer resposta da API | o Worker devolve | sem cache (`cache: 'no-store'` na origem) e com o corpo em stream (SSE e downloads) | — |

### Deploy, jobs e backup

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F8-26 | a infraestrutura provisionada pelo runbook | o `deploy.yml` roda | autentica por WIF, publica a imagem com a tag do commit, aplica as migrations no endpoint direto, cria a revisão, confere o health pela borda e publica Worker e SPA | primeiro deploy |
| F8-27 | a migration falha | o `deploy.yml` roda | para antes da nova revisão; a produção segue na anterior | verificação manual |
| F8-28 | o health da nova revisão falha | o `deploy.yml` roda | o tráfego volta para a revisão anterior e o workflow falha | verificação manual |
| F8-29 | os jobs no Scheduler | chega o horário | `acordu-tick` e `acordu-diario` respondem 2xx pela borda; resposta fora de 2xx avisa por e-mail | verificação no Scheduler |
| F8-30 | o `backup.yml` às 04:00 | roda | grava `acordu-backups/db/AAAA-MM-DD.dump` (`pg_dump -Fc`, cliente 17) no R2; falha avisa | primeira execução |
| F8-31 | um backup do dia | o restore é feito num Postgres 17 local | a API sobe apontada para ele e mostra os dados | verificação manual, antes do primeiro cliente |
| F8-32 | produção no ar | uma empresa se cadastra, convida um contato e o contato abre uma solicitação pelo portal | tudo funciona entre `acordu.app.br` e `api.acordu.app.br`, com o e-mail chegando pelo Resend | fumaça manual pós-deploy |

## 3. Fora de escopo

- Nível 1 em diante (`min-instances=1`, SSE ligado, Neon pago); ficam no ADR 0010 §11.
- Ambiente de homologação e branches do Neon por PR.
- Cópia dos anexos fora do Cloudflare (ADR 0010 §8: quando houver cliente pagante).
- Política de privacidade e termos de uso (o item da LGPD fica no checklist, como texto à parte).
- Secret Manager (segredos seguem no GitHub e nas variáveis do Cloud Run).
- Capturas do manual, que não dependem do deploy.

## 4. Decisões que dependem do Daniel

1. **Como o deploy dispara.**
   - (a) **Manual** (`workflow_dispatch` na `main`) até haver cliente pagante; depois, automático a cada merge. **Recomendado**: o repositório é privado no plano Free, que não tem aprovação obrigatória em *environments*; manual é o jeito gratuito de você decidir o que vai ao ar, e cada deploy gasta minutos.
   - (b) Automático a cada merge na `main` desde o início.

2. **Economia de minutos do CI dentro do F8.**
   - (a) **Primeiro PR do marco** (F8a): CI só pelo PR (o push da branch não roda de novo), filtros por caminho em cada job e tudo na `main`. **Recomendado**: o bloqueio de 23/10 veio do consumo, e o F8 acrescenta deploy, imagem e backup no Actions.
   - (b) Deixar como está e acompanhar o consumo.

3. **Monitoramento de erros.**
   - (a) **No F8, só Discord** (falha de deploy, de backup e dos jobs) e os logs do Cloud Run; erro de aplicação com Sentry ou OpenTelemetry vira decisão própria depois. **Recomendado**: o SDK do Sentry é dependência de fornecedor no código (o ADR 0010 §10 pede nenhum SDK de provedor), e o Cloud Run já guarda os logs.
   - (b) Sentry no F8, como está no `deploy-ci` v0.4.

4. **Backup dos anexos (conflito entre a ERS e o ADR 0010).** A RNF-DIS-01 pede backup diário do banco **e dos anexos**; o ADR 0010 §8 deixa os anexos sem cópia até haver cliente pagante.
   - (a) **Ajustar a ERS** para seguir o ADR (anexos com cópia fora do Cloudflare a partir do primeiro cliente pagante). **Recomendado**: copiar o bucket todo dia no Actions gasta minutos e duplica o armazenamento do R2 sem cliente real.
   - (b) Incluir no `backup.yml` uma cópia diária de `acordu-anexos` para `acordu-backups` (rclone).

## 5. Impacto nos documentos

- **`deploy-ci-acordu-v1.md`:** §3 (gatilhos e filtros do CI, job de imagem), §4 (disparo do deploy pela decisão 1), §7 e §9 (Sentry, pela decisão 3), §5 (o diário passa a existir).
- **`contrato-api-acordu-v1.md`:** nenhum (os jobs e o token de origem ficam fora do OpenAPI).
- **`seguranca-modelo-acesso-acordu-v1.md`:** confirmar o token de origem, os dois saltos e o CORS com credenciais.
- **ERS:** RNF-DIS-01, pela decisão 4.
- **ADR 0010:** nota no §8 se a decisão 4 for (b); nenhuma mudança se for (a).
- **Convenções:** variáveis `VITE_*` do SPA e o padrão de filtros da CI.
- **Comentários no código** que ainda citam OCI, Caddy ou VM: `JobsEndpoints`, `ComposicaoSeguranca`, `OrigemDaRequisicao`, `ComposicaoApi`, `ArmazenamentoS3`, `OpcoesArmazenamento`, `EnderecosPublicos` e `vite.config.ts` (vão no PR da área).

## 6. Divisão em PRs

Cada PR contra a `main`, sem empilhar, com testes e CI verde. O provisionamento (`setup-infra-acordu.md`) é seu e pode correr em paralelo com F8b–F8e; o F8f só fecha depois dele.

| PR | Entrega | Cenários |
|---|---|---|
| F8a | CI: só pelo PR, filtros por caminho por job, tudo na `main` (se a decisão 2 for a) | F8-01 a F8-05 |
| F8b | API: `/internal/jobs/diario`, token de origem, saltos, `App__UrlApi`, `TempoReal__Sse`, teste de CORS, comentários | F8-06 a F8-15 |
| F8c | `docker/api.Dockerfile` (amd64, ReadyToRun, chiseled extra, não-root, 8080) e job de fumaça da imagem | F8-16 |
| F8d | SPA: `VITE_URL_API`, credenciais no `/auth`, `VITE_TEMPO_REAL` com polling na Agenda e no Quadro, pré-aquecimento, `_headers` e `_redirects` do Pages | F8-17 a F8-22 |
| F8e | Worker `infra/cloudflare/api-borda` com testes | F8-23 a F8-25 |
| F8f | `deploy.yml` e `backup.yml`; primeiro deploy, jobs no Scheduler, backup e restore conferidos | F8-26 a F8-32 |

## 7. Registro

- 03/11 — rascunho.
