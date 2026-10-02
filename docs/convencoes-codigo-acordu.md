# Acordu — Convenções de Código

| | |
|---|---|
| **Versão** | 0.1 |
| **Última atualização** | 02/10/2026 |
| **Natureza** | Convenções técnicas do projeto. Toda convenção de código nova entra aqui (decisões de arquitetura entram num ADR). |
| **Base** | Convenções do Saldu, com as adaptações do Acordu marcadas como **(Acordu)**. |

---

## 1. Banco de dados e persistência (PostgreSQL / EF Core)

- **snake_case no banco, PascalCase no C#**, via `EFCore.NamingConventions` + `UseSnakeCaseNamingConvention()`. Não anotar `[Table]`/`[Column]` só para renomear.
- **Enum como texto** (`.HasConversion<string>()`). Renomear valor de enum exige migration.
- **Construtor privado sem parâmetros** em entidades e VOs, usado só pelo EF; factories públicas validam invariantes.
- **`TenantId` em toda entidade de negócio** (indexado, *global query filter* — ADR 0003 do Saldu, herdado).
- **(Acordu) `ClienteId` desnormalizado** em toda raiz ligada a cliente (`Solicitacao`, `Interacao`, `Tarefa`, `Apontamento`, `Projeto`, `Contrato`, `AjusteSaldo`, `AcessoPortal`), definido na criação e imutável. Alimenta o segundo filtro global (ADR 0002).
- **(Acordu) `Duracao`** mapeada por value converter para uma coluna `integer` com sufixo `_minutos` (`duracao_minutos`, `franquia_minutos`, `estimativa_minutos`). Nunca `interval`, nunca `numeric` de horas (ADR 0004).
- **(Acordu) Tempo:**
  - instante → `DateTimeOffset` em UTC → coluna `timestamptz`;
  - dia civil → `DateOnly` → `date`;
  - hora do expediente → `TimeOnly` → `time`.
  - Nunca `DateTime` com `Kind` ambíguo em entidade.
- **(Acordu) Concorrência otimista:** propriedade `uint Versao` mapeada como row version (coluna de sistema `xmin` do Postgres, via `IsRowVersion()` no provider Npgsql). Exposta na API como `versao`.
- **(Acordu) `IgnoreQueryFilters()` só na Infrastructure**, e só em pontos listados (agendador que varre todos os tenants, jobs de manutenção). Teste de arquitetura falha se aparecer em outra camada.
- **(Acordu) SQL cru** (consumo, dashboard, semelhantes) só via o helper de consultas da Infrastructure, que exige `TenantId` — e `ClienteId` nas consultas do portal — como parâmetros obrigatórios.
- Extensões Postgres habilitadas por migration: `pg_trgm`, `unaccent`.
- **(Acordu) Segredos de uso único** (token de link do portal): gerados no domínio com `RandomNumberGenerator`; persiste-se só o SHA-256 em hex minúsculo, com índice único; comparação em tempo constante (`CryptographicOperations.FixedTimeEquals`).
- **(Acordu) Documento (CPF/CNPJ) e e-mail** normalizados no domínio (só dígitos; e-mail em minúsculas) antes de comparar ou persistir.

---

## 2. Domínio

- **(Acordu) `Duracao`** é o VO central (papel que o `Dinheiro` tem no Saldu): `int Minutos` ≥ 0, imutável, operadores retornam nova instância, fábricas `FromMinutos`, `FromHoras(h, m)`. Nenhum `int minutos` solto em assinatura pública do domínio. Ajuste de saldo usa `DuracaoAssinada`.
- **(Acordu) Relógio injetado:** o `Acordu.Domain` nunca chama `DateTime.Now`, `DateTimeOffset.UtcNow` ou `DateOnly.FromDateTime(DateTime.Today)`. Todo método que depende do instante recebe `agora`/`hoje` por parâmetro. Na Application, o instante vem de `TimeProvider` (injetado; `FakeTimeProvider` nos testes).
- **(Acordu) Fuso:** conversão para hora local do tenant só dentro da `CalculadoraSla` e na apresentação, usando o fuso do `CalendarioUtil`.
- **Exceções:** `ArgumentException` para input malformado; **exceções de domínio próprias** para regra de negócio (`TransicaoInvalidaException`, `PeriodoFechadoException`, `ContratoIncoerenteException`, `SlaIncompletoException`...), todas derivadas de `DominioException`.
- **Value objects como `record`:** `readonly record struct` para os pequenos e numéricos (`Duracao`), `sealed record` para os compostos (`Periodo`, `JanelaDiaria`). A igualdade por valor vem do record; não há classe base `ValueObject`. A validação fica no construtor.
- **Eventos de domínio:** a raiz guarda a lista de eventos pendentes (`EntidadeBase.Registrar`); a Infrastructure despacha e chama `LimparEventos`. Sem biblioteca de mediator no domínio.
- Construtores privados + *factory methods*; sem setters públicos; máquinas de estado com a tabela de transições do `modelo-de-dominio-acordu.md` como fonte da verdade (e testes cobrindo cada célula).
- **(Acordu) Enums em PascalCase no C#** (`StatusSolicitacao.EmAndamento`), com valores numéricos explícitos a partir de 1. O formato `SCREAMING_SNAKE` do contrato (`EM_ANDAMENTO`) é responsabilidade da borda: na Api, `JsonStringEnumConverter` com `JsonNamingPolicy.SnakeCaseUpper`; no banco, conversor equivalente definido no marco da Infrastructure.
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
- **(Acordu) Portal separado:** endpoints em `/api/v1/portal`, casos de uso em `Acordu.Application.Portal`, DTOs próprios que **não têm** campos internos. Proibido reaproveitar DTO interno no portal, mesmo "só com alguns campos nulos".
- **(Acordu) Transições** como ações (`POST /recurso/{id}/acao`); `If-Match` com `versao` → 409 se divergir.
- Endpoints em minimal API, agrupados por feature (`MapGroup`), com políticas de autorização nomeadas por permissão.

---

## 4. Frontend

- **Tipos TS gerados do OpenAPI** (`openapi-typescript`), nunca escritos à mão.
- **Cliente HTTP centralizado** em `shared/api` (base URL, Bearer, parsing de Problem Details, `If-Match`).
- Organização **por feature**.
- **(Acordu) Duas áreas:** `features/*` (app interno) e `portal/*` (layout, rotas e cliente de API próprios). O portal não importa nada de `features/`; o que for comum vai para `shared/`.
- **(Acordu) Tempo:** formatador único em `shared/format` (`formatarDuracao(90) → "1h30"`); instantes exibidos no fuso do tenant (vem em `/auth/eu`), não no fuso do navegador.
- Component library (shadcn/ui ou Mantine — decidir no marco do front) para formulários e listas.

---

## 5. Arquitetura / camadas

- **Regra de dependência:** Domain ← Application ← Infrastructure ← Api. O `Acordu.Domain` não referencia EF Core, Npgsql, ASP.NET nem HTTP.
- Application define interfaces (`ITenantContext`, repositórios, `IArmazenamentoArquivos`, `IEnviadorEmail`, `IGeradorProtocolo`); Infrastructure implementa.
- **Autorização na Application**, por caso de uso. **(Acordu)** `ITenantContext` expõe também `Papel`, `ClienteId?`, `EscopoPortal?` e, na sessão de link, `SolicitacaoIdPermitida?`.
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
