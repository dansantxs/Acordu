# Spec — F9: proteção do cadastro contra uso em massa

| | |
|---|---|
| **Status** | Rascunho |
| **Data** | 04/11/2026 |
| **Requisitos** | RF-AUT-06, RF-AUT-07, RNF-SEG-10 (decididos em 04/11), RF-AUT-01, RF-AUT-04, RNF-SEG-08 |
| **ADRs** | 0001, 0002, 0010, 0011 |

## 1. Objetivo

Abrir o cadastro ao público sem que ele vire um canal de envio em massa com o domínio do Acordu: uma empresa nova só manda e-mail a terceiros depois que quem a criou confirmar o e-mail, o cadastro passa por Turnstile e por um limite por IP, e o cadastro abandonado some sozinho em 7 dias.

## 2. Cenários de aceite

"Liberada" = a empresa já pode enviar a terceiros (o criador confirmou o e-mail). Os nomes de campo, status e tipo de erro seguem as recomendações da §4 e mudam se a decisão for outra.

### Cadastro (RNF-SEG-10)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F9-01 | Turnstile ligado (segredo configurado) | `POST /auth/registro` com `tokenTurnstile` que o `siteverify` aprova (ação `registro`) | `201`, como hoje: usuário, empresa e sessão criados | — |
| F9-02 | Turnstile ligado | o registro vem sem `tokenTurnstile` ou com um que o `siteverify` recusa | `400 validacao` em `errors.tokenTurnstile`; nada é criado e nenhum e-mail sai | — |
| F9-03 | Turnstile ligado | o `siteverify` não responde a tempo ou responde erro | cadastro recusado (decisão 5c), sem criar nada | — |
| F9-04 | Turnstile desligado (sem segredo: dev, testes, E2E) | o registro vem sem `tokenTurnstile` | `201`; o servidor não chama o `siteverify` | — |
| F9-05 | 3 cadastros do mesmo IP na última hora | chega o 4º | `429 limite-requisicoes`; outro IP cadastra normalmente | — |
| F9-06 | o limite de cadastro do IP esgotado | o mesmo IP faz login, renova a sessão ou pede redefinição de senha | segue pelo limite próprio de cada rota; o do cadastro não interfere | — |
| F9-07 | um IP | faz 3 tentativas de cadastro recusadas (Turnstile ou validação) e uma 4ª válida | a 4ª recebe `429`: tentativa recusada também conta | — |

### Envio a terceiros (RF-AUT-06)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F9-08 | empresa recém-cadastrada | o criador lê `GET /auth/eu` | `emailConfirmado: false` e `tenant.envioLiberado: false` | — |
| F9-09 | empresa não liberada | alguém com `usuario.convidar` chama `POST /convites` | `422 email-nao-confirmado` (decisão 1); nenhum convite gravado, nenhum e-mail | — |
| F9-10 | empresa não liberada | alguém com `portal.gerenciar_acessos` convida um contato para o portal (inclusive pelo reenvio, se a rota envia e-mail) | `422 email-nao-confirmado`; acesso não criado, nenhum e-mail | — |
| F9-11 | empresa não liberada | alguém emite link de acesso de uma solicitação (`POST /solicitacoes/{id}/acessos-portal`) | `422 email-nao-confirmado`; nenhum link emitido | — |
| F9-12 | empresa não liberada, cliente com contato | a equipe abre uma solicitação para o contato | a solicitação é criada; o contato não recebe e-mail e nenhum link de acesso é gerado | — |
| F9-13 | empresa não liberada | um aviso automático ao contato seria enviado (mensagem externa, aguardando você, resolvida) | não sai e-mail nem link; a operação que o causou não falha | — |
| F9-14 | empresa não liberada | um aviso interno é para o próprio criador (ex.: SLA em risco, consumo de contrato) | sino e e-mail saem normalmente: é o próprio dono do e-mail | — |
| F9-15 | empresa não liberada | o criador confirma o e-mail pelo link | `envioLiberado: true` em `/auth/eu`; convites, links e avisos voltam a sair a partir daí | — |
| F9-16 | avisos automáticos bloqueados antes da confirmação | o criador confirma o e-mail | os avisos bloqueados não saem depois (decisão 6) | — |
| F9-17 | empresa liberada | o criador deixa a equipe (há outro Dono) ou muda de papel | a empresa continua liberada (decisão 2) | — |
| F9-18 | pessoa sem conta | aceita um convite da equipe ou do portal | a conta nasce com `emailConfirmado: true` (regressão: já é assim) | — |
| F9-19 | empresa A não liberada e empresa B liberada | B convida, emite link e avisa contatos | B envia normalmente; o bloqueio é por empresa | — |
| F9-20 | usuário com vínculo em A (não liberada, ele é o criador) e em B (liberada, convidado) | ele age em B | B envia normalmente; o estado de A não vaza para B | — |
| F9-21 | empresas que já existiam antes do F9 | a migration roda | ficam liberadas e com o criador preenchido (o Dono mais antigo) | — |

### Expurgo (RF-AUT-07)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F9-22 | empresa criada há mais de 7 dias, criador sem confirmação, sem dados (decisão 3) | roda o `POST /internal/jobs/diario` | a empresa e tudo dela são apagados, com o usuário (que só tinha esse vínculo); a resposta traz `cadastrosRemovidos: 1` | — |
| F9-23 | a mesma empresa com menos de 7 dias | roda o diário | nada é apagado | — |
| F9-24 | empresa com mais de 7 dias, sem confirmação, com um dado que conta (ex.: um cliente) | roda o diário | nada é apagado | — |
| F9-25 | empresa com mais de 7 dias, criador confirmado, sem dados | roda o diário | nada é apagado | — |
| F9-26 | criador sem confirmação com vínculo em outra empresa | a empresa dele é expurgada | o usuário e o outro vínculo ficam; só a empresa e os vínculos dela saem | — |
| F9-27 | sessão aberta na empresa expurgada | o navegador renova a sessão | `401`: os refresh tokens daquela empresa foram apagados | — |
| F9-28 | uma empresa expurgável e outras com dados | roda o diário | só a expurgável muda; dados, auditoria e usuários das outras ficam intactos (isolamento) | — |
| F9-29 | o diário já expurgou | roda de novo | `cadastrosRemovidos: 0` (idempotente) | — |
| F9-30 | o expurgo de uma empresa falha | roda o diário | a falha é logada e conta em `falhas`; as demais empresas e os outros passos do diário seguem | — |
| F9-31 | uma entidade nova com `TenantId` | o teste de arquitetura roda | falha se ela não estiver classificada como "dado" ou "do registro/rastro" para o expurgo | — |

### Front

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F9-32 | `VITE_TURNSTILE_CHAVE` definida | a tela de cadastro abre | mostra o desafio; o botão só envia com o token; o token vai em `tokenTurnstile` | — |
| F9-33 | sem `VITE_TURNSTILE_CHAVE` | a tela de cadastro abre | sem desafio; o cadastro funciona como hoje | — |
| F9-34 | `400` em `tokenTurnstile` ou `429` no cadastro | a resposta chega | mensagem no formulário; o desafio é renovado | — |
| F9-35 | empresa não liberada | qualquer tela interna abre | o aviso da casca explica que convites, links e avisos a clientes ficam parados até a confirmação, com o "Reenviar link" | — |
| F9-36 | empresa não liberada | a tela da equipe, do contato ou da solicitação abre | os botões de convidar, convidar para o portal e gerar link ficam desabilitados, com a explicação ao lado (decisão 4) | — |
| F9-37 | empresa não liberada e uma resposta `422 email-nao-confirmado` mesmo assim | a resposta chega | a mensagem da API aparece, sem quebrar a tela | — |
| F9-38 | o build do SPA | roda `pnpm csp:conferir` | a CSP permite o Turnstile (`script-src` e `frame-src` com `https://challenges.cloudflare.com`) e continua sem `unsafe-inline` | — |

### Infra

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F9-39 | o `deploy.yml` | roda sem o secret do Turnstile | para antes de publicar (segredo obrigatório em produção) | — |
| F9-40 | o `deploy.yml` com o secret e a chave do site | roda | a API recebe `Turnstile__Segredo` e o SPA é construído com `VITE_TURNSTILE_CHAVE` | — |

## 3. Fora de escopo

- Lista de domínios de e-mail descartáveis e período de teste atrelado a CNPJ ou cartão (segurança §4: fora por ora; o teste entra com a cobrança, v3).
- Aviso por e-mail ao criador antes do expurgo (ex.: no 5º dia). A ERS não pede; pode virar requisito depois.
- Aceitar convite com conta já existente confirmar o e-mail dessa conta (o token chegou por e-mail). Faz sentido, mas não está na ERS; ver §5.
- Limite de envio por empresa liberada (ex.: N convites por dia). O F9 trata só da empresa não liberada.
- Captcha em outras rotas públicas (login, recuperação de senha, convites): seguem só com o rate limit atual.

## 4. Decisões que dependem do Daniel

1. **Status e tipo do erro quando a empresa ainda não pode enviar.**
   - (a) `403` com `type` `email-nao-confirmado`.
   - (b) `422` com `type` `email-nao-confirmado` e `detail` explicando ("Confirme seu e-mail para enviar convites e links…") — **recomendado**. No contrato, `403` é "sem permissão" (RBAC, papel, escopo do link) e `422` é "regra de negócio violada"; aqui a pessoa tem a permissão, o que falta é um estado da empresa que ela mesma resolve. A SPA já mostra o `detail` de `422` sem tratamento especial, e o `type` próprio deixa a tela reconhecer o caso. Entra no contrato §13 (lista de tipos), §2 (o que bloqueia) e nas respostas dessas rotas no OpenAPI.

2. **Quem precisa estar confirmado.**
   - (a) **Só o criador, gravado na empresa, e a liberação também gravada** (`Tenant.CriadoPorUsuarioId` e `Tenant.EnvioLiberadoEm`, preenchida quando ele confirma; nunca volta a nulo) — **recomendado**. É o texto da ERS, é estável (trocar e-mail ou sair da equipe depois não bloqueia de novo uma empresa em uso) e o expurgo precisa saber quem é o criador de qualquer jeito. Antes da liberação, o criador é o único Dono possível, porque convites estão bloqueados.
   - (b) Qualquer Dono confirmado libera, calculado a cada envio. Mesmo efeito prático hoje, mas uma consulta a mais em cada envio, e a empresa voltaria a bloquear se todos os Donos confirmados saíssem.

3. **O que conta como "sem dados" para o expurgo, e o usuário com vínculo em outra empresa.**
   - (a) **"Dado" é qualquer registro de negócio**: clientes, contatos, solicitações, tarefas, projetos, contratos, recorrências, apontamentos, anexos, categorias criadas, convites e acessos ao portal. Não contam o que o registro cria (empresa, papéis padrão, vínculo do criador, calendário padrão), os rastros (auditoria, atividade, notificações, outbox, preferências, tokens) e mudanças de configuração (nome, SLA padrão, calendário editado). Um teste de arquitetura obriga a classificar toda entidade com `TenantId` (F9-31) — **recomendado**: segue a ERS e não deixa uma tabela nova esquecida fazer o expurgo apagar dado real.
   - (b) Como (a), mas mudança de configuração também conta como dado (quem personalizou a empresa não é apagado). Mais conservador; a conta descartável que só trocou o nome sobrevive.
   - (c) Só "sem clientes e sem solicitações". Simples, mas apaga, por exemplo, uma empresa que só cadastrou tarefas.
   - **Usuário com vínculo em outra empresa** (em todas as opções): fica, com o outro vínculo; saem só a empresa expurgada e os vínculos e refresh tokens dela (a ERS já diz isso).

4. **Comportamento do front.**
   - (a) **Aviso da casca com o motivo, botões de convite e de link desabilitados com a explicação ao lado, e o "Reenviar link" que já existe** — **recomendado**: a pessoa entende antes de tentar, e o `422` fica só como rede de proteção. Exige `tenant.envioLiberado` em `/auth/eu`.
   - (b) Só o aviso da casca; os botões continuam ativos e o `422` explica ao clicar. Menos código, mas a pessoa preenche o formulário do convite para descobrir no fim.

5. **Turnstile em dev, testes e E2E, e onde ficam as chaves.**
   - a) **Desligado por configuração**: sem `Turnstile__Segredo`, a API não exige token (F9-04); sem `VITE_TURNSTILE_CHAVE`, o SPA não mostra o desafio. Testes, E2E e o bot das capturas ficam sem rede externa. O validador é testado com um `HttpMessageHandler` falso, inclusive com as respostas das chaves de teste do Cloudflare — **recomendado**.
   - b) Chaves de teste do Cloudflare (sempre aprovam) em dev e na CI. Exercita o caminho real, mas os testes passam a depender de `challenges.cloudflare.com`.
   - **Onde ficam** (nas duas): a chave do site é pública e vai como variável do repositório `TURNSTILE_CHAVE_SITE`, usada no build do Pages (`VITE_TURNSTILE_CHAVE`). O segredo vai no secret `API_TURNSTILE_SEGREDO` e chega ao Cloud Run como `Turnstile__Segredo`, obrigatório no `deploy.yml` (F9-39). O widget é criado no painel do Cloudflare com o hostname `acordu.app.br` (runbook C3). Também entram no deploy-ci §7 e na tabela de segredos do runbook.
   - c) **`siteverify` fora do ar:** recusar o cadastro (fail-closed) com `400` em `tokenTurnstile` e "Não deu para conferir agora; tente de novo" — **recomendado**, sem tipo de erro novo. Aceitar sem conferir abriria a porta exatamente quando o Cloudflare está com problema.

6. **Avisos automáticos bloqueados.**
   - (a) **Descartados**, com log (quantidade, nunca o endereço) — **recomendado**. São avisos de momento ("recebemos sua solicitação", "aguardando você"); entregues dias depois, confundem o cliente. O sino da equipe não é afetado, e o link de acesso não é gerado.
   - (b) Guardados na outbox até a confirmação. A outbox tem tentativas e backoff, não espera: precisaria de um estado novo e de um gatilho na confirmação, e a confirmação dispararia uma rajada de e-mails velhos.

7. **Ordem em relação ao F8.**
   - (a) **F9 na `main` antes do primeiro deploy**: o provisionamento está pausado até a decisão do domínio, e quando voltar já sobe protegido — **recomendado**. A fumaça F8-32 passa a incluir o Turnstile.
   - (b) Primeiro deploy sem o F9, com o cadastro fechado por configuração. Exige uma chave nova (`Cadastro__Aberto`) só para esse intervalo.
   - (c) Deploy sem proteção e cadastro aberto. Não recomendado: é o risco que o PR #132 descreve.

## 5. Impacto nos documentos

- **Contrato da API:** §2 (`tokenTurnstile` no registro; `tenant.envioLiberado` em `/auth/eu`; o que fica bloqueado e quando libera), §3 e §4.2 (o `422` em convites e link), §13 (tipo `email-nao-confirmado`), §5 do job diário ou onde estão os jobs (`cadastrosRemovidos`). OpenAPI regenerado.
- **Modelo de domínio:** `Tenant` ganha `CriadoPorUsuarioId` e `EnvioLiberadoEm` (decisão 2a), com a regra de liberação.
- **Segurança:** §4 (como a liberação é gravada); §9 (limite do cadastro com a chave `RateLimit__Registro__PorHora`, Turnstile fail-closed, CSP com `challenges.cloudflare.com` em `script-src` e `frame-src`).
- **Deploy-ci:** §5 (o diário roda também o expurgo de cadastros), §7 (`Turnstile__Segredo`, `RateLimit__Registro__PorHora`, chave do site no build do SPA).
- **Setup-infra:** widget do Turnstile (C3) e os nomes na tabela "Segredos do GitHub".
- **Convenções:** §4, `VITE_TURNSTILE_CHAVE` na lista de variáveis do SPA.
- **ERS:** nenhuma mudança obrigatória. Proposta opcional, para você decidir depois: aceitar convite com conta existente confirma o e-mail dela (fora de escopo aqui).
- **ADR:** nenhum. Turnstile por HTTP, sem SDK, segue o ADR 0010.

## 6. Divisão em PRs

Cada PR contra a `main`, sem empilhar, com testes e CI verde. A migration sai pelo bot (`.github/migration-request`).

| PR | Entrega | Cenários |
|---|---|---|
| F9a | API: `Tenant.CriadoPorUsuarioId` e `EnvioLiberadoEm` (migration com o preenchimento das empresas existentes), liberação ao confirmar o e-mail, `422` nos convites e no link, `envioLiberado` em `/auth/eu`, contrato e OpenAPI | F9-08 a F9-11, F9-15, F9-17 a F9-21 |
| F9b | API: avisos automáticos a terceiros barrados num ponto só (antes do envio), sem link de acesso; avisos ao próprio criador seguem | F9-12 a F9-14, F9-16 |
| F9c | API: Turnstile (validador HTTP, desligado sem segredo, fail-closed) e limite de cadastro por IP | F9-01 a F9-07 |
| F9d | Job: expurgo no diário, classificação das entidades e teste de arquitetura | F9-22 a F9-31 |
| F9e | Front: desafio no cadastro, aviso da casca, botões desabilitados, `422` tratado, CSP | F9-32 a F9-38 |
| F9f | Infra: `deploy.yml` (segredo obrigatório, chave do site no build), runbook e deploy-ci | F9-39, F9-40 |

F9a vem primeiro (o `422` e o `envioLiberado` são a base do F9b e do F9e); F9c, F9d e F9f são independentes entre si.

## 7. Registro

- 04/11 — rascunho. Requisitos do PR #132.
