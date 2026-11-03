# ADR 0010 — Hospedagem serverless gratuita (Cloud Run + Neon + Cloudflare), com o Acordu como único produto

- **Status:** Aceito
- **Data:** 26/10/2026
- **Contexto do projeto:** Acordu (v1)
- **Revoga:** [ADR 0006](0006-retorno-oracle-always-free.md) (VM Oracle compartilhada).
- **Retoma, com ajustes:** [ADR 0005](0005-hospedagem-serverless-gratuita.md). Este ADR é autocontido: o 0005 fica só como histórico.
- **Emenda:** ADRs 0001 (hospedagem), 0007 (pré-requisito de deploy), 0008 (IP real) e 0009 (tempo real no nível 0).

## Contexto

A conta Oracle Cloud foi **desativada de vez**. Abrir outra só é possível com CNPJ, e isso fica para quando o produto tiver receita. A VM compartilhada do ADR 0006 deixou de existir, e não há outro produto da Nebrys em operação: **o Acordu é o único produto**, e nada da infraestrutura é compartilhado.

Restrições que continuam valendo:
- **Custo zero** enquanto não houver usuário pagante.
- **Dev solo:** sem servidor para administrar.
- **Portabilidade:** perder um provedor não pode exigir reescrever código. Já aconteceu duas vezes.

Limites gratuitos conferidos em 26/10/2026 (mudam sem aviso; conferir de novo antes de provisionar):

| Serviço | Limite do plano gratuito |
|---|---|
| Cloud Run (cobrança por requisição) | 180.000 vCPU-s, 360.000 GiB-s e 2 milhões de requisições por mês, **por conta de faturamento**. A instância é cobrada **enquanto houver requisição aberta**. Free tier só nas regiões Tier 1 (`us-east4` é; `southamerica-east1` não). |
| Cloud Scheduler | 3 jobs por conta de faturamento. |
| Neon Free | 1 GB por projeto, 100 CU-h por projeto por mês, computação até 2 CU, **suspensão após 5 min ocioso** (não desligável). |
| Cloudflare R2 | 10 GB-mês, 1 milhão de operações classe A e 10 milhões classe B por mês, saída gratuita. Sem versionamento de objetos. |
| Cloudflare Workers | 100 mil requisições por dia. |
| Cloudflare Pages | Estático, sem limite de banda. |

## Decisão

### 1. Componentes (nível 0)

| Peça | Escolha | Observações |
|---|---|---|
| SPA | **Cloudflare Pages**, em `acordu.app.br` | `www` e `acordu.com.br` redirecionam para o apex. |
| Borda da API | **Cloudflare Worker** em `api.acordu.app.br` | Proxy fino para a URL `*.run.app` do serviço (§3). |
| API | **.NET 10 em container no Google Cloud Run**, `us-east4` | Imagem **linux/amd64** (o Cloud Run não executa ARM). |
| Imagens | **Artifact Registry** em `us-east4`, com política de limpeza | A cota gratuita de armazenamento é pequena. |
| Banco | **Neon**, projeto `acordu`, **Postgres 17**, `aws-us-east-1` | Mesma costa da API. |
| Arquivos | **Cloudflare R2**: `acordu-anexos` e `acordu-backups`, privados | API S3 com região `auto`. Backup do banco fica num provedor diferente do banco. |
| Jobs | **Cloud Scheduler** (frequentes e diário) e **GitHub Actions** (backup) | §5. |
| E-mail | **Resend**, sem mudança | |

### 2. Conta e projeto

- Projeto Google **`acordu-prod`** na conta de faturamento **`nebrys-billing`**, que hoje só tem o Acordu. **As cotas gratuitas do Cloud Run e do Scheduler são todas dele.** Um produto novo na mesma conta passa a dividi-las e exige rever este ADR.
- Conta Cloudflare da Nebrys: DNS, Pages, Worker e R2.

### 3. Domínios e sessão

- **SPA em `acordu.app.br`; API em `api.acordu.app.br`.** Como `app.br` é sufixo público, os dois são o **mesmo site**: o cookie `acordu_refresh` com `SameSite=Strict` continua sendo enviado, e o ADR 0007 vale sem mudança de modelo. As chamadas passam a ser de outra **origem**: CORS da API restrito a `https://acordu.app.br`, com credenciais, e o SPA envia `credentials: 'include'` nas rotas `/api/v1/auth/*`.
- **Por que um Worker e não o mapeamento de domínio do Cloud Run:** o mapeamento existe em `us-east4`, mas está em *Preview* e a própria documentação do Google o desaconselha para produção, por latência. O balanceador de carga do Google resolveria, mas tem custo fixo. O Worker é gratuito no volume do nível 0, fica no DNS que já é do Cloudflare e é trocável por mapeamento ou balanceador sem mudar o código.
- **Token de origem:** o Worker acrescenta `X-Origem-Token` (segredo do Worker e da API). Com `Proxy__TokenOrigem` configurado, a API responde 404 a qualquer requisição sem o token. Assim, a URL `*.run.app` não serve de atalho para pular o rate limit, e o IP que o Worker repassa é confiável.
- **IP real:** o Worker grava `CF-Connecting-IP` como primeiro salto do `X-Forwarded-For`; o front do Google acrescenta o IP do Worker. A API confia em **dois saltos** (`Proxy__Saltos=2`), só com o token de origem ativo.
- **Feed ICS da agenda:** passa a ter base própria (`App__UrlApi` = `https://api.acordu.app.br/`), separada de `App__UrlPublica` (links do SPA nos e-mails).

### 4. Tempo real no nível 0 (emenda ao ADR 0009)

Uma conexão SSE aberta mantém uma requisição ativa, e o Cloud Run cobra a instância durante todo esse tempo. 180.000 vCPU-s são cerca de 50 horas por mês com 1 vCPU: uma equipe com a Agenda aberta no expediente esgota a cota.

- **SSE desligado por configuração** (`TempoReal__Sse=false`: `/api/v1/eventos` responde 404).
- **Polling no SPA:** a cada 30 s, só nas telas de Agenda e Quadro e só com a aba visível, mais a revalidação ao focar a janela que o TanStack Query já faz. O modo vem de `VITE_TEMPO_REAL` (`polling` ou `sse`).
- O endpoint SSE, o difusor e o leitor do front **continuam no código e testados**. Voltam a valer no nível 1 (`min-instances=1`, cobrança por instância) ou numa VM.

### 5. Jobs

Os endpoints `/internal/jobs/*` já existem: `X-Job-Token`, idempotentes, todos os tenants por chamada. O disparo passa a ser externo, pela borda (`https://api.acordu.app.br/internal/jobs/...`).

| Job do Scheduler | Agenda (`America/Sao_Paulo`) | Endpoint |
|---|---|---|
| `acordu-tick` | `*/15 7-19 * * 1-6` (7h00 a 19h45, segunda a sábado) | `/internal/jobs/tick` |
| `acordu-diario` | `40 6 * * *` | **novo** `/internal/jobs/diario`, que roda em sequência `fechamento-automatico`, `expurgo-auditoria` e `expurgo-notificacoes` e soma as contagens |

- **Por que 15 min e só no expediente:** o Neon suspende depois de 5 min sem uso e cobra o tempo acordado. Um tick a cada 5 min deixaria o banco ligado o dia todo (mais de 100 CU-h). Com 15 min, cada disparo acorda o banco por ~5 min: cerca de 1/3 das 13 horas, ~28 CU-h/mês na computação mínima (0,25 CU), sobrando o resto para uso real. O SLA conta só horas úteis, então a madrugada não importa.
- **Custo aceito:** o alerta de "em risco" ou "violado" pode chegar até 15 min depois. Tenant com expediente fora da janela recebe o alerta no primeiro tick seguinte. Os e-mails continuam saindo na própria requisição; o tick só recolhe falhas.
- **Backup** fica no GitHub Actions (`pg_dump` não chama a API). Os endpoints diários individuais continuam existindo, para uso manual.
- Usa **2 dos 3 jobs gratuitos** do Scheduler; sobra um.

### 6. Banco (Neon)

- Banco `acordu`; `acordu_migrator` (dono do schema, só no Actions, **endpoint direto**) e `acordu_app` (só DML, **endpoint pooled**). O lock de sessão das migrations e o `pg_dump` não passam bem pelo pooler em modo transação.
- Autoscaling da computação entre **0,25 e 1 CU**, para um pico não consumir as CU-h do mês.
- **`/api/health` não consulta o banco** (já é assim): o ping externo não pode manter o Neon acordado.

### 7. Cold start

- Pré-aquecimento pelo SPA (nota de 03/11, F8d: a renovação da sessão que o SPA já faz ao carregar qualquer tela acorda o container da API, como faria o `/api/health`, que também não toca o banco; então não há chamada separada), ReadyToRun, imagem `aspnet` *chiseled extra* (com ICU e tzdata, exigidos pelo SLA) e *startup CPU boost*.
- UptimeRobot em `https://api.acordu.app.br/api/health` a cada 5 min. Cada ping é uma requisição de milissegundos, pouco perto da cota.

### 8. Deploy, migrations e backup

- **Deploy por Workload Identity Federation** (OIDC do GitHub → `acordu-deployer`), restrito a `dansantxs/acordu` na `main`. Nenhuma chave JSON de service account.
- Ordem: build amd64 → push no Artifact Registry → migrations (bundle do EF Core no endpoint direto) → nova revisão → healthcheck → em caso de falha, tráfego de volta à revisão anterior.
- **Backup diário** com `pg_dump -Fc` (cliente 17) pelo Actions para `acordu-backups`, com token R2 que só escreve nesse bucket. Retenção de 14 dias por regra de lifecycle. Restore testado antes do primeiro cliente real.
- **Anexos sem versionamento** (o R2 não oferece). A aplicação só apaga objeto ao substituir o logotipo do portal. O token da API é restrito a `acordu-anexos`. Uma cópia dos anexos fora do Cloudflare entra quando houver cliente pagante.

### 9. Proteções de custo

- Alerta de orçamento na `nebrys-billing` (avisa, não bloqueia).
- `max-instances=2` no Cloud Run.
- Limpeza do Artifact Registry e lifecycle no R2.
- Acompanhamento mensal: CU-h e armazenamento no Neon, vCPU-s no Cloud Run, requisições no Worker, armazenamento no R2.

### 10. Regras que valem em todos os níveis

- **Nenhum SDK de provedor no código.** Banco por connection string Npgsql; arquivos pela API S3 (o `AWSSDK.S3` é só cliente do protocolo, com endpoint configurável); e-mail pelo Resend via HTTP.
- **Toda configuração por variável de ambiente.** Trocar de provedor é trocar variáveis e DNS.
- **Migrations aditivas**, para deploy e rollback sem janela.
- **PostgreSQL 17 em todos os ambientes** (dev, Testcontainers, Neon e qualquer destino).
- **Backup diário** fora do provedor do banco, com restore testado.
- **MFA** em todas as contas (Google, Neon, Cloudflare, GitHub, Resend), com códigos de recuperação guardados fora do provedor e um segundo fator cadastrado. Duas contas perdidas mostram que este é o risco principal.

### 11. Plano de escala

| Nível | Gatilho | O que muda |
|---|---|---|
| **0 — grátis** | Desenvolvimento e primeiros usuários | Esta configuração. |
| **1 — sem cold start** | Cold start incomoda usuários reais, ou equipes pedem tempo real | Cloud Run com `min-instances=1` e cobrança por instância; SSE ligado. Primeiro custo fixo. |
| **2 — gerenciado pago** | Banco perto de 1 GB ou de 100 CU-h/mês; Worker perto de 100 mil requisições/dia | Neon pago; Workers pago ou balanceador do Google. Mesma topologia. |
| **3 — decisão com CNPJ** | Usuários pagantes, CNPJ aberto ou cliente que exija dados no Brasil | **Decidir num ADR novo** entre: continuar no gerenciado pago; VM no Brasil só para o Acordu (ex.: Magalu Cloud SP, ~R$ 62/mês, Compose + Caddy + Postgres 17); ou nova conta Oracle com CNPJ. |

**Migração para uma VM:** `pg_dump` → `pg_restore`, `rclone` dos buckets, troca de variáveis e DNS. Na VM, os jobs voltam a ser um timer local chamando os mesmos endpoints, e o SSE volta a ser ligado.

## Alternativas consideradas

- **Vercel + Supabase:** a Vercel não hospeda a API .NET como serviço contínuo, e o Supabase Free pausa o projeto após uma semana sem uso e empurra para o SDK e a autenticação dele.
- **Mapeamento de domínio do Cloud Run:** Preview, com aviso de latência (§3). Fica como troca possível se for promovido.
- **Cloud Storage no lugar do R2:** funciona pela API S3, mas a cota gratuita é pequena, só nas regiões dos EUA, e cobra saída. No R2 a saída é gratuita e o backup fica fora do Google.
- **Manter o SSE no nível 0:** custo desde a primeira equipe ativa.
- **Tick a cada 5 min:** banco acordado o dia inteiro e cota do Neon esgotada.
- **Outra VM gratuita:** sem free tier de VM confiável sem cartão, e o risco de perder a conta é o que acabou de acontecer.

## Consequências

**Positivas**
- Custo zero, sem servidor para administrar, cotas gratuitas sem divisão.
- Código intacto na troca: só configuração, borda e disparo de jobs mudam.
- Banco, computação, arquivos e backup em três provedores: perder uma conta não leva tudo.

**Negativas / trade-offs**
- **Cold start** da API e do Neon depois de ociosidade (mitigado, não eliminado).
- **Latência de ~120 ms** por ida e volta a partir do Brasil, mais o salto do Worker.
- **Sem tempo real por push no nível 0:** mudanças de outra pessoa aparecem em até 30 s.
- **Alertas de SLA com até 15 min de atraso**, e só dentro da janela do Scheduler.
- **Dados nos EUA:** a transferência internacional (LGPD, art. 33) precisa constar na política de privacidade. Cliente que exija dados no Brasil antecipa o nível 3.
- **Limites rígidos do Neon Free:** estourar suspende o banco até o próximo ciclo. O acompanhamento mensal antecipa o nível 2.
- **Jobs dependem do Scheduler:** se o disparo falhar, nada roda. Resposta não-2xx gera alerta por e-mail (Cloud Monitoring).
- **Anexos sem versionamento** até haver cópia fora do Cloudflare.

Fontes: [Cloud Run pricing](https://cloud.google.com/run/pricing), [Cloud Run — mapeamento de domínio](https://docs.cloud.google.com/run/docs/mapping-custom-domains), [Neon — limites do Free](https://neon.com/faqs/free-plan-limits-and-quotas), [R2 pricing](https://developers.cloudflare.com/r2/pricing/), [R2 — API S3](https://developers.cloudflare.com/r2/api/s3/api/).
