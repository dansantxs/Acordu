# ADR 0005 — Hospedagem serverless gratuita (Cloud Run + Neon + Cloudflare Pages), com plano de escala

- **Status:** Aceito
- **Data:** 05/10/2026
- **Contexto do projeto:** Acordu (v1)
- **Substitui (níveis 0–2):** a hospedagem herdada no ADR 0001 (ADR 0005 do Saldu, núcleo self-hosted em VM) e as adaptações de infraestrutura compartilhada (mesma VM, mesmo Postgres, Caddy de borda, OCI Object Storage), equivalentes ao ADR 0009 do Saldu. O modelo compartilhado volta no **nível 3**.

## Contexto

O Acordu ia rodar na VM Oracle Cloud Always Free (Ampere ARM) do Saldu, com Caddy e PostgreSQL compartilhados numa stack de infraestrutura. O Saldu **perdeu a conta Oracle Cloud**: a VM, os buckets e o runbook deixaram de existir, e a hospedagem dos dois produtos foi redefinida.

Restrições que continuam valendo:
- **Custo zero** enquanto não houver usuário pagante.
- **Dev solo:** operar servidor (SO, patches, backup de disco, TLS) compete com o tempo de produto.
- **Portabilidade:** sair de um provedor não pode exigir reescrever código — a perda da conta Oracle mostrou que isso acontece sem aviso.

Hoje não há free tier de VM confiável e sem cartão no Brasil; os planos gratuitos que existem são de serviços gerenciados que escalam a zero.

## Decisão

### 1. Componentes (nível 0)

| Peça | Escolha | Observações |
|---|---|---|
| API | **.NET 10 em container no Google Cloud Run**, região **`us-east4`** | Região Tier 1: o free tier do Cloud Run não vale em `southamerica-east1` (São Paulo). |
| Banco | **Neon**, projeto próprio do Acordu, **Postgres 17**, região **`aws-us-east-1`** | Mesma costa da API. Não é compartilhado com o Saldu. |
| SPA | **Cloudflare Pages** (se/quando houver o front) | Estático, CDN, sem custo. |
| Imagens | **Artifact Registry** em `us-east4`, com **política de limpeza** | Mantém só as últimas versões; a cota gratuita de armazenamento é pequena. |
| Arquivos | Buckets privados no Cloud Storage: `acordu-anexos` (anexos) e `acordu-backups` (dumps) | Acesso pela API XML compatível com S3 (chave HMAC), sem SDK. Localização numa região do free tier do Cloud Storage. |

### 2. Conta e projeto

- **Projeto Google próprio: `acordu-prod`**, ligado à conta de faturamento compartilhada **`utile-billing`** (Utile Systems). O Saldu tem o projeto dele na mesma conta.
- **A cota grátis do Cloud Run e os 3 jobs gratuitos do Cloud Scheduler são por conta de faturamento**, ou seja, **divididos com o Saldu**. Qualquer uso novo (job agendado, serviço extra) considera o consumo dos dois produtos.
- Isolamento entre produtos fica no nível de **projeto** (IAM, service accounts, buckets), não de VM.

### 3. Código sem provedor

- **Nenhum SDK de provedor** no código (nem Google Cloud, nem Neon, nem Cloudflare). Banco por connection string Npgsql; arquivos por API S3-compatível; e-mail pelo Resend via HTTP.
- **Toda configuração por variável de ambiente** (connection strings, endpoint/credencial S3, chave JWT, chave do Resend, token dos jobs). Trocar de provedor = trocar variáveis.

### 4. Proteções de custo

- **Alerta de orçamento** na `utile-billing`, filtrado pelo projeto `acordu-prod` (alerta não bloqueia gasto; é o aviso para agir).
- **`max-instances` baixo** no Cloud Run (2 no nível 0), para um pico ou abuso não virar fatura.
- Lifecycle nos buckets e limpeza no Artifact Registry, para armazenamento não crescer sem controle.

### 5. Cold start

A API escala a zero; a primeira requisição depois de ociosidade paga a subida do container.
- **Pré-aquecimento pela UI:** ao abrir o app (tela de login/portal), o SPA dispara `GET /api/health` sem esperar a resposta, para a instância subir enquanto o usuário digita.
- **ReadyToRun + imagem pequena + startup CPU boost** (imagem base `aspnet` *chiseled* com ICU e tzdata, que o cálculo de SLA exige).
- **UptimeRobot em `/api/health` a cada 5 min**, mantendo a instância quente na maior parte do tempo.
- **O `/api/health` não consulta o banco.** Manter o Neon acordado com o ping estouraria as **100 CU-h** do plano Free. O Neon suspende a computação ociosa; a primeira consulta depois disso acorda o banco.

### 6. Jobs agendados

Com escala a zero (e CPU só durante requisições), `HostedService` em loop não é confiável. Os jobs viram **endpoints internos** em `/internal/jobs/*`:
- Protegidos por **token em cabeçalho** (`X-Job-Token`, comparado em tempo constante, valor em variável de ambiente); fora do OpenAPI e do portal.
- **Idempotentes** (o mesmo disparo duas vezes não duplica efeito) e **por todos os tenants** numa chamada.
- Disparados por **cron externo**: GitHub Actions (`schedule`) para os diários — recorrências, fechamento automático, backup; **Cloud Scheduler** para o que precisa de frequência alta (alertas de SLA e varredura do outbox a cada poucos minutos), porque o cron do Actions atrasa e cada execução consome minutos do repositório privado. O Acordu usa **no máximo 1** dos 3 jobs gratuitos do Scheduler da conta.
- O outbox de e-mail é despachado logo após o commit da requisição; o job só recolhe o que sobrou.

### 7. Deploy, migrations e backup

- **Deploy por Workload Identity Federation** (GitHub OIDC → service account de deploy no `acordu-prod`). Nenhuma chave JSON de service account existe.
- **Migrations pelo GitHub Actions no endpoint direto do Neon**, com o usuário dono do schema, antes de publicar a revisão nova. **A aplicação usa o endpoint pooled** (PgBouncer em modo transação), com um usuário só de DML. O endpoint direto é necessário para migrations porque o lock de sessão e o DDL não passam bem pelo pooler.
- **Backup diário com `pg_dump`** (cliente 17) pelo Actions → bucket privado **`acordu-backups`**, com retenção por lifecycle. Restore testado antes do primeiro cliente real.

### 8. Plano de escala

| Nível | Quando | O que muda |
|---|---|---|
| **0 — grátis** | Desenvolvimento e primeiros usuários | Esta configuração. Escala a zero. |
| **1 — sem cold start** | Cold start incomoda usuários reais | Cloud Run com **`min-instances=1`**. Primeiro custo fixo (pequeno). |
| **2 — pago gerenciado** | Banco perto de **1 GB** ou de **100 CU-h/mês** (conferir os limites vigentes do Neon Free) | **Neon e Cloud Run pagos**, mesma topologia. |
| **3 — VM no Brasil** | **Usuários pagantes** que justifiquem custo fixo, ou **exigência de dados no Brasil** | **VM na Magalu Cloud em São Paulo** (~R$ 62/mês, 2 vCPU / 4 GB), voltando ao modelo de **Caddy + Postgres compartilhados entre produtos, com nomes por função**. |

**Migração para o nível 3:** `pg_dump` → `pg_restore` no Postgres da VM, troca da connection string e do DNS. Os anexos vão para um object storage S3-compatível com cópia (`rclone`) e troca das variáveis S3. Como o código não conhece o provedor, nada muda na aplicação. Os jobs voltam a poder ser `HostedService` ou cron na VM, chamando os mesmos endpoints.

## Alternativas consideradas

- **Outra VM gratuita (outro provedor):** os free tiers de VM exigem cartão, mudam limites sem aviso (como a Oracle em jun/2026) ou são pequenos demais para .NET + Postgres. Repetiria o risco que acabou de se materializar.
- **Cloud Run em `southamerica-east1`:** menor latência, mas fora do free tier.
- **Cloud SQL:** sem plano gratuito; custo fixo desde o primeiro dia.
- **VM paga no Brasil desde já (nível 3 direto):** custo fixo sem usuário pagante. Fica como destino, não como ponto de partida.

## Consequências

**Positivas**
- Custo zero no v1, sem servidor para administrar (SO, TLS, patches).
- Isolamento entre Acordu e Saldu por projeto e por banco: um não derruba o outro.
- Caminho de escala explícito, com gatilhos medíveis e migração mecânica.

**Negativas / trade-offs**
- **Cold start** da API e do Neon depois de ociosidade (mitigado, não eliminado, no nível 0).
- **~120 ms a mais de latência** por ida e volta a partir do Brasil.
- **Dados nos EUA no v1:** transferência internacional (LGPD, art. 33) precisa constar na política de privacidade; clientes que exigirem dados no Brasil antecipam o nível 3.
- **Limites rígidos do Neon Free** (armazenamento e CU-h): estourar suspende o banco até o próximo ciclo. Monitorar e migrar para o nível 2 antes.
- **Cota gratuita dividida com o Saldu** (Cloud Run e Scheduler na mesma conta de faturamento).
- Jobs dependem de um disparador externo; se o cron falhar, nada roda. Mitigação: falha de disparo ou resposta não-2xx do endpoint vai para o Discord (workflow do Actions e retry do Scheduler). O `/api/health` não pode olhar isso, porque não consulta o banco.
