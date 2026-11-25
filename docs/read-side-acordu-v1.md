# Acordu — Read Side Detalhado (v1)

| | |
|---|---|
| **Versão** | 0.1 |
| **Data** | 30/09/2026 |
| **Escopo** | Consumo de contrato, extrato do portal, situação de SLA, fila/dashboard, progresso de projeto e solicitações semelhantes. |
| **Natureza** | Especificação. Fórmulas e passos, **sem implementação**. |

---

## 1. Princípios

- **CQRS-lite** (ADR 0001): leituras não passam por agregados; são queries que retornam DTOs.
- **Tempo em minutos inteiros** (ADR 0004): toda soma é de `integer`; nenhuma conversão para horas antes da apresentação.
- **Determinismo:** mesma base de dados + mesmo instante de referência → mesmo número. O "agora" é parâmetro (`TimeProvider`), nunca `now()` do SQL.
- **Explicabilidade:** todo total mostrado ao cliente é conferível linha a linha no mesmo DTO.
- **Dois filtros sempre** (ADR 0002): `TenantId` em tudo; `ClienteId` (e `ContatoId` quando escopo PROPRIAS) nas queries do portal. SQL cru recebe os dois como parâmetros obrigatórios de um helper único.
- **Sob demanda no v1.** Cache só se a medição pedir, invalidado pelos eventos de apontamento/ajuste/solicitação.

---

## 2. Notação

- `A(c, [d1, d2])` = apontamentos do contrato `c` com `d1 ≤ data ≤ d2`.
- `mesmo(p)` = `[primeiro dia, último dia]` do mês `p`, **no calendário civil** (datas de apontamento são `DateOnly`, sem fuso).
- `Σmin` = soma de `duracao_minutos`.

---

## 3. Consumo de contrato

### 3.1 MENSAL_FIXO (franquia por período)

```
consumido(c, p)      = Σmin { a ∈ A(c, mes(p)) | a.faturavel }
naoFaturavel(c, p)   = Σmin { a ∈ A(c, mes(p)) | ¬a.faturavel }
ajustes(c, p)        = Σ ajuste.minutos { ajuste de c com data ∈ mes(p) }
saldo(c, p)          = franquia(c) + ajustes(c, p) − consumido(c, p)      // pode ser negativo = excedente
percentual(c, p)     = arredonda_para_baixo(100 × consumido / (franquia + ajustes))
```
- Sem franquia → `saldo` e `percentual` nulos; só `consumido` é informado.
- **Franquia não acumula** entre meses (sobra de setembro não vai para outubro). Se o cliente quiser acumular, o contrato é BANCO_HORAS.
- Vigência parcial no mês (contrato começou dia 15): franquia **cheia** no v1 (sem pró-rata). Registrado como decisão simples; revisar com uso real.

### 3.2 BANCO_HORAS (saldo corrente)

```
saldo(c, ate) = franquia(c)
              + Σ ajuste.minutos { ajuste de c com data ≤ ate }
              − Σmin { a ∈ A(c, [inicio(c), ate]) | a.faturavel }
```
- O detalhamento por mês (`consumido(c, p)`) usa a mesma fórmula do §3.1, mas o **saldo exibido é sempre o acumulado** até o fim do período consultado (ou até hoje, se o período é o corrente).
- `percentual = 100 × consumidoAcumulado / (franquia + ajustesAcumulados)`.

### 3.3 AVULSO
Só `consumido(c, p)` e `naoFaturavel(c, p)`; sem saldo nem percentual.

### 3.4 Limiares (RF-CTR-07)
Após cada `ApontamentoRegistrado/Alterado/Excluido` e `AjusteSaldo`, o handler recalcula `percentual` do contrato afetado no período da data do apontamento e, se cruzou 80% ou 100% **para cima**, emite a notificação uma única vez por `(contrato, período, limiar)` — marcador persistido para idempotência. Cruzar para baixo (exclusão) não notifica e rearma o limiar. **(Implementado no 7c3:** `AlertasConsumo`, marcador em `marcadores_aviso` com chave `consumo:{yyyy-MM}:{limiar}` no MENSAL_FIXO e `consumo:acumulado:{limiar}` no BANCO_HORAS, cujo saldo é acumulado; limiares fixos 80/100.)

### 3.5 Índice
`apontamentos (tenant_id, contrato_id, data) INCLUDE (duracao_minutos, faturavel)` — a soma do mês vira um index-only scan.

---

## 4. Extrato do portal (RF-POR-07)

Para o contrato `c` do cliente do contato e o período `p`:

```
itens = { a ∈ A(c, mes(p)) | a.faturavel }
          ordenados por (data, solicitação, criado_em)
linha(a) = a.visivelCliente
             ? { data, solicitacaoNumero, solicitacaoTitulo, descricaoExterna ?? descricao, duracaoMinutos }
             : { data, solicitacaoNumero?, "Atendimento", duracaoMinutos }
consumido = Σ linha.duracaoMinutos        // bate com §3 por construção
```
- **Regra de conferência:** o total do extrato é sempre a soma das linhas exibidas. Por isso apontamento faturável não visível aparece como linha genérica, em vez de sumir.
- **Não faturáveis nunca aparecem** no extrato (nem no total nem nas linhas).
- `descricao` interna só é usada quando não há `descricaoExterna` **e** o apontamento é visível — o usuário foi avisado na UI ao marcar "visível ao cliente".
- Apontamento de tarefa sem solicitação (ex.: tarefa recorrente) mostra o título da tarefa se ela for `visivelCliente`, senão "Atendimento".

---

## 5. Situação de SLA

Calculada pela `CalculadoraSla` (domínio) a partir do `RelogioSla` persistido e do `CalendarioUtil` do tenant, para um instante `agora`:

```
se sem contrato ou CANCELADA          → SEM_SLA
se RESOLVIDA/FECHADA                  → CUMPRIDO | VIOLADO   (compara resolvidaEm com prazoResolucao)
se pausado                            → situação congelada no instante da pausa
senão:
  total      = MinutosUteisEntre(inicioCiclo, prazoResolucao) + minutosPausadosNoCiclo(0 fora de pausa)
  decorrido  = total − MinutosUteisEntre(agora, prazoResolucao)
  percentual = 100 × decorrido / total
  situacao   = agora > prazoResolucao ? VIOLADO : percentual ≥ 75 ? EM_RISCO : NO_PRAZO
```
Primeira resposta tem situação própria (mesma regra com `prazoPrimeiraResposta`, até existir `primeiraRespostaEm`); o card mostra a pior das duas.

**Filtrar por situação na lista** (`?situacaoSla=EM_RISCO`): calcular para milhares de linhas em memória é caro. Estratégia v1:
1. Pré-filtro no SQL por **instante-limite**, que depende só do prazo: para EM_RISCO, persistir também `limiteRisco` (instante em que o relógio atinge 75%), recalculado junto com os prazos (abrir, pausar, retomar, repriorizar).
2. `VIOLADO` ⇔ `agora > prazo_resolucao` (não pausada); `EM_RISCO` ⇔ `limite_risco ≤ agora ≤ prazo_resolucao`.
3. Índice: `solicitacoes (tenant_id, status, prazo_resolucao)` e `(tenant_id, status, limite_risco)`.

Assim filtro e ordenação por prazo ficam no banco; a `CalculadoraSla` só roda para as linhas exibidas (percentual no card).

**Cumprimento no período** (dashboard): `100 × resolvidas no prazo / resolvidas com SLA`, sobre solicitações com `resolvidaEm` no período.

---

## 6. Fila e dashboard

Uma query por bloco, todas com `tenant_id` na frente:

| Bloco | Definição |
|---|---|
| Fila | contagem por `status` não final; `semResponsavel` = não final e `responsavel_id is null`. |
| SLA | `emRisco`/`violadas` pelo §5; `cumprimentoPeriodo` pelo §5. |
| Horas | `Σmin` por `faturavel` no intervalo `[de, ate]`; top N clientes por minutos faturáveis. |
| Contratos | §3 para cada contrato ATIVO, período corrente, ordenado por `percentual` desc. |
| Tarefas | `vencidas` = não finais com `prazo < hoje (fuso do tenant)`; próximas recorrentes = `RecorrenciaTarefa` ATIVA ordenada por `proxima_data`, limite 5. |

**(Implementado no 11a:** `ConsultasDashboard`; fila, SLA, tarefas e recorrências em LINQ com contagens no banco; horas em SQL cru pelo `ConsultaSql`; contratos reaproveitam o consumo do §3, um contrato por vez — candidato a SQL único se o número de contratos ativos crescer.)

Meta RNF-DES-01: p95 < 800 ms com ~50 mil solicitações e ~200 mil apontamentos por tenant. Se algum bloco estourar, é o candidato a cache, não o dashboard inteiro.

---

## 6a. Agenda (marco 13)

Regras no contrato §11b. `LeituraAgenda` (Application) valida o período, decide a situação de cada item e expande as recorrências pelo `GeradorDeDatas`. `ConsultasAgenda` (Infrastructure) só filtra e resolve nomes, com uma consulta por tipo e todas pelos filtros globais:

| Tipo | Filtro no banco |
|---|---|
| Solicitação | não final, com SLA, `prazo_resolucao < início do dia seguinte ao fim` (índice `(tenant_id, status, prazo_resolucao)`) |
| Tarefa | não cancelada, `coalesce(planejada_para, prazo)` no período, ou antes dele se em aberto e o período inclui hoje |
| Projeto | EM_ANDAMENTO, `coalesce(inicio, prazo) ≤ fim` e `coalesce(prazo, inicio) ≥ início` (ou atrasado) |
| Recorrência | ATIVA com `proxima_data ≤ fim` |
| Contrato | ATIVO cruzando o período; consumo do §3 só para os vigentes hoje, um por vez |

Volume esperado: dezenas a centenas de itens por período. Se a tarefa pesar, o candidato é um índice `(tenant_id, coalesce(planejada_para, prazo))`.

---

## 7. Progresso de projeto

```
tarefasTotal       = count(tarefas do projeto, status ≠ CANCELADA)
tarefasConcluidas  = count(… status = CONCLUIDA)
apontadoMinutos    = Σmin (apontamentos de tarefas do projeto)          // faturáveis + não faturáveis
estimativaMinutos  = projeto.estimativa ?? Σ tarefa.estimativa (não canceladas)
```

---

## 7a. Capacidade da semana (marco 13)

Regras no contrato §11c. `ConsultasCapacidade` busca a equipe com a capacidade, as tarefas em aberto com `coalesce(planejada_para, prazo)` na semana (só responsável, dia e estimativa) e os responsáveis das solicitações em aberto com prazo de solução na semana. `LeituraCapacidade` soma por pessoa e por dia em memória: o volume de uma semana é pequeno.

---

## 8. Solicitações semelhantes (RF-CON-02)

**Base de busca:** só solicitações do mesmo tenant, `status ∈ {RESOLVIDA, FECHADA}` e com `solucao` preenchida.

**Documento indexado** (coluna gerada `busca_tsv`):
```
setweight(to_tsvector('portuguese', unaccent(titulo)), 'A')
|| setweight(to_tsvector('portuguese', unaccent(coalesce(solucao_problema,'') || ' ' || descricao)), 'B')
|| setweight(to_tsvector('portuguese', unaccent(solucao_texto)), 'C')
```

**Consulta** para a solicitação `s`:
1. Candidatos: `busca_tsv @@ websearch_to_tsquery('portuguese', unaccent(s.titulo || ' ' || s.descricao))` com operador OR entre termos, **ou** `similarity(titulo, s.titulo) > 0.3` (pg_trgm, pega grafia diferente e títulos curtos).
2. Relevância: `0.7 × ts_rank_cd(busca_tsv, query) normalizado + 0.3 × similarity(titulo, s.titulo)`.
3. Bônus pequeno (+0.05) para mesma `categoria_id` e mesmo `cliente_id` — desempate, nunca filtro (o problema de um cliente costuma aparecer em outro).
4. Excluir a própria `s`; top 5; relevância < 0.15 descartada (melhor nada que sugestão ruim).

**Índices:** GIN em `busca_tsv`; GIN `gin_trgm_ops` em `titulo`. Meta RNF-DES-02: p95 < 500 ms.

**Isolamento:** a query roda sob os filtros globais normais; nunca atravessa tenants (RNF-PRIV-02).

**Implementado (marco 1, `IConsultaSemelhantes`):**
- `unaccent()` não é IMMUTABLE e não pode ir em coluna gerada: o documento usa a configuração de busca `portuguese_unaccent` (cópia de `portuguese` com o dicionário `unaccent` antes do stemmer), criada na migration `ExtensoesBusca`. A consulta usa a mesma configuração.
- "OR entre termos": `plainto_tsquery` com `&` trocado por `|`. Trigrama pelo operador `%` (limiar padrão 0,3), que usa o índice GIN.
- Rank normalizado: `ts_rank_cd(..., 32)` (rank/(rank+1)). Bônus: +0,05 por mesma categoria **e** +0,05 por mesmo cliente (até +0,10). Relevância devolvida com 2 casas.
- No contexto de contato, só solicitações do próprio cliente entram (solução de outro cliente nunca vaza pelo portal).

---

## 8a. Área da Nebrys (F14k, ADR 0014)

A sessão de operação não tem tenant (segurança §5.5): com o contexto vazio, os filtros globais não devolvem nada. A área lê por consultas próprias (`IConsultasNebrys`, implementada em `Acordu.Infrastructure.Persistence.Nebrys.ConsultasNebrys`), um **ponto revisado** de `IgnoreQueryFilters` (convenções §1) que:

- lê só o `Tenant`, a conta de quem o criou (nome e e-mail, tabela do Identity) e a **contagem** dos vínculos da equipe (`vinculos` sem `cliente_id`; os contatos do portal não contam). Nenhuma tabela de negócio do prestador (clientes, contatos, solicitações, tarefas, apontamentos, contratos, anexos) entra na consulta, e um teste de arquitetura garante que nem o tipo é referenciado;
- projeta só os campos da relação com a Nebrys (contrato §17): nome, documento, situação, a marca de demonstração (`tenants.demonstracao`, #225), criada em, fim do teste, início do só leitura, cancelamento, expurgo previsto (calculado como no `/auth/eu`: início do só leitura + `Empresas__DiasAteExpurgo`, nulo fora do só leitura), criador, pessoas na equipe e, no detalhe, a `versao` (`xmin`) para o `If-Match` das ações (F14l);
- lista as criadas por último primeiro (`criado_em desc, id desc`), com o filtro opcional de situação e a paginação do contrato §1. Uma consulta por página, com o criador e a contagem como subconsultas; sem índice novo (a lista é da operação, com poucas empresas no nível 0).

As ações sobre a situação (F14l, contrato §17) respondem com a mesma consulta do detalhe, lida depois do commit (a `versao` já é a nova). A gravação delas não passa por aqui: roda num escopo próprio com o tenant fixado (`ISituacaoDasEmpresas`, segurança §5.5), que lê só o `Tenant` e o fuso do calendário dele. A liberação de documento não lê o read side: acha o registro global pelo HMAC (`IDocumentosTeste`).

Convites de empresa (F14n): a mesma `IConsultasNebrys` lista (`convites_empresa`, emitidos por último primeiro: `criado_em desc, id desc`, paginação do contrato §1) e lê um convite. A tabela é global, sem filtro, então não há `IgnoreQueryFilters`. A `situacao` é calculada na leitura, pelo instante do `TimeProvider` (vencido = `expira_em` já passou, sem uso nem revogação); o hash do token nunca é projetado. As ações de emitir e reenviar respondem com o convite lido depois do commit e o `emailEnviado`. Sem índice para a lista (poucos convites no nível 0); `email` e `token_hash` têm índice para a emissão e o cadastro. O convite usado, revogado ou vencido há 90 dias sai no job diário (#213) e some da lista e do detalhe (404); a entrada da lista de espera de onde ele saiu fica com o `conviteId` nulo.

Lista de espera (F14o): a mesma `IConsultasNebrys` lista `entradas_lista_espera` por ordem de chegada (`criada_em asc, id asc`, paginação do contrato §1), com o `convite_id` do convite mais recente que saiu de cada entrada (subconsulta em `convites_empresa.entrada_lista_espera_id`, que tem índice pela chave estrangeira). Tabela global, sem filtro nem `IgnoreQueryFilters`. A `situacao` é calculada pela `convidada_em`. Sem índice para a ordem (poucas entradas no nível 0); o `email` tem índice único, que é o "não duplica" da inscrição pública.

Empresa de demonstração (F14s): `IEmpresaDeDemonstracao` (implementada em `Acordu.Infrastructure.Persistence.Demonstracao.EmpresaDeDemonstracao`, fora das consultas da área, porque a gravação dela monta dado de negócio) lê, num **ponto revisado** de `IgnoreQueryFilters`, só o `Tenant` marcado (`demonstracao = true`; há um só, sem índice) e as pessoas indicadas: os vínculos de equipe dela (sem `cliente_id`) juntados às contas, menos as contas fictícias (os e-mails do `demonstracao.json`, comparados na forma normalizada do Identity), com nome e e-mail. Nenhum dado de negócio da demonstração vai para a área. As ações respondem com essa mesma leitura, depois do commit.

## 8b. Dados de exemplo (F14r; RF-EMP-05; design do F14, D13)

`IDadosDeExemplo` (implementada em `Acordu.Infrastructure.Persistence.Exemplos.DadosDeExemplo`) lê no tenant do contexto, pelos filtros globais (nenhum `IgnoreQueryFilters`):

- **exemplo** é o cliente com `exemplo = true` e tudo o que tem o `cliente_id` dele (o `ClienteId` desnormalizado do ADR 0002), mais o que pende de uma tarefa dele (cronômetro, comentário);
- **carregados**: existe cliente de exemplo;
- **dado próprio** ("sem dado" é a regra da `ClassificacaoDoExpurgo`, sem contar os exemplos): um registro de qualquer tipo de `ClassificacaoDoExpurgo.Dados` fora dos exemplos (cliente sem a marca, categoria, convite da equipe, raiz ligada a outro cliente, tarefa ou apontamento sem cliente, cronômetro ou comentário de tarefa fora dos exemplos), um papel criado pela empresa ou um vínculo de outra pessoa que não o criador (o vínculo de contato de um cliente de exemplo não conta). Uma consulta `EXISTS` por tipo, parando na primeira que achar; um teste confere que os tipos conferidos são os de `ClassificacaoDoExpurgo.Dados`.

A remoção usa os mesmos critérios: lê os ids dos clientes de exemplo, das tarefas, solicitações, projetos, contratos e cronômetros deles e as chaves dos anexos, e apaga por `ExecuteDelete` (no tenant do contexto, `tenant_id` e `cliente_id = ANY(...)`) toda entidade do modelo com `ClienteId` (vem do modelo: entidade nova entra sozinha), as que pendem da tarefa, os seguidores e as marcas de alerta desses itens; os clientes, com os contatos, saem pela unidade de trabalho. Sem índice novo: as tabelas grandes já têm índice começando por `(tenant_id, cliente_id …)` (§9), e a carga é pequena (dezenas de linhas).

---

## 9. Onde vive no código

- Query handlers em `Acordu.Application` (namespaces `…Consultas` internos, `Acordu.Application.Portal` e `Acordu.Application.Nebrys` separados; §8a).
- Projeções simples: EF Core `Select` para DTO. Somas de consumo, dashboard e semelhantes: SQL em `Acordu.Infrastructure.Persistence.Consultas`, via helper que exige `TenantId` (e `ClienteId` quando portal).
- Índices criados nas migrations (lista consolidada abaixo).
- **Implementado (marco 1):** `IConsultasContrato` (§3), `IConsultaExtratoPortal` (§4, em `Acordu.Application.Portal.Extrato`) e `IConsultasProjeto` (§7). No banco de horas, o saldo acumula até o fim do período ou até hoje, o que vier antes. No extrato, apontamento visível de tarefa sem solicitação usa o título da tarefa se ela for visível, senão "Atendimento"; a ordem é (data, número da solicitação, id do apontamento). Os índices sobre colunas do relógio (`prazo_resolucao`, `limite_risco`) são SQL acrescentado à migration `ConsultasHoras`; um teste confere que existem.

| Tabela | Índice |
|---|---|
| `apontamentos` | `(tenant_id, contrato_id, data) INCLUDE (duracao_minutos, faturavel)`; `(tenant_id, cliente_id, data)`; `(tenant_id, usuario_id, data)` |
| `solicitacoes` | `(tenant_id, numero)` único; `(tenant_id, status, prazo_resolucao)`; `(tenant_id, status, limite_risco)`; `(tenant_id, cliente_id, status)`; GIN `busca_tsv`; GIN trgm `titulo` |
| `tarefas` | `(recorrencia_id, data_referencia)` único parcial; `(tenant_id, responsavel_id, status)`; `(tenant_id, solicitacao_id)`; `(tenant_id, projeto_id)` |
| `interacoes` | `(tenant_id, solicitacao_id, criado_em)` |
| `recorrencias` | `(status, proxima_data)` — varredura do agendador entre tenants |
| `auditoria` | `(tenant_id, ocorrido_em)` — tela; `(ocorrido_em)` — expurgo de 12 meses |
| `ajustes_saldo` | `(tenant_id, contrato_id, data)` |

---

## 10. Parâmetros e defaults

| Parâmetro | Default |
|---|---|
| Limiares de consumo | 80% e 100% |
| Limite de "em risco" | 75% do tempo útil |
| Semelhantes: máximo / relevância mínima | 5 / 0,15 |
| Franquia com vigência parcial no mês | cheia (sem pró-rata) |
| Descrição de linha não visível no extrato | "Atendimento" |

---

## 11. Casos de borda (checklist de teste)

- Contrato sem apontamentos → consumido 0; saldo = franquia.
- Excedente (consumido > franquia) → saldo negativo, percentual > 100.
- Banco de horas com recarga no meio do mês → saldo acumulado correto antes e depois.
- Apontamento não faturável → fora do consumo e do extrato; dentro de `naoFaturavel` e do dashboard.
- Faturável não visível → linha "Atendimento" no extrato; total bate.
- Exclusão de apontamento → limiar desce e é rearmado; não notifica.
- Solicitação pausada → situação congelada; não entra em "em risco" enquanto pausada.
- Solicitação sem contrato → SEM_SLA; fora dos indicadores de cumprimento.
- Semelhantes: título com erro de digitação → encontrado por trigrama; solicitação de outro tenant → nunca aparece.
- Contato com escopo PROPRIAS → extrato do contrato **continua** visível? **Não** no v1: escopo PROPRIAS vê só suas solicitações e não vê extrato (o extrato expõe trabalho pedido por colegas). Registrado aqui para validar com usuários.
