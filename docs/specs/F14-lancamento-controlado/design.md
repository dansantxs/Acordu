# Design — F14: situação da empresa e cadastro controlado

Spec: [spec.md](spec.md). Processo: `docs/processo.md` §3.3. ADRs: 0001, 0002, 0007, 0008, 0010, 0012; criados por este design e aceitos no G2: [0014](../../adr/0014-area-da-nebrys-sessao-de-operacao.md) e [0015](../../adr/0015-empresa-somente-leitura-pela-guarda-de-gravacao.md).

Os valores, as regras e os comportamentos estão nas decisões 1 a 19 do G2 (spec §4, PR #186, 13/11), e o design traz só o **como**. Os valores numéricos ficam em configuração, com os padrões decididos: teste de 30 dias, expurgo 30 dias depois do vencimento ou do cancelamento, e avisos aos 7 e 1 dia antes do fim, no vencimento e 7 dias antes do expurgo. Os comportamentos estão nos cenários; as decisões abaixo dizem onde e como cada um é implementado.

## 1. Decisões

O G2 (PR #186, mesclado em 13/11 pelo Daniel) aprovou o design com a recomendação de cada D. ADRs 0014 e 0015 aceitos. O D16 (18/11) entrou depois e aguarda o G2 da mudança do F14h.

1. **D1 — Como modelar a situação.**
   - (a) **Recomendado.** Enum `SituacaoEmpresa` (`Teste`, `Ativa`, `Vencida`, `Cancelada`) no próprio `Tenant`, com `FimDoTeste` (instante UTC), `SomenteLeituraDesde`, `CanceladaEm`, `GravavelDesde` (última saída do só leitura) e as marcas de aviso (D7). As transições ficam em métodos de domínio (`Vencer`, `Ativar`, `Prorrogar`, `Cancelar`, `Reativar`), com a tabela de transições no modelo de domínio e `TransicaoInvalidaException` para o resto. A data do expurgo é calculada (`SomenteLeituraDesde` + prazo da configuração), não gravada. Por quê: é configuração por tenant, como o fechamento automático e a liberação do envio (modelo §4.11.1 e §4.11.2), e não pede agregado. O histórico fica na trilha (D14).
   - (b) Agregado próprio (`AssinaturaEmpresa`) com histórico. Prepara a cobrança (v3), mas é estrutura sem uso no F14 e uma leitura a mais na guarda (D2).
   - **Demonstração:**
     - (a1) **Recomendado.** Flag `Tenant.Demonstracao` com situação ATIVA, e o domínio recusa as transições. Por quê: ATIVA já fica fora do vencimento e do expurgo, o ciclo fica com os quatro valores da ERS, e só o bloqueio de envio olha a flag.
     - (a2) Um quinto valor no enum: a API mostraria uma situação que a ERS não tem.
   - **Empresas anteriores ao F14** (decisão 13 do G2, F14-08):
     - o preenchimento da migration (`ScriptsDeDados`) as põe em CANCELADA, com `CanceladaEm` e `SomenteLeituraDesde` numa data fixa antiga, `2026-01-01T00:00:00Z`;
     - a data é anterior a qualquer empresa do Acordu e já passou do prazo de expurgo (30 dias, e de qualquer valor razoável), sem depender da configuração, que a migration não lê;
     - entre o deploy do F14a e o do F14i elas ficam só leitura (com o F14b na `main`), sem aviso (F14-113, D7);
     - o primeiro diário com o passo `expurgo-empresas` as apaga pelo caminho normal (F14-112, D6).
   - Conferência: sem conflito com ADR aceito.

2. **D2 — Como aplicar o só leitura.**
   - (a) **Recomendado.** Guarda na gravação, no `AcorduDbContext`, ao lado da de isolamento; classificação obrigatória de toda entidade com `TenantId` ("da empresa" ou "da pessoa ou rastro"); jobs só nas empresas graváveis; outbox descartada; erro próprio (D3). A situação é lida do banco uma vez por escopo, e não do token. Por quê:
     - roda no `SaveChanges`, depois da permissão, do 404 e das regras do caso de uso: a precedência do F14-16 sai sem código em cada um dos 78 `Exigir`;
     - cobre equipe, portal, aceite de convite, jobs e manipuladores num ponto só;
     - entidade nova nasce bloqueada.

     Vale além do marco: **[ADR 0015](../../adr/0015-empresa-somente-leitura-pela-guarda-de-gravacao.md)** (Aceito no G2).
   - (b) Filtro de rota por método HTTP, com as rotas isentas marcadas: roda antes das permissões da Application (trocaria 403 por só leitura) e não cobre jobs nem outbox.
   - (c) Checagem em cada comando, como o `EnvioATerceiros.GarantirLiberadoAsync` do F9: dezenas de pontos, e um esquecido grava.
   - (d) Situação como claim do token: só mudaria no refresh (até 60 min, ADR 0007), contra o F14-65.
   - **Classificação "da pessoa"** (decisão 10 do G2, F14-18, F14-100): sino, preferências de aviso, assinatura da agenda, seguidores, refresh tokens, trilha de auditoria e aceites. As gravações fora do change tracker (`RegistroSeguidores`, `ConsultasNotificacoes`) já são desse grupo, e ponto novo fora do tracker declara a classificação (ADR 0015, item 3).
   - **Convites antigos** (F14-97, F14-98): o aceite cria um `Vinculo`, que é "da empresa". A guarda recusa, e a transação inteira volta, então o convite não é consumido. A sessão de link (F14-99) só grava rastro (auditoria da troca), que passa.
   - Conferência: ADR 0001 (filtro e guarda são a barreira do multi-tenant; a guarda nova fica no mesmo lugar) e ADR 0002 (erro neutro no portal). Sem conflito.

3. **D3 — Tipo do erro de só leitura.**
   - (a) **Recomendado.** `422 empresa-somente-leitura` nas rotas da equipe e no aceite de convite da equipe; `422 portal-indisponivel` com a mensagem neutra da decisão 10 do G2 nas rotas `/portal/*` e `/auth/portal/*`. O `ManipuladorExcecoes` escolhe pela superfície da rota, não pelo contexto: o aceite de convite é rota pública. Por quê: segue o precedente do F9 (`422 email-nao-confirmado`), e um `type` próprio no portal não deixa o contato ler "somente-leitura" na resposta.
   - (b) `403`: mistura com "sem permissão", que o F14-16 precisa distinguir.
   - (c) `423 Locked`: status a mais, sem uso no contrato.

4. **D4 — Operador da Nebrys sem quebrar o isolamento.**
   - (a) **Recomendado.**
     - Lista de e-mails em `Nebrys__Operadores`.
     - Sessão de operação **sem tenant** (`tipo = OPERACAO`, 30 min, sem refresh), aberta por `POST /api/v1/nebrys/sessao`.
     - Rotas `/api/v1/nebrys/*` com um filtro que responde 404 a qualquer outra sessão e ao anônimo.
     - `Acordu.Application.Nebrys` com DTOs próprios e teste de arquitetura contra tipos de negócio.
     - `openapi/nebrys.json` e área própria no SPA (`src/nebrys`, sob demanda).

     Por quê: com o contexto de tenant vazio, os filtros dos ADRs 0001 e 0002 não devolvem dado de negócio nem com defeito na área (F14-62 por construção). É o modelo do portal aplicado à operação. Vale além do marco: **[ADR 0014](../../adr/0014-area-da-nebrys-sessao-de-operacao.md)** (Aceito no G2).
   - (b) Lista de operadores em tabela ou claim do Identity: concede sem deploy, mas cria um papel e uma tela para uma pessoa só.
   - (c) "Tenant da Nebrys" que vê os outros: exceção nos filtros do ADR 0002. Rejeitado.
   - (d) Cloudflare Access na frente da rota: prende a autenticação ao provedor (ADR 0010). Rejeitado no nível 0.
   - **Tabelas globais e o ADR 0012:** o ADR 0012 e o modelo §4.14 dizem que nenhuma rota lê as tabelas globais deles (ocorrências, marcas, sugestões), e testes de arquitetura (`OperacaoTests`) garantem isso. Esses testes ficam como estão. Cada tabela global nova do F14 ganha um teste no mesmo molde, que libera o tipo **só** para `Acordu.Application.Nebrys`, as consultas da área e a feature que grava (ADR 0014, item 7). O teste da `Sugestao` hoje libera o `ExpurgoCadastros` pelo nome; passa a liberar o `ApagadorDeEmpresa` (D6).
   - Conferência: a sessão de operação é um tipo novo, fora dos tenants, como a de link no ADR 0002, e não muda o que vale para equipe e contato (ADR 0001). Sem contradição. O ADR 0014 complementa os anteriores e foi aceito no G2.

5. **D5 — Concorrência das ações da Nebrys** (decisão 15 do G2, F14-107).
   - (a) **Recomendado.** As ações sobre a empresa (ativar, prorrogar, cancelar, reativar) recebem `If-Match` com a `versao` do `Tenant` (`xmin`, `IControleVersao`, convenções §1 e §5), devolvida no detalhe da área. Divergência → `409 conflito-versao`. Sem o cabeçalho, não há checagem, como no resto da API (convenções §5); o SPA da área manda sempre. O vencimento do diário grava pelo change tracker, o que muda o `xmin`. Por quê: é o mecanismo do contrato §1, sem tabela nem campo novo. Entra no ADR 0014, item 5.
   - (b) A última grava, com `422 transicao-invalida` só quando a transição não vale no estado novo (opção que ficou de fora na decisão 15).

6. **D6 — Expurgo da empresa vencida ou cancelada.**
   - (a) **Recomendado.** Extrair do `ExpurgoCadastros` (F9d) um `ApagadorDeEmpresa` (Infrastructure), usado pelo expurgo do F9, pelo passo novo `expurgo-empresas` e pela restauração da demonstração (D13). Ele:
     - apaga `TiposComTenant` e a empresa por `ExecuteDelete` com `IgnoreQueryFilters`. É o ponto revisado da convenções §1 (expurgos dos jobs) e passa **fora** das guardas de isolamento e de só leitura (ADR 0015, item 3). Por isso o F14i depende do F14b, que cria a guarda e a classificação;
     - anonimiza as sugestões;
     - avalia **cada conta** com vínculo na empresa pela regra de rastro fora do F9;
     - remove anexos e logotipo do storage depois do commit, pelas chaves lidas do banco antes de apagar (melhor esforço, com log).

     Cada empresa num escopo e numa transação serializável, com a condição de situação repetida no `DELETE` da empresa: uma reativação concorrente faz o commit falhar, e a falha conta em `falhas`. Por quê: reaproveita o código revisado do F9 e mantém os dois expurgos coerentes.
   - (b) Passo independente: duplica a ordem das remoções e a regra das contas.
   - (c) Anexos por prefixo `{tenantId}/` no storage: pega órfãos, mas exige listar objetos na porta `IArmazenamentoArquivos` e no S3Mock. Fica como melhoria.
   - **Sessão da empresa apagada** (F14-33), pelo mesmo mecanismo do F9 (F9-33, F9-34):
     - os refresh tokens têm `TenantId` e saem com a empresa, então o `POST /auth/refresh` não acha o token e responde `401`;
     - o `GET /auth/eu` carrega o vínculo do `sub` no `tenant_id` do token e responde `401` quando ele não existe mais;
     - nas outras rotas, o access token de até 60 min não enxerga nada (os filtros não têm linhas daquele tenant), e o SPA cai no `401` da próxima renovação.
   - **Empresas canceladas pela migration** (F14-112): têm `SomenteLeituraDesde` na data fixa do D1, e o primeiro diário com o passo as apaga como qualquer outra. Não há passo próprio.
   - **Registro do documento** (D8): não tem referência à empresa e sobrevive ao expurgo da empresa vencida ou cancelada sem passo extra (F14-37). Pela decisão 16 do G2 (F14-104), o expurgo do RF-AUT-07 apaga também a linha do documento da empresa, achada pelo HMAC do documento guardado no `Tenant`.

7. **D7 — Vencimento e avisos no diário.**
   - Ordem dos passos (deploy-ci §5): `vencimento-testes` primeiro, para as recém-vencidas já ficarem fora do fechamento automático; depois `avisos-situacao`, os passos que já existem, e `expurgo-empresas` ao lado de `expurgo-cadastros`. A resposta ganha `empresasVencidas`, `avisosSituacao` e `empresasExpurgadas`.
   - **Marcas dos avisos:**
     - (a) **Recomendado.** Lista de textos no `Tenant` (`AvisosSituacao`), cada um `tipo:data-de-referência` (o fim do teste ou a data do expurgo). O passo calcula, para cada empresa, o aviso devido no instante do diário a partir das datas de referência e das marcas, e grava a marca no mesmo commit em que decide enviar. A data de referência na chave é o que implementa o reenvio depois de prorrogar (F14-101). A comparação "devido e ainda sem marca" implementa o aviso atrasado e o mais próximo do fim (F14-102, F14-103), e as marcas dos avisos dispensados são gravadas junto.
     - Data de referência já passada (F14-113, como as empresas canceladas pela migration): o aviso de antes do expurgo não é devido, e o cálculo só considera avisos cuja data de referência ainda está à frente do instante do diário. O mesmo vale para os avisos de antes do fim do teste.
     - (b) `marcas_operacao` (ADR 0012): global e retida por 7 dias, curta para os avisos de antes do expurgo.
     - (c) Tabela própria de avisos: mais uma tabela para o mesmo efeito.
   - **Como o e-mail sai:**
     - (a) **Recomendado.** O passo envia logo depois do commit, como os e-mails da conta (segurança §4). A falha é logada, conta em `falhas` e remove a marca, para o próximo diário tentar. Os destinatários seguem a regra do `EnvioATerceiros` (criador antes da liberação).
     - (b) Pela outbox do tenant: a outbox descarta as mensagens de empresa só leitura (D2), e os avisos de vencimento e de expurgo são para elas.

8. **D8 — Registro do documento** (decisão 8 do G2).
   - (a) **Recomendado.** HMAC-SHA256 do documento normalizado, com chave em `Documentos__ChaveHmac`, numa tabela global `documentos_teste` (`hash` único, `primeiro_teste_em`, `liberado_em`, `liberacao_usada_em`), **sem referência à empresa nem à pessoa**. Gravada na mesma transação do cadastro. A liberação (F14-71, F14-108) calcula o HMAC do documento digitado e não o guarda. O documento em claro fica só no `Tenant`, enquanto ele existe (F14-61). Por quê: SHA-256 puro de um CPF se desfaz por força bruta (10⁹ combinações). Com a chave fora do banco, o hash não identifica ninguém sozinho.
   - (b) SHA-256 puro: cumpre a letra, mas não protege o CPF.
   - (c) Em claro.
   - **Onde fica registrado:** segurança §4, no F14g, ao lado da regra dos tokens: HMAC com chave em segredo, chave que não gira (os originais não ficam), perda da chave zera a regra para os documentos expurgados, e o que a recusa revela, limitado pelo Turnstile e pelo limite por IP. Não vira ADR: é um detalhe de proteção de dado de uma regra só, e a segurança é o documento dessas regras. A chave é obrigatória no `deploy.yml`; em dev e nos testes há uma chave fixa de desenvolvimento.

9. **D9 — Lista de e-mails descartáveis.**
   - (a) **Recomendado.** Arquivo de texto versionado (um domínio por linha), copiado de uma lista pública de licença livre (ex.: `disposable-email-domains`; licença conferida no PR), lido uma vez por uma porta da Application (`IDominiosDescartaveis`, implementada na Infrastructure). Compara o domínio e os domínios-pai. Atualizações por PR `melhoria`. Por quê: sem dependência em runtime e testável.
   - (b) Baixar em runtime: falha de rede vira resposta sem motivo.
   - (c) Editável pela área: tela e tabela para um volume que não pede.
   - (d) Serviço externo: manda o e-mail a um terceiro.

10. **D10 — Versão dos textos legais e aceite.**
    - (a) **Recomendado.**
      - Textos em Markdown no SPA (`apps/web/src/features/legal/textos/`), com `versao` e `vigencia` no cabeçalho, em páginas públicas que não chamam a API.
      - A API tem as versões vigentes numa constante (`TextosLegais`, Application). O cadastro e o aceite de convite mandam as versões que a pessoa viu, e a API confere com a constante.
      - Um teste do front confere arquivo contra constante (exposta no OpenAPI como `enum` de um campo).
      - Aceites na tabela global `aceites_textos` (conta, texto, versão, data), apagada com a conta.
      - `/auth/eu` traz os textos pendentes, e `POST /auth/textos/ciente` registra.

      Por quê: as páginas abrem sem acordar o Cloud Run e o Neon (ADR 0010 §6), e a API não registra versão forjada.
    - (b) A API serve os textos: uma fonte só, mas cada visita acorda a API e o banco.
    - (c) A API grava a versão que vier: aceite de texto vencido por cliente antigo.
    - **Vira convenção** (convenções §4, no F14f e no F14h): "mudar um texto legal é mudar a versão no arquivo e na constante, no mesmo PR; o teste confere". Por quê: todo PR futuro que mexer nos textos precisa seguir isso. Não é ADR: não há trade-off de arquitetura além deste marco.
    - **Publicação:** o aceite, o registro dele e o "ciente" só valem com os textos publicados (decisão do Daniel, 18/11). Como, no D16.

11. **D11 — Modo do cadastro.**
    - (a) **Recomendado.** `Cadastro__Modo` (`convite` ou `aberto`), **sem padrão em produção**. O `deploy.yml` exige a variável `CADASTRO_MODO` e para antes de publicar sem ela, como o segredo do HMAC. Sem valor, a API trata como `convite` (fail-closed). `appsettings.Development.json`, a fixture dos testes, a E2E e o bot do manual definem `aberto`. `GET /api/v1/auth/cadastro` público responde `{ modo }` para a tela. Por quê: configuração só por ambiente (ADR 0010). Abrir o cadastro é trocar a variável e fazer um deploy (G4), e um esquecimento nunca abre o cadastro.
    - (b) Chave na área (tabela global de configuração): troca sem deploy, mas é mais uma tabela e uma ação para uma troca que acontece uma vez.
    - (c) Também numa variável do SPA: duas fontes que podem divergir.

12. **D12 — Convite de empresa e lista de espera.**
    - (a) **Recomendado.** Entidades globais sem referência a empresa (ADR 0014, item 7):
      - `ConviteEmpresa`: e-mail; token de 32 bytes guardado só em hash; validade `Cadastro__DiasConvite` (7); usado, revogado e entrada de origem;
      - `EntradaListaEspera`: nome; e-mail normalizado com índice único; nome da empresa; mensagem opcional; datas.

      O e-mail do convite sai na requisição, depois do commit, como os da conta: não há tenant para a outbox. A falha volta na resposta. A consulta pública do convite (`POST /auth/registro/convite/consulta`) segue o padrão dos outros convites (401 sem distinguir). A lista de espera é `POST /api/v1/lista-espera`, com Turnstile (ação `lista-espera`, o verificador ganha a ação como parâmetro) e limite `RateLimit__ListaEspera__PorHora` (3). O índice único implementa o "não duplica" do F14-53, e o caso de uso responde igual nos dois caminhos.
    - (b) Convite como `ConviteEquipe` de um tenant que ainda não existe: força um tenant falso. Rejeitado.

13. **D13 — Demonstração e dados de exemplo** (decisão 11 do G2; decisão 17 para as pessoas indicadas: só conta existente, F14-110).
    - (a) **Recomendado.**
      - **Um conjunto de dados fictícios só:** JSON versionado em `src/Acordu.Application/Exemplos/demonstracao.json`, com datas relativas ao dia. A API lê o arquivo, e o bot do manual (`apps/web/ajuda/demonstracao.ts`) o importa e continua criando pela API.
      - **Dados de exemplo:** só itens ligados a cliente, marcados por `Cliente.Exemplo`. Apagar é por `ClienteId` em todos os tipos com cliente, o que reaproveita o `ClienteId` desnormalizado (ADR 0002). Anexos saem depois do commit. "Sem dado" é a regra da `ClassificacaoDoExpurgo`, sem contar os clientes de exemplo.
      - **Demonstração:** criada pela área no escopo do tenant fixado (ADR 0014, item 5). Contas fictícias em `@exemplo.invalid`. Restaurar usa o `ApagadorDeEmpresa` (D6) sem apagar a empresa, as contas fictícias e os vínculos indicados, e semeia de novo.
      - **Envio na demonstração:** `PodeAvisarAsync` falso; convites e link com e-mail → `422 demonstracao-sem-envio`; link sem e-mail é emitido.
    - (b) Portar os dados para C# e o bot usar a rota da área: muda o workflow do manual (M2).
    - (c) Coluna `Exemplo` em cada raiz, ou tabela de ids: mais estrutura, e o que a equipe criou dentro do exemplo ainda pediria regra.
    - **Vira convenção** (convenções §4, item do manual, no F14r): "os dados fictícios da demonstração e dos exemplos ficam só no `demonstracao.json`; o bot e a API leem dele". Por quê: o manual e a demonstração precisam continuar iguais nos próximos marcos. Não é ADR.
    - **Bloco separável:** F14r e F14s não são dependência de outro PR. Recomendo mantê-los no F14, por último. Se o marco se alongar, viram um marco próprio sem mexer nos demais; a decisão pode ficar para o G3 do F14q.

14. **D14 — Trilha das ações da Nebrys** (F14-70).
    - (a) **Recomendado.** Auditoria do ADR 0008 **sem tenant**, com o operador em `usuario`, ações `nebrys.*` (situação, convite, lista, liberação, demonstração) e o antes e o depois em `dados`. O e-mail afetado entra em `dados`; o documento liberado, nunca. Sobrevive ao expurgo e some em 12 meses. Cada PR da área registra as próprias ações, e o F14p confere todas (F14-70).
    - (b) Tabela própria: duplica a trilha.
    - Conferência: complementa o item 4 do ADR 0008 sem mudar o texto (ADR 0014, item 6).

15. **D15 — Canal da Nebrys** (decisão 14 do G2: `contato@acordu.app.br`). A variável `CONTATO_NEBRYS` do GitHub, com esse endereço, alimenta `Nebrys__Contato` na API e `VITE_CONTATO_NEBRYS` no build do SPA.
    - (a) **Recomendado.** Por quê: uma fonte no deploy, sem chamada extra. Cada lado nasce no primeiro PR que o usa: na API, o F14g (recusa do documento, F14-57; depois os e-mails do F14j); no SPA, o F14e (faixas).
    - (b) A API devolve o canal em `/auth/eu`: não serve às páginas públicas.

16. **D16 — Aceite atrás da publicação dos textos** (decisão do Daniel, 18/11: "Pôr o aceite atrás da TEXTOS_LEGAIS_PUBLICADOS"; F14-114 a F14-121, emenda o D10). Proposto: entra no G2 desta mudança.
    - **Como a API recebe a configuração e como o deploy garante o mesmo valor (F14-121):**
      - (a) **Recomendado.** Uma fonte só, dois leitores. A variável opcional `TEXTOS_LEGAIS_PUBLICADOS` do GitHub, a mesma do F14f, é conferida e normalizada no passo "Variáveis da API" do `deploy.yml`: ausente ou vazia → `false`; `true` ou `false`, aparado e sem diferenciar maiúsculas → o próprio; qualquer outro valor (`1`, `sim`, `ligado`) para o deploy antes de publicar, com a mensagem de variável inválida. O valor normalizado sai do passo (`$GITHUB_OUTPUT`) e alimenta os dois: `TextosLegais__Publicados` no env do Cloud Run e `VITE_TEXTOS_LEGAIS_PUBLICADOS` no build do SPA (que deixa de ler `vars.` direto). Na API, `OpcoesTextosLegais` (Application, ao lado da constante `TextosLegais` do D10) com `Publicados` booleano, **padrão desligado**, lida na `ComposicaoApi` como as `OpcoesEmpresas`: valor presente e inválido impede a subida. O front segue lendo só em `shared/config/ambiente.ts` (só `true` liga). Por quê:
        - é o padrão do `CONTATO_NEBRYS` (D15): uma variável do GitHub, conferida antes de publicar, que vira a configuração de cada lado com o nome dele;
        - com o valor inválido barrado no deploy e o mesmo texto normalizado indo aos dois, os leitores não têm como discordar: o front só liga com `true`, e a API só recebe `true` ou `false`;
        - configuração só por variável de ambiente (ADR 0010), sem tabela nem chamada a mais.
      - (b) A API é a fonte: uma rota pública (ou o `GET /auth/cadastro` do D11, adiantado para o F14h) devolve `textosPublicados`, e a caixa de aceite segue a resposta. A caixa nunca diverge da API, mas as páginas e os links continuam no build (as páginas não chamam a API, D10): o front passa a ter duas fontes para a mesma coisa, e a caixa pode aparecer com links para "não encontrada", o estado que a decisão de 18/11 quis evitar. Rejeitado pelo mesmo motivo da opção (c) do D11.
      - (c) Duas variáveis no GitHub (uma por lado): o esquecimento de uma é o cadastro impossível do F14-121.
      - **Janela da virada (risco a aceitar no G2):** o `deploy.yml` publica a API antes do SPA (revisão, health, borda, build, Pages: alguns minutos). No deploy que liga a variável, nesses minutos e numa aba aberta com o SPA anterior até recarregar, a tela sem caixa encontra a API que exige o aceite, e o cadastro recebe `400` no campo do aceite. A virada acontece uma vez (depois da #194), num deploy autorizado no G4. Recomendo aceitar e registrar no deploy-ci §7, com a orientação de fazer a virada fora do horário de uso. Eliminar a janela pediria a opção (b), com o custo dito acima, ou inverter a ordem do deploy, que vale para todo deploy e não só para este. A virada contrária (desligar) é inofensiva: a tela antiga manda o aceite e a API o ignora (F14-116).
    - **Recusa do "ciente" com os textos não publicados (F14-118):**
      - (a) **Recomendado.** `404 nao-encontrado`, antes de ler o corpo. A rota continua mapeada (o OpenAPI não depende da configuração); quem recusa é o caso de uso. Por quê: é o mesmo tratamento das páginas escondidas ("não encontrada") e do `TempoReal__Sse=false` (`/api/v1/eventos` → 404, ADR 0010 §4); nenhuma tela chama a rota nesse estado (não há aviso), então um tipo novo serviria só a pedido direto.
      - (b) `422` com tipo novo `textos-nao-publicados`: mais explícito, mas um tipo no contrato §13 para uma chamada que nenhuma tela faz.
      - (c) `422 transicao-invalida`: o tipo é das transições de estado de uma entidade; aqui não há entidade.
    - **Quem não tem aceite nenhum (F14-119):**
      - (a) **Recomendado.** Sem marca nem migration: os pendentes do `/auth/eu` são, para cada texto, os que a conta não tem aceite na versão vigente, contando a falta de linha em `aceites_textos` como versão aceita nula, diferente da vigente. Com os textos não publicados, `textosPendentes` é sempre a lista vazia (F14-118). Assim, quem se cadastrou ou entrou por convite com os textos escondidos, e quem já tinha conta antes do F14h, cai no mesmo aviso da versão nova ao ligar a variável, e o "ciente" grava a versão vigente de cada texto pendente. O `/portal/eu` não ganha o campo e as rotas do portal não registram aceite (F14-120). Por quê: é a mesma regra do F14-83 sem caso especial, e a virada não pede passo de dados.
      - (b) Marcar no cadastro "sem aceite" para tratar à parte: estado a mais para o mesmo aviso.
    - **Estado exposto ao front:**
      - (a) **Recomendado.** Nenhum campo novo. O front decide páginas, links, caixa de aceite e aviso de "ciente" pelo mesmo ponto (`TEXTOS_LEGAIS_PUBLICADOS` de `ambiente.ts`); o aviso aparece só com a configuração ligada **e** `textosPendentes` não vazio. A API decide exigir, gravar e listar pendentes pela dela. Por quê: o deploy garante o mesmo valor (acima), e a condição dupla no aviso faz a divergência, se houver, esconder em vez de mostrar link para "não encontrada".
      - (b) `textosPublicados` no `/auth/eu` ou numa rota pública: a opção (b) da primeira pergunta.
    - **Promoção:** a regra "configuração que o SPA e a API precisam ver igual vem de uma variável só do GitHub, conferida e normalizada no `deploy.yml`, que alimenta os dois" vira item das convenções §4 (variáveis de build), no F14h, citando `CONTATO_NEBRYS` e `TEXTOS_LEGAIS_PUBLICADOS`. Não é ADR: aplica o ADR 0010 sem trade-off novo.
    - Conferência: ADR 0010 (configuração só por variável de ambiente; a API sobe sem a variável, com o padrão seguro) e ADR 0013 (a spec do F9 não muda). D11 (c) rejeitou duas fontes para o modo do cadastro; aqui a fonte é uma, com dois leitores, porque as páginas públicas não chamam a API (D10). Sem contradição com ADR aceito.

### 1.1 Decisões do G2 que tocam o design

As decisões 15 a 19 do G2 (spec §4) vieram dos achados do design:

- a decisão 15 está no D5;
- a 16, no D6;
- a 17, no D13;
- a 18 não muda o cadastro: o e-mail em uso é recusado como hoje (F14-105);
- a 19 não pausa o relógio do SLA, e o ADR 0003 fica intacto (F14-111).

A decisão 13 (apagar as empresas anteriores) está no D1, no D6 e no D7. A decisão 2 (30 dias até o expurgo) é só o padrão da configuração.

### 1.2 Decisões de implementação (sem portão)

- **Sessão:**
  - `/auth/eu` traz `tenant.situacao`, `fimDoTeste`, `somenteLeituraDesde`, `expurgoPrevistoEm`, `diasFaixaTeste`, `demonstracao` e `textosPendentes`;
  - `textosPendentes` é a lista vazia com os textos não publicados (D16);
  - `/portal/eu` traz só `envioDisponivel`;
  - o front desabilita as ações por um único `podeGravar` da sessão, que entra nas convenções §4 no F14e.
- **Transições:**
  - TESTE → ATIVA, VENCIDA, CANCELADA ou TESTE (prorrogar);
  - VENCIDA → ATIVA, TESTE (prorrogar) ou CANCELADA;
  - ATIVA → CANCELADA;
  - CANCELADA → ATIVA.

  Prorrogar até o dia D grava o início de D+1 no fuso do tenant. VENCIDA → CANCELADA mantém `SomenteLeituraDesde` e grava `CanceladaEm`.
- **Recorrências sem retroativo:** a geração trata as datas antes de `GravavelDesde` como já trata as de cliente inativo.
- **Migration:** colunas aditivas; o preenchimento põe as empresas existentes em CANCELADA com a data fixa antiga (D1, decisão 13 do G2); o documento fica anulável no `Tenant`, e a Application o exige no cadastro.
- **Cadastro:** o corpo ganha `documento`, `convite`, `versaoTermos` e `versaoPrivacidade`. As validações ficam no caso de uso de registro, numa sequência única: primeiro o que não consulta o banco, depois convite e documento. O registro do documento e o consumo do convite entram na mesma transação da empresa. As recusas são erro de campo (`400`), como o cadastro já faz; nenhum tipo novo. `versaoTermos` e `versaoPrivacidade` são opcionais no OpenAPI, porque a obrigatoriedade depende da configuração (D16): publicados, a falta ou a versão diferente da constante dá `400` no campo; não publicados, o caso de uso não confere nem grava nada (F14-115, F14-116). O aceite de convite da equipe segue a mesma regra (F14-117).
- **Área no SPA:**
  - `/nebrys` sem sessão de operação mostra a entrada da área, com a aparência do `/entrar`;
  - com sessão de empresa, o SPA não carrega a área;
  - a sessão de operação fica só em memória (`shared/sessao/sessaoOperacao.ts`), como a de link;
  - as ações mandam o `If-Match` (D5) e, no `409`, recarregam o detalhe.
- **Configuração nova:**
  - `Empresas__DiasDeTeste` (30), `Empresas__DiasAteExpurgo` (30), `Empresas__AvisosAntesDoFim` (`7,1`), `Empresas__AvisoAntesDoExpurgo` (7), `Empresas__DiasFaixaTeste` (7);
  - `Cadastro__Modo` (obrigatória em produção), `Cadastro__DiasConvite` (7);
  - `Documentos__ChaveHmac`, `Nebrys__Operadores` e `Nebrys__Contato`;
  - `RateLimit__ListaEspera__PorHora` (3);
  - `TextosLegais__Publicados` (padrão desligado, D16; no deploy, vem de `TEXTOS_LEGAIS_PUBLICADOS`).
- **Textos publicados nos testes** (D16): a fixture dos testes funcionais liga `TextosLegais__Publicados`, como o `vite.config.ts` liga `VITE_TEXTOS_LEGAIS_PUBLICADOS` nos do front; os testes do estado desligado (F14-114 a F14-118) desligam no próprio teste, nos dois lados. E2E, bot do manual e desenvolvimento ficam sem as duas variáveis: desligados dos dois lados. O F14-121 pede uma conferência do `deploy.yml` (o mesmo valor normalizado nos dois destinos); se o testador não a automatizar, a coluna Teste diz como foi conferido no deploy.
- **Testes de arquitetura:**
  - `Acordu.Application.Nebrys` não referencia tipos de negócio nem o portal;
  - cada tabela global nova só é usada pelos namespaces liberados (D4);
  - um teste de integração classifica toda entidade com `TenantId` para o só leitura (ADR 0015, item 2).
- **Operação:** o resumo do diário em #deploy ganha as contagens novas, e as falhas dos passos novos seguem o F13c.

## 2. Impacto nos documentos

Cada documento muda no PR que muda o comportamento.

- **ERS:** nenhum além do que a spec e o G2 já trouxeram (a RF-AUT-09 já tem a exceção do cadastro abandonado, decisão 16; a RF-AUT-11 já tem a condição da publicação, 18/11).
- **Modelo de domínio:**
  - §4.11.3 novo, Situação da empresa: campos, tabela de transições, demonstração e marcas de aviso. Entra no F14a e resolve a contradição do §4.11;
  - §4.14 ganha as entidades globais: registro de documento (F14g), aceite (F14h), `ConviteEmpresa` (F14n) e `EntradaListaEspera` (F14o);
  - §4.1 ganha `Cliente.Exemplo` (F14r).
- **Contrato e OpenAPI:**
  - §2: situação em `/auth/eu` (F14a), documento (F14g), aceite e `textos/ciente` (F14h, com a condição da publicação: sem os textos publicados, o aceite do registro e do convite é ignorado, `textosPendentes` vem vazio e o `ciente` responde `404`, D16), convite, `auth/cadastro` e consulta do convite (F14n), e-mail descartável (F14p). Resolve a contradição do §2 nesses PRs, um campo por vez;
  - §12: `envioDisponivel` (F14c);
  - §13: tipos `empresa-somente-leitura` (F14b), `portal-indisponivel` (F14c) e `demonstracao-sem-envio` (F14s);
  - §6, exemplos: F14r;
  - §16, lista de espera: F14o;
  - §17 novo, área da Nebrys: F14k (sessão, lista e detalhe), F14l (ações com `If-Match` e liberação), F14n, F14o e F14s;
  - `openapi/nebrys.json` nasce no F14k, e o `ContratoOpenApiTests` passa a cobrir os três documentos.
- **Read side:** seção "Área da Nebrys" (consultas globais em pontos revisados, só campos da relação) no F14k; "sem dado" dos exemplos no F14r.
- **Segurança:**
  - §3.3: o só leitura como guarda (ADR 0015), no F14b;
  - §4: o "Fora, por ora" é substituído. O documento e o HMAC (D8) entram no F14g; o cadastro por convite, no F14n; a lista descartável, no F14p;
  - §5.5 novo, sessão de operação (ADR 0014): F14k;
  - §6: link da política (F14f), com o texto citando a trilha da Nebrys (12 meses), backups (14 dias) e expurgos; no F14h, uma frase no item da política: o aceite no cadastro e no convite da equipe é registrado por conta, só com os textos publicados (D16);
  - §8: ações `nebrys.*` (F14k, F14l);
  - §9: rotas da área com 404 (F14k); limite e Turnstile da lista de espera (F14o).
- **Deploy e CI:**
  - §5, passos novos do diário: F14a (vencimento), F14i (expurgo) e F14j (avisos). Resolve a contradição do §5;
  - §7, cada variável no PR que a usa: `VITE_CONTATO_NEBRYS` (F14e), `Documentos__ChaveHmac` e `Nebrys__Contato` (F14g), `Nebrys__Operadores` (F14k), `Cadastro__Modo` (F14n), `RateLimit__ListaEspera__PorHora` (F14o);
  - §4 (passo 7) e §7, no F14h: `TEXTOS_LEGAIS_PUBLICADOS` conferida e normalizada no passo "Variáveis da API", indo à API como `TextosLegais__Publicados` e ao build do SPA; a janela da virada como risco aceito, com a orientação de ligar fora do horário de uso (D16);
  - `deploy.yml` exige o segredo do HMAC (F14g) e `CADASTRO_MODO` (F14n), parando antes de publicar sem eles, e para com `TEXTOS_LEGAIS_PUBLICADOS` inválida (F14h);
  - §10: o item da política aponta para a página (F14f).
- **Setup da infra:** em "Segredos do GitHub", `CONTATO_NEBRYS` (F14e), `API_DOCUMENTOS_CHAVE` (F14g), `API_NEBRYS_OPERADORES` (F14k) e `CADASTRO_MODO` (F14n). A linha de `TEXTOS_LEGAIS_PUBLICADOS` passa a dizer, no F14h, que a mesma variável liga o aceite na API e que só aceita `true` ou `false` (D16). O widget do Turnstile (C5) serve à lista de espera sem mudança.
- **ADRs:**
  - 0014 e 0015 aceitos no G2 (PR #186), com o `CLAUDE.md` citando 0001–0015;
  - nenhum ADR aceito muda de texto: os testes do ADR 0012 ficam, e as tabelas novas ganham os seus (D4);
  - a spec do F9 não é editada (ADR 0013, item 9).
- **Convenções:**
  - §1: classificação do só leitura (F14b), tabelas globais novas (F14g);
  - §3: rotas `/api/v1/nebrys`, 404, DTOs próprios e `If-Match` nas ações (F14k, F14l); tipos novos (F14b, F14c);
  - §4: `podeGravar` e `VITE_CONTATO_NEBRYS` (F14e), textos legais versionados (F14f, F14h, D10), no F14h a caixa de aceite e o aviso de "ciente" atrás do mesmo ponto da publicação e a regra da variável única para SPA e API (D16), área `src/nebrys` (F14m), dados fictícios só no JSON (F14r, D13);
  - §5: `EmpresaSomenteLeituraException` (F14b), jobs só em empresas graváveis (F14d).
- **Estrutura do repositório:** `apps/web/src/nebrys` e `openapi/nebrys.json` (F14k, F14m); `features/legal` (F14f).
- **Manual e ajuda:**
  - casca e Configurações › Empresa, portal com a mensagem neutra (F14e);
  - aceite (F14h);
  - cadastro, convite e lista de espera, com a captura do cadastro pelo bot (F14q);
  - Hoje e exemplos, e o bot lendo o JSON (F14r);
  - nota de Novidades em cada PR que muda tela.

  A área da Nebrys fica fora do manual.
- **`docs/specs/README.md`:** a coluna Design do F14 aponta para este arquivo (orquestrador).

## 3. Divisão em PRs

Cada PR contra a `main`, a partir dela, um aberto por vez, com testes e CI verde. Migrations pelo bot; OpenAPI e tipos pelo workflow quando a API muda.

| PR | Entrega | Cenários |
|---|---|---|
| F14a | API: situação no `Tenant` (D1), migration com as existentes em CANCELADA (data fixa antiga), cadastro em TESTE, `/auth/eu`, passo `vencimento-testes` | F14-01 a F14-09 |
| F14b | API: guarda de só leitura e classificação (D2, ADR 0015), `422 empresa-somente-leitura`, o que é da pessoa segue | F14-13 a F14-19, F14-23, F14-100 |
| F14c | API: só leitura no portal e nos convites: `portal-indisponivel` (D3), `/portal/eu` com `envioDisponivel`, aceite de convite antigo recusado, link antigo só lê | F14-20, F14-21, F14-97 a F14-99 |
| F14d | API: jobs só nas graváveis, outbox descartada, `PodeAvisarAsync`, recorrências sem retroativo, SLA na volta | F14-22, F14-24, F14-111 |
| F14e | Front: faixas da casca, situação em Configurações › Empresa, `podeGravar`, recusa tratada, portal neutro; ajuda e Novidades | F14-10 a F14-12, F14-25 a F14-28 |
| F14f | Front: páginas `/termos` e `/privacidade`, links no entrar e no portal, texto da política e dos termos (revisão do Daniel antes do merge) | F14-78, F14-84 a F14-86 |
| F14g | API: documento obrigatório, HMAC (D8), recusa e liberação consumida; campo no formulário | F14-56 a F14-59 |
| F14h | API e front: aceite no cadastro e no convite da equipe (D10), "ciente" de versão nova e de quem não tem aceite, tudo atrás de `TEXTOS_LEGAIS_PUBLICADOS` na API e no SPA (D16) | F14-79 a F14-83, F14-114 a F14-121 |
| F14i | API: `ApagadorDeEmpresa` e `expurgo-empresas` (D6) | F14-29 a F14-38, F14-104, F14-112 |
| F14j | API: `avisos-situacao` com marcas no `Tenant` e e-mails aos Donos (D7) | F14-39 a F14-43, F14-101 a F14-103, F14-113 |
| F14k | API: sessão de operação, filtro 404, lista e detalhe das empresas (ADR 0014), `openapi/nebrys.json` | F14-61 a F14-63 |
| F14l | API: ações de situação com `If-Match` (D5), liberação de documento, trilha `nebrys.*` | F14-65 a F14-69, F14-71, F14-106 a F14-108 |
| F14m | Front: área da Nebrys (entrada, lista, detalhe, ações, liberação) | F14-64 |
| F14n | API: convite de empresa na área, modo do cadastro (D11), registro com convite | F14-45, F14-47 a F14-49, F14-51, F14-72 a F14-74, F14-105 |
| F14o | API: lista de espera pública e na área (D12) | F14-52 a F14-55, F14-75 a F14-77 |
| F14p | API: e-mail descartável no cadastro e na lista (D9), cadastro aberto com todas as regras, trilha conferida em todas as ações da área | F14-50, F14-60, F14-70 |
| F14q | Front: cadastro no modo convite (lista de espera no lugar, e-mail fixo), telas da área para convites e lista; ajuda e captura | F14-44, F14-46, F14-109 |
| F14r | API e front: JSON de dados fictícios (D13), dados de exemplo e oferta na Hoje; bot lendo o JSON | F14-91 a F14-96 |
| F14s | API e front: empresa de demonstração na área (criar, restaurar, indicar) | F14-87 a F14-90, F14-110 |

Tamanho: o antigo F14b virou F14b, F14c e F14d (guarda; portal e convites; jobs). O antigo F14j virou F14k e F14l (sessão, lista e detalhe; ações). O antigo F14m virou F14o e F14p (lista de espera; descartável e fecho das regras).

Ordem e dependências:

- **Só leitura:** F14a → F14b → F14c → F14d → F14e. O F14e precisa dos tipos do F14b e do F14c.
- **Corpo do registro** (`/auth/registro` e o OpenAPI): F14g → F14h → F14n → F14p, nessa ordem, cada um a partir da `main` com o anterior já mesclado, para nenhum PR carregar conflito no mesmo caso de uso e no `interno.json`. O F14h depende também do F14f (as páginas). O aceite fica atrás da mesma configuração das páginas e dos links (D16), então o F14h vai a produção com `TEXTOS_LEGAIS_PUBLICADOS` desligada, sem esperar a #194; ligá-la depois da #194 publica páginas, links e aceite juntos.
- **Independentes do resto:** F14f (abrir cedo, porque a revisão do texto é o caminho mais longo) e o F14g.
- **Expurgo e avisos:** F14i depende de F14a, F14b (guarda e classificação; o apagador passa fora delas) e F14g (F14-37, F14-104). F14j depende de F14a e do F14g (`Nebrys__Contato`).
- **Área:** F14k depende de F14a; F14l de F14k e F14g; F14m de F14k e F14l; F14n de F14l e F14h; F14o de F14n; F14p de F14o; F14q de F14m, F14n, F14o e F14p.
- **Exemplos e demonstração:** F14r depende de F14a e F14b; F14s de F14i, F14l, F14m e F14r.
- **Abertura em produção** no modo convite: F14a a F14q na `main` e o deploy com `CADASTRO_MODO=convite`. F14r e F14s podem vir depois (D13).

## 4. Registro

Uma linha por PR: o que entrou e as decisões não óbvias que surgiram na implementação.

- 18/11 — design: aceite atrás de `TEXTOS_LEGAIS_PUBLICADOS` (decisão do Daniel, 18/11, a partir de `notas/desenvolvedor-F14f-esconder.md`). D16 novo, proposto para o G2 desta mudança: a mesma variável do GitHub, conferida e normalizada no `deploy.yml`, alimenta `TextosLegais__Publicados` (padrão desligado; inválido não sobe) e o build do SPA; "ciente" com os textos não publicados → `404`; quem não tem aceite cai nos pendentes do `/auth/eu` sem migration; nenhum campo novo para o front; janela da virada como risco a aceitar. D10 aponta para o D16; §1.2, §2 e a linha do F14h (F14-114 a F14-121) atualizados, e o "decidir no F14h" saiu das dependências.
- 18/11 — F14j: passo `avisos-situacao` no diário, logo depois de `vencimento-testes` (`AvisosDeSituacao`, laço próprio, inclui as só leitura). `Tenant.AvisoDeSituacaoDevido(agora, fuso, prazos)` decide por **dia civil no fuso da empresa** (a mesma conta da faixa da SPA; achado do revisor, rodada 1): sai um aviso por empresa e diário, o mais próximo; os outros já devidos ficam marcados (F14-103); o atrasado sai uma vez com os dias que faltam (F14-102); referência já passada, nenhum (F14-113). Marcas em `tenants.avisos_situacao text[]` (`tipo:instante` da referência, o que reabre o aviso depois de prorrogar, F14-101; migration `AvisosDeSituacaoDaEmpresa`, aditiva), gravadas por `ExecuteUpdate` com o `xmin` lido, fora da guarda (ADR 0015 item 3). Destinatários buscados antes de marcar: sem nenhum, nada é marcado. E-mail fora da outbox e do `PodeAvisarAsync`, aos Donos (só o criador antes da liberação, F14-43); falha devolve as marcas e o Dono que já recebeu pode receber de novo. Configurações `Empresas__AvisosAntesDoFim` (padrão `7,1`) e `Empresas__AvisoAntesDoExpurgo` (padrão 7). Exceção de fuso no Domain registrada nas convenções §2; RF-EMP-02 da ERS emendada para não contradizer a RF-EMP-04. Premissas em `notas/desenvolvedor-F14j.md`.
- 18/11 — F14i: `ApagadorDeEmpresa` (Infrastructure, extraído do `ExpurgoCadastros`) apaga por `TenantId` todo tipo do modelo (`TiposComTenant`) dentro da transação de quem chama, fora dos filtros e das guardas, anonimiza as sugestões e apaga cada conta ligada sem rastro em outra empresa (vínculo, refresh token, contato ou empresa que ela criou); anexos e logotipo saem do storage depois do commit, em melhor esforço (órfão possível se o processo cair: limpeza por prefixo `{tenantId}/` fica como melhoria, `docs/estado.md`). Passo `expurgo-empresas` no diário depois de `expurgo-cadastros` (`Tenant.ExpurgoDevido(agora, prazo)`, VENCIDA ou CANCELADA com `SomenteLeituraDesde` além de `Empresas__DiasAteExpurgo`), uma transação serializável por empresa com a condição repetida no `DELETE`; `empresasExpurgadas` na resposta e no #deploy. F14-104 por decisão do Daniel (18/11, "Só apagar a linha"): o expurgo do cadastro abandonado apaga só a linha do documento que o próprio cadastro criou (`PrimeiroTesteEm == Tenant.CriadoEm`, mesmo instante no registro, com teste); a liberação usada não volta. Demonstração (F14-31) coberta por ser ATIVA: **o F14s mantém a demonstração ATIVA ou a exclui no `ExpurgoEmpresas`**. Sem migration. Premissas em `notas/desenvolvedor-F14i.md`.

- 16/11 — F14g: documento obrigatório no `POST /auth/registro`, validado pelo `Documento.Normalizar` do F10 (agora público); em claro só em `Tenant.Documento` (anulável). `documentos_teste` guarda só o HMAC-SHA256 do documento normalizado (`ProtecaoDocumento`, chave `Documentos__ChaveHmac` como texto UTF-8, ≥ 32 bytes, a API não sobe sem ela), sem referência a empresa ou pessoa (testes de arquitetura, D4). Reserva e consumo da liberação no mesmo `SaveChanges` da empresa; a disputa (chave primária e `xmin`) dá a mesma recusa `400` com `Nebrys__Contato` (vinda de `CONTATO_NEBRYS`). Ordem das validações: o que não consulta o banco, depois e-mail em uso e senha (Identity), depois o documento, como no contrato. Liberação sem método no domínio: nasce no F14l; os testes a gravam no banco. Migration `DocumentoDoTeste` aditiva. Premissas em `notas/desenvolvedor-F14g.md`.
- 16/11 — F14f: páginas públicas `/termos` e `/privacidade` (`features/legal`), sem chamar a API; textos em Markdown com versão e vigência no cabeçalho e em `VERSOES_TEXTOS_LEGAIS`, que guarda também uma impressão do corpo: mudar o texto sem a constante falha no teste (D10, convenções §4). Links no pé das telas de acesso (política e termos) e nas do contato (só a política), em outra aba, sem aceite. Textos tirados só dos documentos; o que falta é `[a preencher pelo Daniel]`, e as cláusulas redigidas sem apoio nos documentos ficam `[a confirmar pelo Daniel, com o advogado]` (achado do revisor, rodada 1). Vigência provisória. Setup da infra: caminho novo do Email Routing e remoção do MX nulo e do `v=spf1 -all`. Premissas em `notas/desenvolvedor-F14f.md`.
- 16/11 — F14e: `podeGravar` único em `shared/sessao/gravacao.ts` (equipe pela situação; portal pelo `envioDisponivel`); `Botao` ganhou `grava`, com a explicação da faixa por `aria-describedby`; a seção Criar do Ctrl+K some; o que é da pessoa segue livre. Faixas da casca (teste dentro da janela; só leitura com desde, expurgo e canal) e "Situação da conta" em Configurações › Empresa, só leitura. A recusa `422` com a tela aberta relê a sessão e mostra a mensagem. Canal por `VITE_CONTATO_NEBRYS` (`CONTATO_NEBRYS` no `deploy.yml`, obrigatória). Por decisão do Daniel (16/11), a janela da faixa vem da API (`Empresas__DiasFaixaTeste`, padrão 7, em `tenant.diasFaixaTeste`), e os controles que não são `Botao` (menus "⋯", campos que salvam ao mudar, links de texto, arrastar no quadro) seguem habilitados, dependendo da recusa da API: issue #191. Email Routing de `contato@acordu.app.br` no setup da infra. Premissas em `notas/desenvolvedor-F14e.md`.
- 13/11 — F14d: a outbox descarta a mensagem de empresa só leitura (`processada_em` com `erro` "descartada: empresa só leitura"), sem falha nem retorno à fila; o evento inteiro sai, inclusive feed e sino ainda não gerados, e não volta se a empresa voltar a gravar. E-mails da conta saem fora da outbox e seguem. `PodeAvisarAsync` falso em só leitura (segunda barreira). `Tenant.GravavelDesde` (migration `GravavelDesdeDaEmpresa`, sem preenchimento): as recorrências pulam as datas antes do dia da volta, no fuso da empresa. **O F14l tem de gravar `GravavelDesde` em Ativar, Prorrogar e Reativar, com teste**, senão as recorrências voltam retroativas. SLA sem código: o prazo não pausa (decisão 19). Premissas em `notas/desenvolvedor-F14d.md`.
- 13/11 — F14c: `422 portal-indisponivel` com a mensagem neutra "No momento não é possível enviar por aqui; fale com {prestador}", escolhido pela rota (`/api/v1/portal` e `/api/v1/auth/portal`, por segmento e sem diferenciar maiúsculas); as demais rotas seguem com `empresa-somente-leitura`. O nome do prestador (`NomePortal ?? Nome`) vem na mesma consulta da situação, sem consulta a mais. `/portal/eu` ganha só `envioDisponivel`. Convites e link antigos não precisaram de código: a guarda do F14b já os barra, e a transação do aceite volta inteira (conta nova incluída). O lockout da conferência de senha de conta existente fica antes da transação e persiste. Premissas em `notas/desenvolvedor-F14c.md`.
- 13/11 — F14b: guarda de só leitura no `SaveChanges` do `AcorduDbContext`, depois da de isolamento, com a situação lida do banco uma vez por tenant no escopo; `ClassificacaoSomenteLeitura` com teste obrigatório (tipo sem classe conta como da empresa; filha segue a raiz); `EmpresaSomenteLeituraException` → `422 empresa-somente-leitura`. 403, 404 e 400 vêm antes; o `409` de versão, que sai no SQL, fica atrás. Critério único em `SituacaoEmpresaExtensions.SituacoesSomenteLeitura`. Adiantado do F14d: os jobs por tenant percorrem só as empresas graváveis (ADR 0015 item 4), senão contariam falha em toda empresa só leitura; o descarte da outbox e o F14-22 seguem no F14d. A outbox é "rastro" só para a guarda não bloquear o `ProcessadorOutbox`. O escopo que deixa a situação mudar em empresa só leitura fica para o F14l, com as ações. Premissas em `notas/desenvolvedor-F14b.md`.
- 13/11 — F14a: `SituacaoEmpresa` e `Tenant.Vencer(agora)` (só a transição de vencimento; as demais no F14l), `OpcoesEmpresas` (`Empresas__DiasDeTeste` e `Empresas__DiasAteExpurgo`, padrão 30, valor inválido não sobe), passo `vencimento-testes` primeiro no diário (`empresasVencidas`), situação no `/auth/eu`, migration `SituacaoDaEmpresa` com as existentes em CANCELADA em 2026-01-01T00:00:00Z. `SomenteLeituraDesde` do vencimento é o instante do diário, não o fim do teste. `GravavelDesde`, marcas de aviso e demonstração ficam para os PRs que as gravam. Nos testes, ATIVA e CANCELADA são gravadas direto no banco (as ações chegam no F14l). Premissas em `notas/desenvolvedor-F14a.md`.
- 13/11 — G2: PR #186 mesclado; decisões registradas; ADRs 0014 e 0015 aceitos. Design alinhado:
  - empresas anteriores em CANCELADA pela migration, com data fixa antiga (D1);
  - F14-112 no F14i e F14-113 no F14j;
  - expurgo em 30 dias;
  - canal `contato@acordu.app.br`;
  - referências às decisões do G2 no lugar das perguntas.
- 13/11 — devolução do `/plano` (1ª da etapa), corrigida:
  - ADR 0014: sem `EmpresaId` nas tabelas novas, e `documentos_teste` sem referência a empresa ou pessoa;
  - a divisão passou a 19 PRs, com F14-97 a F14-111;
  - §1.1 aponta para as perguntas 15 a 19 da spec;
  - textos com cara de cenário reescritos como decisão;
  - `Nebrys__Contato` no F14g;
  - `Cadastro__Modo` obrigatório em produção;
  - D5 novo (`If-Match` nas ações da área);
  - D6 com o `ExecuteDelete` fora da guarda e a sessão da empresa apagada;
  - D8 registrado na segurança §4;
  - D10 e D13 viram convenção;
  - D4 com os testes de arquitetura das tabelas globais.

  Títulos das tabelas da spec refeitos para a nova divisão (correção de forma).
- 13/11 — rascunho. Design sobre a spec em Rascunho, com as recomendações da §4. ADRs 0014 e 0015 propostos. Correção de forma na spec: títulos das tabelas com os PRs e a linha ADRs do cabeçalho.
