# Spec — F9: proteção do cadastro contra uso em massa

| | |
|---|---|
| **Status** | Concluída (código); F9-46/47 conferidos no primeiro deploy |
| **Data** | 04/11/2026 |
| **Requisitos** | RF-AUT-06, RF-AUT-07, RNF-SEG-10 (decididos em 04/11), RF-AUT-01, RF-AUT-04, RNF-SEG-08 |
| **ADRs** | 0001, 0002, 0010, 0011 |

## 1. Objetivo

Abrir o cadastro ao público sem que ele vire um canal de envio em massa com o domínio do Acordu: uma empresa nova só manda e-mail a terceiros depois que quem a criou confirmar o e-mail, o cadastro passa por Turnstile e por um limite por IP, e o cadastro abandonado some sozinho em 7 dias.

## 2. Cenários de aceite

"Liberada" = a empresa já pode enviar a terceiros (`Tenant.EnvioLiberadoEm` preenchido, gravado na primeira vez que o e-mail do criador fica confirmado, por qualquer caminho). "Sem confirmação", no expurgo, também é `EnvioLiberadoEm` nulo. Os nomes de campo, status e tipo de erro seguem as recomendações da §4 e mudam se a decisão for outra.

### Cadastro (RNF-SEG-10)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F9-01 | Turnstile ligado (segredo configurado) | `POST /auth/registro` com `tokenTurnstile` que o `siteverify` aprova (ação `registro`) | `201`, como hoje: usuário, empresa e sessão criados | `ProtecaoCadastroTests › Registro_TokenAprovado_CriaEEnviaSegredoEIp` |
| F9-02 | Turnstile ligado | o registro vem sem `tokenTurnstile` ou com um que o `siteverify` recusa | `400 validacao` em `errors.tokenTurnstile`; nada é criado e nenhum e-mail sai | `ProtecaoCadastroTests › Registro_SemToken_400SemChamarOCloudflare`; `ProtecaoCadastroTests › Registro_TokenRecusado_400ENadaCriado` |
| F9-03 | Turnstile ligado | o `siteverify` não responde a tempo ou responde erro | cadastro recusado (decisão 5c), sem criar nada | `ProtecaoCadastroTests › Registro_CloudflareForaDoAr_RecusaPedindoNovaTentativa`; `ProtecaoCadastroTests › Registro_CloudflareEstouraOTempo_RecusaPedindoNovaTentativa`; `ProtecaoCadastroTests › Registro_CloudflareRespondeErro_Recusa`; `ProtecaoCadastroTests › Registro_SegredoDoServidorRecusado_TrataComoIndisponivel` |
| F9-04 | Turnstile ligado | o `siteverify` aprova um token de outro hostname ou de outra ação | `400` em `errors.tokenTurnstile` | `ProtecaoCadastroTests › Registro_TokenDeOutroSiteOuOutraAcao_Recusa` |
| F9-05 | Turnstile desligado (sem segredo: dev, testes, E2E) | o registro vem sem `tokenTurnstile` | `201`; o servidor não chama o `siteverify` | `ProtecaoCadastroTests › Registro_TurnstileDesligado_CriaSemToken` |
| F9-06 | 3 cadastros do mesmo IP na última hora | chega o 4º | `429 limite-requisicoes`; outro IP cadastra normalmente | `ProtecaoCadastroTests › Registro_QuartoDoMesmoIpNaHora_429_OutroIpSegue`; `ProtecaoCadastroTests › ChaveDoIp_Ipv4OuIpv6_AgrupaIpv6Por64` |
| F9-07 | o limite de cadastro do IP esgotado | o mesmo IP faz login, renova a sessão ou pede redefinição de senha | segue pelo limite próprio de cada rota; o do cadastro não interfere | `ProtecaoCadastroTests › OutrasRotasDeAuth_LimiteDoCadastroEsgotado_Seguem` |
| F9-08 | um IP | faz 3 tentativas de cadastro recusadas (Turnstile ou validação) e uma 4ª válida | a 4ª recebe `429`: tentativa recusada também conta | `ProtecaoCadastroTests › Registro_TentativasRecusadas_ContamNoLimite`; `ProtecaoCadastroTests › Registro_RecusasDoTurnstile_ContamNoLimite` |

### Envio a terceiros (RF-AUT-06)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F9-09 | empresa recém-cadastrada | o criador lê `GET /auth/eu` | `emailConfirmado: false` e `tenant.envioLiberado: false` | `EnvioATerceirosTests › Eu_EmpresaRecemCadastrada_NaoLiberada` |
| F9-10 | empresa não liberada | alguém **sem** `usuario.convidar` chama `POST /convites` (ou sem `portal.gerenciar_acessos` nas rotas do portal) | `403 sem-permissao`: a permissão é conferida antes do bloqueio | `EnvioATerceirosTests › Convites_SemPermissaoNaEmpresaNaoLiberada_Retorna403` |
| F9-11 | empresa não liberada | alguém com `usuario.convidar` chama `POST /convites` | `422 email-nao-confirmado` (decisão 1); nenhum convite gravado, nenhum e-mail | `EnvioATerceirosTests › ConvidarEquipe_EmpresaNaoLiberada_Retorna422SemGravarNemEnviar` |
| F9-12 | empresa não liberada | alguém com `portal.gerenciar_acessos` convida (ou reconvida) um contato para o portal: `POST /clientes/{id}/contatos/{contatoId}/acesso-portal` | `422 email-nao-confirmado`; acesso não criado, nenhum e-mail | `EnvioATerceirosTests › ConvidarPortal_EmpresaNaoLiberada_Retorna422SemAcessoNemEmail` |
| F9-13 | empresa não liberada | alguém emite link de acesso de uma solicitação (`POST /solicitacoes/{id}/acessos-portal`), com ou sem `enviarEmail` | `422 email-nao-confirmado`; nenhum link emitido, nem só na resposta | `EnvioATerceirosTests › EmitirLink_EmpresaNaoLiberada_Retorna422ComOuSemEmail` |
| F9-14 | empresa não liberada | alguém altera o escopo de um acesso ao portal (`PATCH …/acesso-portal`, não envia e-mail) | segue funcionando como hoje | `EnvioATerceirosTests › AlterarEscopo_EmpresaNaoLiberada_SegueFuncionando` |
| F9-15 | empresa não liberada, cliente com contato | a equipe abre uma solicitação para o contato | a solicitação é criada; o contato não recebe e-mail e nenhum link de acesso é gerado | `AvisosBloqueadosTests › AbrirSolicitacao_EmpresaNaoLiberada_CriaSemAvisarNemGerarLink` |
| F9-16 | empresa não liberada | um aviso automático ao contato seria enviado (mensagem externa, aguardando você, resolvida) | não sai e-mail nem link; a operação que o causou não falha | `AvisosBloqueadosTests › AvisosAoContato_EmpresaNaoLiberada_OperacoesSeguemSemEmail` |
| F9-17 | empresa não liberada | um aviso interno é para o próprio criador (ex.: SLA em risco, consumo de contrato) | sino sempre; e-mail conforme a decisão 8 | `AvisosBloqueadosTests › AvisoInterno_EmpresaNaoLiberada_EmailSoAoCriadorESinoATodos` |
| F9-18 | empresa não liberada | o criador pede o reenvio da confirmação ou a redefinição de senha | os e-mails da conta saem normalmente: são para ele mesmo | `EnvioATerceirosTests › EmailsDaConta_EmpresaNaoLiberada_Saem` |
| F9-19 | empresa não liberada | o criador confirma o e-mail pelo link | `envioLiberado: true` em `/auth/eu`; convites, links e avisos voltam a sair a partir daí | `EnvioATerceirosTests › ConfirmarEmail_EmpresaNaoLiberada_LiberaConvitesELinks` |
| F9-20 | empresa não liberada | o criador redefine a senha pelo link do e-mail (que já confirma o e-mail, contrato §2) | a empresa fica liberada, como na confirmação | `EnvioATerceirosTests › RedefinirSenha_EmpresaNaoLiberada_Libera` |
| F9-21 | avisos automáticos bloqueados antes da confirmação | o criador confirma o e-mail | os avisos bloqueados não saem depois (decisão 6) | `AvisosBloqueadosTests › ConfirmarEmail_DepoisDeAvisosBloqueados_NaoEntregaOsAntigos`; `EnvioATerceirosTests › PodeAvisar_EventoAnteriorALiberacao_Descarta` (nova tentativa da outbox) |
| F9-22 | empresa liberada | o criador deixa a equipe (há outro Dono) ou muda de papel | a empresa continua liberada (decisão 2) | `EnvioATerceirosTests › TrocarPapel_CriadorRebaixado_EmpresaContinuaLiberada` |
| F9-23 | pessoa sem conta | aceita um convite da equipe ou do portal | a conta nasce com `emailConfirmado: true` (regressão: já é assim) | `EnvioATerceirosTests › AceitarConvite_ContaNova_NasceConfirmada` |
| F9-24 | empresa A não liberada e empresa B liberada | B convida, emite link e avisa contatos | B envia normalmente; o bloqueio é por empresa | `EnvioATerceirosTests › Envios_EmpresaLiberadaAoLadoDeBloqueada_SaemSoNaLiberada` |
| F9-25 | usuário com vínculo em A (não liberada, ele é o criador) e em B (liberada, convidado) | ele age em B | B envia normalmente; o estado de A não vaza para B | `EnvioATerceirosTests › Convidar_UsuarioNaoConfirmadoEmEmpresaLiberada_Envia` |
| F9-26 | o mesmo usuário do F9-25 | ele abre o sistema em B | não vê o texto de envio bloqueado (B está liberada); o lembrete de confirmar o e-mail continua, porque é da conta | `casca.test.tsx › conta não confirmada em empresa liberada: sem o aviso de envio parado`; `conta.test.tsx › no app, lembra de confirmar o e-mail e reenvia o link`; `› empresa não liberada: só a faixa da casca, sem repetir o lembrete na Hoje` |
| F9-27 | empresas que já existiam antes do F9 | a migration roda | ficam liberadas e com o criador preenchido (o Dono mais antigo) | `MigrationsTests › PreenchimentoDoF9_CriadorEhODonoMaisAntigo_EEmpresaFicaLiberada` |

### Expurgo (RF-AUT-07)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F9-28 | empresa com `CriadoEm` há 7 dias ou mais (no instante do diário), não liberada, sem dados (decisão 3) | roda o `POST /internal/jobs/diario` | a empresa e tudo dela são apagados, com o usuário (que só tinha esse vínculo); a resposta traz `cadastrosRemovidos: 1` | `ExpurgoCadastrosTests › Diario_CadastroAbandonadoSemDados_ApagaEmpresaECriador` |
| F9-29 | a mesma empresa com menos de 7 dias desde `CriadoEm` | roda o diário | nada é apagado | `ExpurgoCadastrosTests › Diario_CadastroComMenosDeSeteDias_Fica` |
| F9-30 | empresa com 7 dias ou mais, não liberada, com um dado que conta (ex.: um cliente) | roda o diário | nada é apagado | `ExpurgoCadastrosTests › Diario_CadastroAbandonadoComCliente_Fica` |
| F9-31 | empresa com 7 dias ou mais, liberada, sem dados | roda o diário | nada é apagado | `ExpurgoCadastrosTests › Diario_EmpresaLiberadaSemDados_Fica` |
| F9-32 | criador sem confirmação com vínculo em outra empresa | a empresa dele é expurgada | o usuário e o outro vínculo ficam; só a empresa e os vínculos dela saem | `ExpurgoCadastrosTests › Diario_CriadorComVinculoEmOutraEmpresa_FicaComOOutroVinculo` |
| F9-33 | sessão aberta na empresa expurgada | o navegador renova a sessão | `401`: os refresh tokens daquela empresa foram apagados | `ExpurgoCadastrosTests › Refresh_DepoisDoExpurgo_Retorna401` |
| F9-34 | access token ainda válido da empresa expurgada | chega `GET /auth/eu` ou outra rota interna | `401`: o vínculo não existe mais | `ExpurgoCadastrosTests › Eu_ComAccessTokenDaEmpresaExpurgada_Retorna401` |
| F9-35 | uma empresa expurgável e outras com dados | roda o diário | só a expurgável muda; dados, auditoria e usuários das outras ficam intactos (isolamento) | `ExpurgoCadastrosTests › Diario_ComExpurgo_NaoTocaOutrasEmpresas` |
| F9-36 | o diário já expurgou | roda de novo | `cadastrosRemovidos: 0` (idempotente) | `ExpurgoCadastrosTests › Diario_RodadoDeNovo_NaoRemoveMais` |
| F9-37 | o expurgo de uma empresa falha | roda o diário | a falha é logada e conta em `falhas`; as demais empresas e os outros passos do diário seguem | `ExpurgoCadastrosTests › Diario_ExpurgoDeUmaEmpresaFalha_ContaEmFalhasESegueComAsDemais` (falha forçada por trigger) |
| F9-38 | uma entidade nova com `TenantId` | o teste de classificação roda | falha se ela não estiver classificada como "dado" ou "do registro/rastro" para o expurgo | `ClassificacaoDoExpurgoTests › EntidadesComTenant_TodasClassificadas_NumaListaSo` (integração: o modelo do EF é a fonte) |

### Front

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F9-39 | `VITE_TURNSTILE_CHAVE` definida | a tela de cadastro abre | mostra o desafio; o botão só envia com o token; o token vai em `tokenTurnstile` | `turnstile.test.tsx › desenha o desafio com a chave e manda o token no cadastro`; `› sem o token, o botão não envia`; `› Enter num campo sem o token não envia o cadastro`; `› token vencido volta a travar o botão`; `› ao sair da tela, o widget é removido`; `› script do Cloudflare que não carrega avisa na tela` |
| F9-40 | sem `VITE_TURNSTILE_CHAVE` | a tela de cadastro abre | sem desafio; o cadastro funciona como hoje | `acesso.test.tsx › sem a chave do Turnstile, o cadastro não mostra desafio nem manda token` |
| F9-41 | `400` em `tokenTurnstile` ou `429` no cadastro | a resposta chega | mensagem no formulário; o desafio é renovado | `turnstile.test.tsx › token recusado mostra a mensagem e renova o desafio`; `› limite de cadastros explica que é por hora e renova o desafio` |
| F9-42 | empresa não liberada (`tenant.envioLiberado: false`) | qualquer tela interna abre | o aviso da casca explica que convites, links e avisos a clientes ficam parados até a confirmação, com o "Reenviar link" | `casca.test.tsx › empresa não liberada: toda tela explica o que fica parado e reenvia o link` |
| F9-43 | empresa não liberada | a tela da equipe, do contato ou da solicitação abre | os botões de convidar, convidar para o portal e gerar link ficam desabilitados, com a explicação ao lado (decisão 4) | `equipe.test.tsx`, `clientes/portal.test.tsx` (convidar e reenviar) e `detalhe.test.tsx › empresa não liberada: … desabilitado, com a explicação` (ligada ao botão por `aria-describedby`) |
| F9-44 | empresa não liberada e uma resposta `422 email-nao-confirmado` mesmo assim | a resposta chega | a mensagem da API aparece, sem quebrar a tela | `equipe.test.tsx › mostra a mensagem da API quando o convite é recusado por e-mail não confirmado` |
| F9-45 | o build do SPA | roda `pnpm csp:conferir` | a CSP permite o Turnstile (`script-src` e `frame-src` com `https://challenges.cloudflare.com`) e continua sem `unsafe-inline` | `pnpm csp:conferir` na CI (Cloudflare em `script-src` e `frame-src`; sem `unsafe-inline`, `unsafe-eval` nem `*` em `script-src`) |

### Infra

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F9-46 | o `deploy.yml` | roda sem o secret do Turnstile | para antes de publicar (segredo obrigatório em produção) | revisão do `deploy.yml` (lista de obrigatórias); conferido no primeiro deploy |
| F9-47 | o `deploy.yml` com o secret e a chave do site | roda | a API recebe `Turnstile__Segredo` e o SPA é construído com `VITE_TURNSTILE_CHAVE` | revisão do `deploy.yml` (variável `TURNSTILE_CHAVE_SITE` exigida no build); conferido no primeiro deploy (runbook C5) |

## 3. Fora de escopo

- Lista de domínios de e-mail descartáveis e período de teste atrelado a CNPJ ou cartão (segurança §4: fora por ora; o teste entra com a cobrança, v3).
- Aviso por e-mail ao criador antes do expurgo (ex.: no 5º dia). A ERS não pede; pode virar requisito depois.
- Aceitar convite com conta já existente confirmar o e-mail dessa conta (o token chegou por e-mail). Faz sentido, mas não está na ERS; ver §5. Por isso existe o F9-26: um usuário não confirmado pode estar em outra empresa.
- Limite de envio por empresa liberada (ex.: N convites por dia). O F9 trata só da empresa não liberada.
- Captcha em outras rotas públicas (login, recuperação de senha, convites): seguem só com o rate limit atual.

## 4. Decisões que dependem do Daniel

1. **Status e tipo do erro quando a empresa ainda não pode enviar.**
   - (a) `403` com `type` `email-nao-confirmado`.
   - (b) `422` com `type` `email-nao-confirmado` e `detail` explicando ("Confirme seu e-mail para enviar convites e links…") — **recomendado**. No contrato, `403` é "sem permissão" (RBAC, papel, escopo do link) e `422` é "regra de negócio violada"; aqui a pessoa tem a permissão, o que falta é um estado da empresa que ela mesma resolve. A SPA já mostra o `detail` de `422` sem tratamento especial, e o `type` próprio deixa a tela reconhecer o caso. Entra no contrato §13 (lista de tipos), §2 (o que bloqueia) e nas respostas dessas rotas no OpenAPI.

2. **Quem precisa estar confirmado.**
   - (a) **Só o criador, gravado na empresa, e a liberação também gravada** (`Tenant.CriadoPorUsuarioId` e `Tenant.EnvioLiberadoEm`, preenchida na primeira vez que o e-mail do criador fica confirmado, seja pelo link de confirmação, seja pela redefinição de senha, que também confirma; nunca volta a nulo) — **recomendado**. É o texto da ERS, é estável (trocar e-mail ou sair da equipe depois não bloqueia de novo uma empresa em uso) e o expurgo precisa saber quem é o criador de qualquer jeito. Antes da liberação, o criador é o único Dono possível, porque convites estão bloqueados.
   - (b) Qualquer Dono confirmado libera, calculado a cada envio. Mesmo efeito prático hoje, mas uma consulta a mais em cada envio, e a empresa voltaria a bloquear se todos os Donos confirmados saíssem.

3. **O que conta como "sem dados" para o expurgo, e o usuário com vínculo em outra empresa.**
   - (a) **"Dado" é qualquer registro de negócio**: clientes, contatos, solicitações, tarefas, projetos, contratos, recorrências, apontamentos, anexos, categorias criadas, convites e acessos ao portal. Não contam o que o registro cria (empresa, papéis padrão, vínculo do criador, calendário padrão), os rastros (auditoria, atividade, notificações, outbox, preferências, tokens) e mudanças de configuração (nome, SLA padrão, calendário editado). Um teste de arquitetura obriga a classificar toda entidade com `TenantId` (F9-38) — **recomendado**: segue a ERS e não deixa uma tabela nova esquecida fazer o expurgo apagar dado real.
   - (b) Como (a), mas mudança de configuração também conta como dado (quem personalizou a empresa não é apagado). Mais conservador; a conta descartável que só trocou o nome sobrevive.
   - (c) Só "sem clientes e sem solicitações". Simples, mas apaga, por exemplo, uma empresa que só cadastrou tarefas.
   - **Usuário com vínculo em outra empresa** (em todas as opções): fica, com o outro vínculo; saem só a empresa expurgada e os vínculos e refresh tokens dela (a ERS já diz isso). Na implementação (F9d), a conta também fica se a pessoa já esteve em outra empresa (sessão aberta lá ou conta ligada a um contato), para não deixar autoria órfã; papel criado pela empresa conta como dado; o logotipo do portal, se houver, sai do storage depois do commit.

4. **Comportamento do front.**
   - (a) **Aviso da casca com o motivo, botões de convite e de link desabilitados com a explicação ao lado, e o "Reenviar link" que já existe** — **recomendado**: a pessoa entende antes de tentar, e o `422` fica só como rede de proteção. Exige `tenant.envioLiberado` em `/auth/eu`.
   - (b) Só o aviso da casca; os botões continuam ativos e o `422` explica ao clicar. Menos código, mas a pessoa preenche o formulário do convite para descobrir no fim.

5. **Turnstile em dev, testes e E2E, e onde ficam as chaves.**
   - a) **Desligado por configuração**: sem `Turnstile__Segredo`, a API não exige token (F9-05); sem `VITE_TURNSTILE_CHAVE`, o SPA não mostra o desafio. Testes, E2E e o bot das capturas ficam sem rede externa. O validador é testado com um `HttpMessageHandler` falso (as chaves de teste do Cloudflare ficaram de fora: a resposta delas traz outro hostname e seria recusada pela conferência) — **recomendado**.
   - b) Chaves de teste do Cloudflare (sempre aprovam) em dev e na CI. Exercita o caminho real, mas os testes passam a depender de `challenges.cloudflare.com`.
   - **Onde ficam** (nas duas): a chave do site é pública e vai como variável do repositório `TURNSTILE_CHAVE_SITE`, usada no build do Pages (`VITE_TURNSTILE_CHAVE`). O segredo vai no secret `API_TURNSTILE_SEGREDO` e chega ao Cloud Run como `Turnstile__Segredo`, obrigatório no `deploy.yml` (F9-46). O widget é criado no painel do Cloudflare com o hostname `acordu.app.br`, num passo próprio do runbook (C5). Também entram no deploy-ci §7 e na tabela de segredos do runbook.
   - **`siteverify` fora do ar (5c):** recusar o cadastro (fail-closed), porque aceitar sem conferir abriria a porta justo quando o Cloudflare está com problema. Como recusar:
     - (c1) `400` em `tokenTurnstile` com "Não deu para conferir agora; tente de novo" — **recomendado**: sem tipo novo, e a tela já mostra erro de campo e renova o desafio. O defeito é dizer "erro no formulário" quando a falha é de fora.
     - (c2) `503` com `type` novo `servico-indisponivel`. Mais correto semanticamente; mais um tipo no contrato e um tratamento na tela.
   - O servidor confere `success`, a `action` (`registro`) e o `hostname` (o de `App__UrlPublica`).
   - Segunda barreira opcional: a API não subir em Production sem `Turnstile__Segredo`. Não recomendo agora: a fumaça da imagem na CI sobe em Production sem segredos, e a trava do `deploy.yml` (F9-46) já cobre a produção.

6. **Avisos automáticos bloqueados.**
   - (a) **Descartados**, com log (quantidade, nunca o endereço) — **recomendado**. São avisos de momento ("recebemos sua solicitação", "aguardando você"); entregues dias depois, confundem o cliente. O sino da equipe não é afetado, e o link de acesso não é gerado.
   - (b) Guardados na outbox até a confirmação. A outbox tem tentativas e backoff, não espera: precisaria de um estado novo e de um gatilho na confirmação, e a confirmação dispararia uma rajada de e-mails velhos.

7. **Ordem em relação ao F8.**
   - (a) **F9 na `main` antes do primeiro deploy**: o provisionamento está pausado até a decisão do domínio, e quando voltar já sobe protegido — **recomendado**. A fumaça F8-32 passa a incluir o Turnstile.
   - (b) Primeiro deploy sem o F9, com o cadastro fechado por configuração. Exige uma chave nova (`Cadastro__Aberto`) só para esse intervalo.
   - (c) Deploy sem proteção e cadastro aberto. Não recomendado: é o risco que o PR #132 descreve.

8. **Aviso interno ao próprio criador numa empresa não liberada.** A ERS diz "não envia e-mail a ninguém além dele" e, na mesma frase, "avisos automáticos a contatos e à equipe não saem". As duas leituras divergem só para o criador, que é a única pessoa da equipe antes da liberação.
   - (a) **O e-mail ao criador sai** (SLA, consumo, menção): ele é o dono do endereço, e não há abuso possível. Ajusta a redação da RF-AUT-06 para "à equipe, exceto o criador" — **recomendado**.
   - (b) Nenhum e-mail automático sai, nem ao criador; só o sino. Segue a letra da ERS e é mais simples de explicar ("até confirmar, só sai o e-mail de confirmação").

## 5. Impacto nos documentos

- **Contrato da API:** §2 (`tokenTurnstile` no registro; `tenant.envioLiberado` em `/auth/eu`; o que fica bloqueado e quando libera, inclusive pela redefinição de senha), §3 e §4.2 (o `422` nos convites da equipe e do portal), §7 (o `422` no link de acesso), §12a e §12b (e-mails ao contato e internos que não saem), §13 (tipo `email-nao-confirmado`; `servico-indisponivel` se a decisão 5 for c2). OpenAPI regenerado. O job diário fica fora do OpenAPI; a resposta (`cadastrosRemovidos`) é documentada no deploy-ci §5.
- **Modelo de domínio:** `Tenant` ganha `CriadoPorUsuarioId` e `EnvioLiberadoEm` (decisão 2a), com a regra de liberação.
- **Segurança:** §4 (como a liberação é gravada); §9 (limite do cadastro com a chave `RateLimit__Registro__PorHora`, Turnstile fail-closed, CSP com `challenges.cloudflare.com` em `script-src` e `frame-src`).
- **Deploy-ci:** §5 (o diário roda também o expurgo de cadastros), §7 (`Turnstile__Segredo`, `RateLimit__Registro__PorHora`, chave do site no build do SPA).
- **Setup-infra:** passo novo C5 (widget do Turnstile) e os nomes na tabela "Segredos do GitHub".
- **Convenções:** §4, `VITE_TURNSTILE_CHAVE` na lista de variáveis do SPA.
- **ERS:** RF-AUT-06, ajuste de redação se a decisão 8 for (a). Proposta opcional, para você decidir depois: aceitar convite com conta existente confirma o e-mail dela (fora de escopo aqui).
- **Spec do F8:** F8-32 (fumaça pós-deploy) passa a confirmar o e-mail antes de convidar o contato e a passar pelo Turnstile.
- **ADR:** nenhum. Turnstile por HTTP, sem SDK, segue o ADR 0010.

## 6. Divisão em PRs

Cada PR contra a `main`, sem empilhar, com testes e CI verde. A migration sai pelo bot (`.github/migration-request`).

| PR | Entrega | Cenários |
|---|---|---|
| F9a | API: `Tenant.CriadoPorUsuarioId` e `EnvioLiberadoEm` (migration com o preenchimento das empresas existentes), liberação ao confirmar o e-mail (link e redefinição), `422` nos convites e no link, `envioLiberado` em `/auth/eu`, contrato e OpenAPI | F9-09 a F9-14, F9-18 a F9-20, F9-22 a F9-25, F9-27 |
| F9b | API: avisos automáticos a terceiros barrados num ponto só (antes do envio), sem link de acesso; avisos ao próprio criador seguem | F9-15 a F9-17, F9-21 |
| F9c | API: Turnstile (validador HTTP, desligado sem segredo, fail-closed) e limite de cadastro por IP, próprio e no lugar do de `/auth` nessa rota (o ASP.NET aplica uma política por rota; os 3/h já são mais restritos) | F9-01 a F9-08 |
| F9d | Job: expurgo no diário, classificação das entidades e teste de arquitetura | F9-28 a F9-38 |
| F9e | Front: desafio no cadastro, aviso da casca, botões desabilitados, `422` tratado, CSP | F9-26, F9-39 a F9-45 |
| F9f | Infra: `deploy.yml` (segredo obrigatório, chave do site no build), runbook e deploy-ci | F9-46, F9-47 |

Ordem: F9a primeiro (criador, liberação, `422` e `envioLiberado`); depois F9b e F9d, que usam o criador e a liberação. F9c não depende de nenhum. F9e precisa de F9a e F9c na `main` (`envioLiberado` e `tokenTurnstile` nos tipos do OpenAPI). F9f entra quando quiser, antes do primeiro deploy.

## 7. Registro

- 04/11 — F9e na main (PR #138). F9f: `deploy.yml` exige `API_TURNSTILE_SEGREDO` e `TURNSTILE_CHAVE_SITE`; runbook C5 (widget e conferência do hostname); F8-32 passa pelo Turnstile e pela confirmação do e-mail.
- 04/11 — F9e, revisão: explicação ligada aos botões por `aria-describedby` (inclusive "Criar conta" sem o token); reenviar convite com a explicação; testes do script que falha, do token vencido e da desmontagem. A conferir no primeiro deploy (F9f, C5): o hostname que o Turnstile devolve com `Referrer-Policy: same-origin` no SPA.
- 04/11 — F9c na main (PR #137). F9e: aviso da empresa não liberada na casca (o lembrete de confirmar a conta segue na Hoje), botões de convite e link desabilitados com a explicação, desafio no cadastro com renovação a cada recusa e mensagem própria para o limite por hora; CSP com o Cloudflare.
- 04/11 — F9c, revisão: limite do cadastro numa janela só (o encadeado criava timers por IP sem descarte), IPv6 por /64, segredo recusado vira indisponível com log de erro, hostname com o padrão dos links.
- 04/11 — F9d na main (PR #136). F9c: `IVerificacaoHumana` (Turnstile por HTTP na Infrastructure; desligada sem segredo), conferida no endpoint antes de tudo; política `registro` com o limite de `/auth` encadeado ao por hora.
- 04/11 — F9d, revisão: transação serializável e a empresa apagada primeiro com a condição repetida (corrida com a confirmação); conta mantida se houver rastro em outra empresa; papel não padrão conta como dado; logotipo removido; F9-37 com teste.
- 04/11 — F9b na main (PR #135). F9d: `ExpurgoCadastros` no diário (`cadastrosRemovidos`); vínculo de outra pessoa conta como dado; a classificação é conferida contra o modelo do EF num teste de integração.
- 04/11 — F9a na main (PR #134). F9b: avisos barrados em `EnvioATerceiros.PodeAvisarAsync`, chamado por `AvisosAoContato` (antes de gerar o link) e `AvisoInterno` (exceto o criador); RF-AUT-06 da ERS com a redação da decisão 8.
- 04/11 — spec aprovada (PR #133, todas as recomendações da §4); F9a em implementação.
- 04/11 — rascunho. Requisitos do PR #132. Revisão: liberação também pela redefinição de senha, precedência 403/422, rota real do convite do portal, aviso da casca pelo estado da empresa, decisão 8 (e-mail ao próprio criador), ordem dos PRs.
