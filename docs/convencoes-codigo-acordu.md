# Acordu — Convenções de Código

| | |
|---|---|
| **Versão** | 0.1 |
| **Última atualização** | 02/10/2026 |
| **Natureza** | Convenções técnicas do projeto. Toda convenção de código nova entra aqui (decisões de arquitetura entram num ADR). |
| **Base** | Convenções do projeto; os itens marcados **(Acordu)** são específicos deste domínio. |

---

## 1. Banco de dados e persistência (PostgreSQL / EF Core)

- **snake_case no banco, PascalCase no C#**, via `EFCore.NamingConventions` + `UseSnakeCaseNamingConvention()`. Não anotar `[Table]`/`[Column]` só para renomear.
- **Enum como texto** — **(Acordu)** em SCREAMING_SNAKE (`EM_ANDAMENTO`) via `EnumTextoConverter`, aplicado por convenção a todo enum de `Acordu.Domain.Enums`. Renomear valor de enum exige migration de dados.
- **Construtor privado sem parâmetros** em entidades, usado só pelo EF; factories públicas validam invariantes. **(Acordu)** VOs owned (`Periodo`, `Solucao`, `RegraRecorrencia`, `JanelaDiaria`) são materializados pelo construtor público; como propriedades só com getter não são mapeadas por convenção, o mapeamento declara cada uma.
- **`TenantId` em toda entidade de negócio** (*global query filter* — ADR 0001). **(Acordu)** Sem índice só em `TenantId`: cada tabela tem índices compostos que começam por ele (e nenhum índice que seja prefixo de outro).
- **(Acordu) Filtros globais nomeados** (EF Core 10): `tenant` (sempre) e `cliente` (contexto de contato). Sem tenant no contexto, nada é devolvido. No contexto de cliente, `clientes` filtra pelo próprio Id, `cronometros` some, `categorias` e `calendarios` seguem visíveis.
- **(Acordu) Guarda na gravação:** o `AcorduDbContext` recusa incluir, alterar ou remover raiz de outro tenant — ou, no contexto de contato, de outro cliente ou de tipo sem cliente — com `IsolamentoVioladoException`, antes de qualquer SQL.
- **(Acordu) Mapeamento de VOs e filhos:** VOs de uma raiz viram colunas na tabela dela (owned), com nomes fixos quando o read side depende deles (`prazo_resolucao`, `limite_risco`); `PoliticaSla` e `ModeloTarefa` viram `jsonb`; feriados viram `date[]`; entidades filhas (`Contato`, `ItemChecklist`, `Anexo`, expediente) têm tabela própria com cascade e são sempre carregadas com a raiz.
- **(Acordu) `ClienteId` desnormalizado** em toda raiz ligada a cliente (`Solicitacao`, `Interacao`, `Tarefa`, `Apontamento`, `Projeto`, `Contrato`, `AjusteSaldo`, `AcessoPortal`), definido na criação e imutável. Alimenta o segundo filtro global (ADR 0002).
- **(Acordu) `Duracao`** mapeada por value converter para uma coluna `integer` com sufixo `_minutos` (`duracao_minutos`, `franquia_minutos`, `estimativa_minutos`). Nunca `interval`, nunca `numeric` de horas (ADR 0004).
- **(Acordu) Tempo:**
  - instante → `DateTimeOffset` em UTC → coluna `timestamptz`;
  - dia civil → `DateOnly` → `date`;
  - hora do expediente → `TimeOnly` → `time`.
  - Nunca `DateTime` com `Kind` ambíguo em entidade.
- **(Acordu) Concorrência otimista:** propriedade escondida (shadow) `Versao` (`uint`) mapeada como row version na coluna de sistema `xmin` do Postgres; o domínio não a conhece. A Application lê o valor via EF para expor `versao` e conferir o `If-Match`.
- **(Acordu) `IgnoreQueryFilters()` só na Infrastructure**, e só em pontos listados (agendador que varre todos os tenants, jobs de manutenção). Teste de arquitetura falha se aparecer em outra camada.
- **(Acordu) SQL cru** (consumo, dashboard, semelhantes) só via o helper `ConsultaSql` da Infrastructure, que entrega o escopo do contexto e **recusa executar** se o `TenantId` — e, no contexto de contato, o `ClienteId` — não estiver entre os parâmetros. Regras do texto:
  - o texto é sempre literal; trechos interpolados viram parâmetros, nunca SQL (não montar SQL por concatenação);
  - para servir equipe e contato com o mesmo texto: `AND (CAST({e.ClienteId} AS uuid) IS NULL OR x.cliente_id = {e.ClienteId})`;
  - colunas do resultado em snake_case mapeiam as propriedades do tipo lido (a convenção vale também para `SqlQuery`); escalar usa o alias `"Value"`;
  - a entidade principal é lida antes pelo EF (filtros globais): invisível no contexto → consulta devolve nulo.
- Extensões Postgres habilitadas por migration: `pg_trgm`, `unaccent`.
- **(Acordu) Migrations** ficam em `src/Acordu.Infrastructure/Persistence/Migrations` e são geradas pelo `dotnet-ef` fixado em `.config/dotnet-tools.json`: pelo workflow **Migrations** (gera e faz commit na branch de feature; nunca na `main`), disparado por um arquivo `.github/migration-request` com o nome da migration ou manualmente no Actions, ou localmente com o comando do cabeçalho do workflow. Migration gerada não é editada à mão, exceto para acrescentar SQL que o EF não expressa (índices sobre colunas de tipos owned, configuração de busca), sempre **no fim do `Up` e no início do `Down`** (desfazer na ordem inversa), com comentário explicando o porquê e, se possível, um teste que confira o objeto criado.
- **(Acordu) Segredos de uso único** (token de link do portal): gerados no domínio com `RandomNumberGenerator`; persiste-se só o SHA-256 em hex minúsculo, com índice único; comparação em tempo constante (`CryptographicOperations.FixedTimeEquals`).
- **(Acordu) Documento (CPF/CNPJ) e e-mail** normalizados no domínio (só dígitos; e-mail em minúsculas) antes de comparar ou persistir.

---

## 2. Domínio

- **(Acordu) `Duracao`** é o VO central (o tempo é o "dinheiro" do Acordu): `int Minutos` ≥ 0, imutável, operadores retornam nova instância, fábricas `FromMinutos`, `FromHoras(h, m)`. Nenhum `int minutos` solto em assinatura pública do domínio. Ajuste de saldo usa `DuracaoAssinada`.
- **(Acordu) Relógio injetado:** o `Acordu.Domain` nunca chama `DateTime.Now`, `DateTimeOffset.UtcNow` ou `DateOnly.FromDateTime(DateTime.Today)`. Todo método que depende do instante recebe `agora`/`hoje` por parâmetro. Na Application, o instante vem de `TimeProvider` (injetado; `FakeTimeProvider` nos testes).
- **(Acordu) Fuso:** conversão para hora local do tenant só dentro da `CalculadoraSla` e na apresentação, usando o fuso do `CalendarioUtil`.
- **Exceções:** `ArgumentException` para input malformado; **exceções de domínio próprias** para regra de negócio (`TransicaoInvalidaException`, `PeriodoFechadoException`, `ContratoIncoerenteException`, `SlaIncompletoException`...), todas derivadas de `DominioException`.
- **Value objects como `record`:** `readonly record struct` para os pequenos e numéricos (`Duracao`), `sealed record` para os compostos (`Periodo`, `JanelaDiaria`). A igualdade por valor vem do record; não há classe base `ValueObject`. A validação fica no construtor.
- **Eventos de domínio:** a raiz guarda a lista de eventos pendentes (`EntidadeBase.Registrar`); a `UnitOfWork` da Infrastructure despacha e chama `LimparEventos`. Sem biblioteca de mediator. **(Acordu)** Reação de estado = `IManipuladorEvento<T>` (na transação); efeito externo = `IManipuladorPosCommit<T>` (via outbox, idempotente). Ver modelo de domínio §7.
- Construtores privados + *factory methods*; sem setters públicos; máquinas de estado com a tabela de transições do `modelo-de-dominio-acordu.md` como fonte da verdade (e testes cobrindo cada célula).
- **(Acordu) Enums em PascalCase no C#** (`StatusSolicitacao.EmAndamento`), com valores numéricos explícitos a partir de 1. O formato `SCREAMING_SNAKE` do contrato (`EM_ANDAMENTO`) é responsabilidade da borda: na Api, `JsonStringEnumConverter` com `JsonNamingPolicy.SnakeCaseUpper`; no banco, `EnumTextoConverter` (§1).
- **(Acordu) Operação não permitida no status atual** (inclusive editar agregado em estado final) lança `TransicaoInvalidaException`, mesmo quando não é uma transição propriamente dita.
- **(Acordu) Textos:** tamanhos máximos em `Acordu.Domain.Common.LimitesTexto` (fonte única para validação e para `HasMaxLength`); entrada é aparada, obrigatório vazio → `ArgumentException`, opcional vazio → `null`.
- **(Acordu) Ids:** a raiz recebe o Id na factory (gerado pela Application); entidade filha dentro do agregado (ex.: `ItemChecklist`) tem o Id gerado pela raiz com `Guid.CreateVersion7()`. Referências entre agregados validadas por `Common.Ids` (`Guid.Empty` → `ArgumentException`).
- **(Acordu) Agregado que precisa validar outro** recebe a raiz como parâmetro somente leitura e copia só o Id (ex.: `AjusteSaldo.Registrar(..., contrato, ...)`); nunca guarda referência de objeto.

---

## 3. API e contrato

- **Tempo trafega como inteiro em minutos**, campo com sufixo `Minutos` (`duracaoMinutos`). Nunca horas decimais.
- **Instantes em ISO-8601 UTC** (`Z`); datas civis `yyyy-MM-dd`; períodos `yyyy-MM`.
- **Recurso de outro tenant ou de outro cliente → 404.**
- **Erros:** Problem Details (RFC 9457), `type` com URI estável por categoria (`.../erros/transicao-invalida`).
- **`TenantId` sempre do token**; **(Acordu)** `ClienteId` do portal também do token (claim do vínculo), nunca da URL.
- **(Acordu) Portal separado:** endpoints em `/api/v1/portal`, casos de uso em `Acordu.Application.Portal`, DTOs próprios que **não têm** campos internos. Proibido reaproveitar DTO interno no portal, mesmo "só com alguns campos nulos". O que a **equipe** faz sobre o portal (convidar contatos, escopo, revogar) fica fora desse namespace (`Acordu.Application.AcessosPortal`).
- **(Acordu) Rotas do portal abertas à sessão de link** levam `.PermitirLink()`; as demais respondem 403 a ela (filtro `SessaoPortal`). Rotas de quem tem conta (`/auth/eu`, `/auth/tenant-ativo`) usam a política `Usuario`.
- **(Acordu) Rotas públicas** (`/auth/registro`, `/login`, `/refresh`, `/logout`, convite do portal) levam `AllowAnonymous()`: o contexto de tenant não é definido a partir de um `Bearer` enviado junto, porque elas fixam o próprio tenant e a SPA pode mandar o token de outra sessão.
- **(Acordu) Transições** como ações (`POST /recurso/{id}/acao`); `If-Match` com `versao` → 409 se divergir.
- Endpoints em minimal API, agrupados por feature (`MapGroup`), com políticas de autorização nomeadas por permissão.

---

## 4. Frontend

- **Tipos TS gerados do OpenAPI** (`openapi-typescript`), nunca escritos à mão. **(Acordu, F1)** O contrato fica versionado em `openapi/interno.json` e `openapi/portal.json`, conferido por um teste funcional (`ContratoOpenApiTests`, sem as rotas de teste do fixture). Quando a API muda, regera-se com `ACORDU_ATUALIZAR_OPENAPI=1` ou pelo workflow **OpenAPI** (commitar `.github/openapi-request`), que também roda `pnpm api:tipos`; a CI falha se o contrato ou os tipos estiverem desatualizados.
- **Cliente HTTP centralizado** em `shared/api` (base URL, Bearer, parsing de Problem Details, `If-Match`).
- Organização **por feature**.
- **(Acordu) Duas áreas:** `features/*` (app interno) e `portal/*` (layout, rotas e cliente de API próprios). O portal não importa nada de `features/`; o que for comum vai para `shared/`.
- **(Acordu) Tempo:** formatador único em `shared/format` (`formatarDuracao(90) → "1h30"`); instantes exibidos no fuso do tenant (vem em `/auth/eu`), não no fuso do navegador.
- **(Acordu, F1) Stack do front:**
  - React 19, Vite e TypeScript estrito, com pnpm (`apps/web`, Node 22);
  - **React Aria Components** (comportamento e acessibilidade, sem estilo), com o visual nosso via tokens CSS e Tailwind 4 só para aplicar os tokens. Substitui a ideia de shadcn/Mantine;
  - TanStack Router com rotas por arquivo (`src/routes`, `routeTree.gen.ts` gerado e fora do Git: o plugin do Vite o cria no `dev`/`test`, e `typecheck`/`build` rodam `tsr generate` antes) e TanStack Query;
  - `openapi-fetch` no cliente central (`shared/api/cliente.ts`);
  - Vitest com Testing Library e Playwright (fumaça);
  - ESLint (typescript-eslint) e Prettier (sem ponto e vírgula, aspas simples, 120 colunas).
- **(Acordu, F1) Sessão** (`shared/sessao`, ADR 0007):
  - o access token fica só na memória do módulo `sessao.ts`; o cliente HTTP o pede a cada requisição;
  - renovar é serializado: na aba, uma promessa compartilhada; entre abas, o Web Lock `acordu-refresh`, e quem renova anuncia o token novo no `BroadcastChannel` `acordu-sessao`. Falha de rede não é repetida; só um 401 do refresh encerra a sessão;
  - o cliente renova antes de enviar quando faltam menos de 60 s e repete uma vez depois de um 401. As rotas de `/auth` que abrem ou dispensam sessão ficam fora disso;
  - login, cadastro, aceite de convite e troca de empresa chamam `abrirSessao`; logout e refresh recusado, `encerrarSessao`. As duas valem para todas as abas e limpam o cache do TanStack Query;
  - guarda das rotas no layout `_app` (sem sessão → `/entrar?voltar=`, contato de cliente → `/portal`); o perfil (`GET /auth/eu`) vai para o contexto das rotas. `?voltar=` só aceita caminho interno;
  - tokens de link (convite, senha, confirmação) saem da URL na primeira carga (segurança §9): a rota `…/$token` guarda o token na `sessionStorage` da aba (`shared/sessao/tokenDaUrl.ts`) e troca a URL, com `replace`, pela versão sem ele. O token é apagado quando usado. Ação que gasta o token ao abrir a página (confirmação de e-mail) é consulta do TanStack Query, e não mutação, para o StrictMode não chamar duas vezes;
  - formulários com o `Form` do React Aria: o `name` do campo é o do corpo da API, e `validationErrors` recebe os erros do Problem Details.
- **(Acordu, F1) Casca e telas:**
  - o layout `_app` envolve as rotas da equipe com `features/casca/Casca` (capa lateral fixa a partir de `md`, gaveta abaixo disso, paleta Ctrl+K);
  - a navegação fica em `features/casca/navegacao.ts`, e cada item lista as permissões de leitura (basta uma). Item sem permissão não aparece, para não levar a um 403;
  - toda tela começa com `shared/ui/Pagina` (contexto, título, ações; também dá nome à aba), e lista ou tela vazia usa `shared/ui/EstadoVazio`;
  - datas exibidas usam o fuso do tenant (`shared/format/quando`, `saudacao`); comparação de texto sem acento por `shared/format/texto`;
  - ícones de traço em `shared/ui/Icone`, sem biblioteca; decorativos (`aria-hidden`), com o nome no texto ou no `aria-label` do botão.
- **(Acordu, F1) Dados na tela, tempo real e retorno:**
  - **chaves do TanStack Query** começam pelo recurso no plural (`['solicitacoes', filtros]`, `['tarefas', id]`); telas que juntam recursos têm chave própria (`agenda`, `capacidade`, `panorama`, `atividades`, `hoje`). Consulta de um recurso dentro de outra tela usa a chave do recurso (as solicitações do cliente são `['solicitacoes', { clienteId }]`), para o sinal certo invalidá-la;
  - **tempo real** (`shared/tempoReal`, ADR 0009): a casca mantém o SSE lido com `fetch`. Cada sinal invalida a chave do recurso e as visões, juntando os sinais de 300 ms numa rodada só; ao reconectar depois de uma queda, invalida tudo. Um 401 renova o token, um 403 desiste, e as novas tentativas esperam a partir do `retry` da API, dobrando até 60 s;
  - **ação reversível não pergunta "tem certeza?"**: acontece e oferece desfazer num aviso (`avisar(texto, { desfazer })`, 8 s). Erro de ação sem formulário vai para `avisarErro`. Erro de formulário fica no formulário;
  - **erro ao carregar tela** usa o componente de erro padrão do roteador (`ErroTela`: 403, 404 ou falha, com "Tentar de novo"). Endereço inexistente dentro do app cai na rota `_app/$`, sem sair da casca. Sem rede, uma faixa no topo avisa.
- **(Acordu, F1) Tema:**
  - agenda de couro; tokens em `shared/tema/tokens.css`, com o papel por `data-tema` (claro/escuro) e a capa por `data-couro` (Café padrão; Conhaque e Bordô prontos, sem seletor no v1);
  - a escolha fica no navegador e, sem ela, vale a do sistema, aplicada antes da primeira pintura;
  - fontes Fraunces (títulos) e Source Sans 3 (texto), hospedadas com o app (`@fontsource`), nunca do Google.

---

## 5. Arquitetura / camadas

- **Regra de dependência:** Domain ← Application ← Infrastructure ← Api. O `Acordu.Domain` não referencia EF Core, Npgsql, ASP.NET nem HTTP.
- Application define interfaces (`ITenantContext`, repositórios, `IArmazenamentoArquivos`, `IEnviadorEmail`, `IGeradorProtocolo`); Infrastructure implementa.
- **Autorização na Application**, por caso de uso. **(Acordu)** `ITenantContext` expõe também `Papel`, `ClienteId?`, `EscopoPortal?` e, na sessão de link, `AcessoPortalId?` e `SolicitacaoPermitida?`.
- **(Acordu) Contexto de tenant:** `ContextoTenant` (Infrastructure) é a única implementação de `ITenantContext`, por escopo, definida uma vez — pela Api a partir do token ou por um job para o tenant que processa. Escopo sem definição não enxerga nem grava nada.
- **(Acordu) Vínculos e sessões** (buscar o vínculo do usuário, contar Donos, revogar refresh tokens, e-mail já vinculado) ficam em `IVinculos`, usado pela equipe e pelo portal.
- **(Acordu) Repositórios:** `IRepositorio<T>` genérico (`ObterPorIdAsync` procura no change tracker antes do banco); consultas específicas entram com os casos de uso.
- **(Acordu) Casos de uso (CQRS-lite, sem mediator):** por feature, uma classe de comandos (`Clientes/ComandosCliente`) com um método por caso de uso, que devolve o Id; e uma interface de consultas (`IConsultasCliente`) implementada na Infrastructure com projeção do EF ou `ConsultaSql`. A Api escreve com o comando e responde com a consulta. Registro em `Api/Composicao/CasosDeUso.cs` (a Application segue sem pacotes).
- **(Acordu) Erros da Application:** `contexto.Exigir(permissao)` → `AcessoNegadoException` (403 `sem-permissao`); raiz não encontrada pelos filtros → `NaoEncontradoException` (404 `nao-encontrado`); referência inválida no corpo → `ErroDeCampo.Criar("campoJson", msg)` (400 com o campo em `errors`). Campo dentro de objeto aninhado usa o caminho com ponto (`regra.diaReferencia`, `modelo.responsavelId`): a Application converte o `ArgumentException` do VO para esse caminho.
- **(Acordu) Auditoria:** ação sensível (segurança §8) chama `IAuditoria.Registrar(AcoesAuditoria.X, "tipo", alvoId, dados)` no caso de uso, antes do `SalvarAsync` (mesma transação). `dados` com o valor anterior quando a ação altera ou exclui; nunca segredo nem token. Nome de ação novo entra em `AcoesAuditoria` e no contrato §11a (ADR 0008).
- **(Acordu) Avisos internos:** aviso à equipe sai por `AvisoInterno.AvisarAsync` (sino sempre + e-mail se a categoria não estiver desligada), com uma `chave` estável por aviso: o sino é idempotente por (usuário, chave), então a reentrega da outbox não duplica. Categoria nova nasce com o e-mail **ligado**; a que faria barulho demais entra em `PadroesDeAviso.EmailDesligadoPorPadrao` (hoje só SEGUIDAS, decidido em 14/10). A coluna `preferencias_notificacao.email_desligado` guarda as categorias **fora do padrão** (desligadas, nas ligadas por padrão; ligadas, nas desligadas por padrão), e no `PUT` categoria ausente volta ao padrão.
- **(Acordu) Autor do evento:** a outbox grava o usuário da requisição (`autor_id`) junto com o evento, e o `ProcessadorOutbox` o expõe aos manipuladores por `IOrigemDoEvento.AutorId` (nulo em job), inclusive nas novas tentativas. `IOrigemDoEvento.MensagemId` e `OcorridoEm` identificam a ocorrência: quem grava uma linha por evento (feed de atividade) usa o Id da mensagem como chave de idempotência. Serve para não avisar quem causou o evento sem pôr o usuário em todo evento de domínio (decidido no 14c). Evento que já tem o autor como dado de negócio (`AbertaPor`, `AutorId`) continua usando o próprio campo.
- **(Acordu) If-Match:** a Api lê o header e passa a versão ao comando, que chama `IControleVersao.Esperar(raiz, versao)` logo depois de carregar; a Infrastructure usa a versão como valor original do `xmin` e marca a raiz como alterada (o UPDATE acontece mesmo quando só uma filha mudou). Sem header, sem checagem.
- **(Acordu) E-mail** só em `IManipuladorPosCommit<T>` (outbox), nunca dentro da transação do caso de uso. Token que vai em e-mail é gerado no próprio manipulador, com só o hash gravado antes do envio: nada em claro no banco nem na outbox. Sem dado de cliente no log do envio (`Email__RegistrarConteudo` só em desenvolvimento). Envio real pelo Resend via HTTP (`EnviadorEmailResend`), texto e HTML do mesmo conteúdo; o nome do prestador vai no "De" (`MensagemEmail.NomeRemetente`), o endereço é sempre o do Acordu. Exceções, com o e-mail saindo logo após o commit, na requisição: o link por solicitação (o link volta na resposta; a falha é informada nela) e os e-mails da conta (confirmação, redefinição de senha: pedidos do próprio usuário, repetíveis e sem tenant; a falha vai para o log).
- **(Acordu) Datas civis da Application:** "hoje" e mês corrente no fuso do tenant via `RelogioDoTenant` (calendário + `TimeProvider`).
- **(Acordu) Testes de arquitetura** (projeto `Acordu.ArchitectureTests`, NetArchTest) garantem: regra de dependência entre camadas; nada de `DateTime.Now`/`UtcNow` no Domain; nada de `IgnoreQueryFilters` fora da Infrastructure; nenhum tipo de `Acordu.Application.Portal` referenciando DTOs internos.

---

## 6. Estrutura, namespaces, testes e commits

- **Namespace espelha a pasta** (`Acordu.Domain.Entities`, `Acordu.Application.Portal.Solicitacoes`...).
- **Domínio organizado por tipo:** `Entities/`, `Enums/`, `ValueObjects/`, `Services/`, `Events/`, `Exceptions/`.
- Projetos: `Acordu.Domain`, `Acordu.Application`, `Acordu.Infrastructure`, `Acordu.Api` (+ `Acordu.Worker` adiado).
- **Assertions:** AwesomeAssertions (fork Apache 2.0 do FluentAssertions). Não usar FluentAssertions 8+, que exige licença comercial para produto pago.
- **Nomes de teste:** `Metodo_Cenario_ResultadoEsperado` (ex.: `Resolver_SemSolucao_LancaExcecao`). Um arquivo de teste por classe testada.
- **Casos de borda documentados viram teste:** checklists dos documentos (read side §11, ADR 0003) são a lista mínima.
- Commits: `convencoes-commit-acordu.md`. Layout: `estrutura-repositorio-acordu.md`.

---

> **Regra de manutenção:** convenção de código nova é registrada **aqui**, no mesmo turno em que é decidida. Decisão de *arquitetura* com trade-off vira ADR (`docs/adr/`).
