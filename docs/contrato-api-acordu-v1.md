# Acordu — Contrato da API (v1)

| | |
|---|---|
| **Versão** | 0.1 |
| **Data** | 30/09/2026 |
| **Estilo** | REST sobre HTTP, JSON. API-first (SPA interna + portal do cliente; app mobile no futuro). |
| **Base** | `/api/v1` (interna) e `/api/v1/portal` (portal do cliente) |
| **Base de requisitos** | `ERS-acordu-v1.md`, `modelo-de-dominio-acordu.md`, ADRs 0002–0004 |

---

## 1. Convenções gerais

Herdadas do Saldu, com as adaptações do Acordu:

- **Autenticação:** `Authorization: Bearer <token>` em tudo, exceto `/auth/*` e a troca de link do portal.
- **Tenant:** sempre derivado do token (tenant ativo). Nunca na URL nem no corpo.
- **Duas superfícies (ADR 0002):**
  - `/api/v1/*` — equipe do prestador. Token com papel **Cliente** → **403** em qualquer rota interna.
  - `/api/v1/portal/*` — contatos do cliente. Exige papel Cliente ou sessão de link. DTOs próprios, **sem campos internos**.
- **Isolamento:** recurso de outro tenant **ou de outro cliente** → **404** (para o chamador, não existe).
- **Instantes:** ISO-8601 em UTC (`2026-09-30T17:05:00Z`). **Datas civis** (prazo de tarefa, data de apontamento, período): `2026-09-30`. **Período mensal:** `2026-09`.
- **Tempo (ADR 0004):** sempre **inteiro em minutos**, em campos com sufixo `Minutos` (`duracaoMinutos: 90`). Nunca horas decimais no contrato; formatação é do front.
- **Ids:** GUID em string. **Protocolo** da solicitação: inteiro (`numero: 1042`), exibido como `#1042`.
- **Enums:** string em MAIÚSCULAS_COM_UNDERSCORE (`EM_ANDAMENTO`), igual ao banco.
- **Paginação:** `{ "itens": [...], "pagina": 1, "tamanhoPagina": 50, "total": 213 }`; parâmetros `?pagina=&tamanhoPagina=` (máx. 200).
- **Ordenação:** `?ordem=campo` / `?ordem=-campo` nas listagens que a suportam.
- **Concorrência otimista:** respostas de recursos mutáveis trazem `versao` (row version do Postgres). `PATCH` e ações de transição aceitam `If-Match: <versao>`; divergência → **409**.
- **Transições de estado** são sub-recursos de ação (`POST /solicitacoes/{id}/resolucao`), não `PATCH` de campo — como no Saldu.
- **(Acordu) `PATCH` recebe todos os campos editáveis do recurso** (o formulário inteiro); campo opcional ausente ou nulo é apagado. Com `If-Match`, não há perda de atualização. Recurso criado → `201` com o detalhe e `Location`; inativação/exclusão → `204`.
- **Erros:** Problem Details (RFC 9457), §13.

---

## 2. Autenticação e sessão

Igual ao Saldu: `POST /auth/registro`, `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/tenant-ativo`, confirmação de e-mail e recuperação de senha.

Sessão (Acordu, decidido na implementação):
- As rotas que abrem sessão (`registro`, `login`, `refresh`, `tenant-ativo`) respondem `{ "accessToken": "eyJ...", "expiraEm": "..." }` (JWT de 60 min) e entregam o **refresh token só em cookie** `acordu_refresh` — HttpOnly, Secure, SameSite=Strict, `Path=/api/v1/auth`, 30 dias. Nunca no corpo: fica fora do alcance de JavaScript. SPA e API estão no mesmo site, então o cookie é first-party.
- `POST /auth/registro` → `201`: `{ nome, email, senha, nomeEmpresa }`; cria usuário, tenant (Dono) e já abre a sessão (confirmação de e-mail adiada). Campo inválido, e-mail em uso ou senha curta (< 10) → `400 validacao` com o campo em `errors`.
- `POST /auth/login` → `200`; credencial errada, conta bloqueada (5 falhas, 15 min) ou usuário sem vínculo → `401`, sem distinguir. Abre no último tenant usado.
- `POST /auth/refresh` (só o cookie) → `200` com novo access token e **novo** cookie (rotação). Reapresentar um refresh já trocado revoga todas as sessões do usuário naquele tenant → `401`.
- `POST /auth/logout` → `204`; revoga o refresh e apaga o cookie.
- `POST /auth/tenant-ativo` `{ tenantId }` (autenticado) → `200`; sem vínculo no tenant → `403`. Revoga o refresh anterior.
- Mudança de papel/permissão vale no próximo refresh (o access token carrega as permissões).

Específico do Acordu:

| Método | Rota | Descrição |
|---|---|---|
| POST | `/auth/portal/link` | Troca o token de um link de acesso por uma sessão restrita a uma solicitação (RF-POR-08). Público, com rate limit. |
| GET | `/auth/eu` | Usuário atual, tenant ativo, papel, permissões e, se Cliente, `clienteId` e `escopoPortal`. |

**POST `/auth/portal/link`** → `200 OK`
```json
// request
{ "token": "k3Jq...(43 chars)" }
// response
{ "accessToken": "eyJ...", "expiraEm": "2026-09-30T18:05:00Z",
  "solicitacaoId": "f1e...", "tipo": "LINK_SOLICITACAO" }
```
Token inválido, expirado ou revogado → `401` (sem distinguir o motivo).

---

## 3. Usuários, convites e papéis

Mesma superfície do Saldu (`/usuarios`, `/convites`, `/papeis`). O papel **Cliente** não pode ser atribuído por `/convites` — contatos entram pelo §4.2.

---

## 4. Clientes e contatos

### 4.1 Clientes

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/clientes` | Lista (`?busca=&ativo=`). | `cliente.ver` |
| POST | `/clientes` | Cria. | `cliente.gerenciar` |
| GET | `/clientes/{id}` | Detalha, com contatos. | `cliente.ver` |
| PATCH | `/clientes/{id}` | Nome, documento, observações. | `cliente.gerenciar` |
| DELETE | `/clientes/{id}` | Inativa (soft). | `cliente.gerenciar` |

```json
// POST /clientes → 201
{ "nome": "Padaria Bom Grão", "documento": "12.345.678/0001-90", "observacoes": null }
```

### 4.2 Contatos e acesso ao portal

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| POST | `/clientes/{id}/contatos` | Adiciona contato. | `cliente.gerenciar` |
| PATCH | `/clientes/{id}/contatos/{contatoId}` | Edita. | `cliente.gerenciar` |
| DELETE | `/clientes/{id}/contatos/{contatoId}` | Inativa. | `cliente.gerenciar` |
| POST | `/clientes/{id}/contatos/{contatoId}/acesso-portal` | Convida o contato para o portal. | `portal.gerenciar_acessos` |
| PATCH | `/clientes/{id}/contatos/{contatoId}/acesso-portal` | Altera `escopoPortal`. | `portal.gerenciar_acessos` |
| DELETE | `/clientes/{id}/contatos/{contatoId}/acesso-portal` | Revoga acesso (mantém histórico). | `portal.gerenciar_acessos` |

```json
// POST .../acesso-portal → 202 (convite enviado por e-mail)
{ "escopoPortal": "TODAS_DO_CLIENTE" }
```
E-mail já em uso por outro contato ativo do mesmo cliente → `422`.

---

## 5. Contratos

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/contratos` | Lista (`?clienteId=&status=`). | `contrato.ver` |
| POST | `/contratos` | Cria. | `contrato.gerenciar` |
| GET | `/contratos/{id}` | Detalha (inclui SLA e consumo do período corrente). | `contrato.ver` |
| PATCH | `/contratos/{id}` | Franquia, vigência (fim), SLA. | `contrato.gerenciar` |
| POST | `/contratos/{id}/suspensao` · `/reativacao` · `/encerramento` | Transições. | `contrato.gerenciar` |
| POST | `/contratos/{id}/ajustes` | Ajuste de saldo (banco de horas). | `contrato.ajustar_saldo` |
| GET | `/contratos/{id}/consumo` | Consumo de um período (`?periodo=2026-09`). | `contrato.ver` |
| POST | `/contratos/{id}/periodos/{periodo}/fechamento` | Fecha o mês para edição de apontamentos. | `contrato.fechar_periodo` |

**POST `/contratos`** → `201`
```json
{
  "clienteId": "c1a...",
  "tipo": "BANCO_HORAS",
  "inicio": "2026-10-01", "fim": null,
  "franquiaMinutos": 1200,
  "sla": [
    { "prioridade": "URGENTE", "primeiraRespostaMinutos": 60,  "resolucaoMinutos": 240 },
    { "prioridade": "ALTA",    "primeiraRespostaMinutos": 120, "resolucaoMinutos": 960 },
    { "prioridade": "MEDIA",   "primeiraRespostaMinutos": 240, "resolucaoMinutos": 1920 },
    { "prioridade": "BAIXA",   "primeiraRespostaMinutos": 480, "resolucaoMinutos": 4800 }
  ]
}
```
`422`: franquia incoerente com o tipo; SLA incompleto; já existe contrato ATIVO do mesmo tipo para o cliente no período.

- `GET /contratos/{id}` traz `sla` (quatro prioridades em minutos), `periodosFechados` (`["2026-09"]`), `versao` e `consumoAtual` (o consumo do mês corrente no fuso do tenant).
- `POST .../encerramento` aceita corpo opcional `{ "data": "2026-10-31" }` (sem corpo: hoje); encerrar nunca estende a vigência. As transições respondem `200` com o contrato.
- `POST .../ajustes` `{ "quantidadeMinutos": 600, "motivo": "...", "data": "2026-10-02" }` (data opcional) → `201`; só em BANCO_HORAS não encerrado (`422`).
- `POST .../periodos/{periodo}/fechamento` → `204`; só mês já encerrado e dentro da vigência (`400` no campo `periodo`); fechar de novo não muda nada.

**GET `/contratos/{id}/consumo?periodo=2026-09`**
```json
{
  "contratoId": "k9...", "tipo": "MENSAL_FIXO", "periodo": "2026-09",
  "franquiaMinutos": 1200,
  "consumidoMinutos": 1035,
  "naoFaturavelMinutos": 180,
  "ajustesMinutos": 0,
  "saldoMinutos": 165,
  "percentual": 86,
  "periodoFechado": false
}
```
Para `BANCO_HORAS`, `saldoMinutos` é o saldo **acumulado** desde o início do contrato (ver read side §3.2) e `periodo` só delimita o detalhamento.

---

## 6. Configurações do tenant

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET/PUT | `/configuracoes/calendario` | Fuso, expediente por dia, feriados (ADR 0003). | `configuracao.gerenciar` (GET: qualquer interno) |
| GET/PUT | `/configuracoes/portal` | Nome exibido e logotipo (RF-POR-09), dias para fechamento automático. | `configuracao.gerenciar` |
| GET/POST/PATCH/DELETE | `/categorias` | Categorias de solicitação. | `configuracao.gerenciar` |

```json
// PUT /configuracoes/calendario
{
  "fusoHorario": "America/Sao_Paulo",
  "expediente": [
    { "diaSemana": "SEGUNDA", "inicio": "08:00", "fim": "18:00" },
    { "diaSemana": "SEXTA",   "inicio": "08:00", "fim": "17:00" }
  ],
  "feriados": ["2026-10-12", "2026-11-02", "2026-11-15"]
}
```
Dias ausentes em `expediente` são não úteis.

---

## 7. Solicitações

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/solicitacoes` | Lista filtrável. | `solicitacao.ver` |
| POST | `/solicitacoes` | Cria (origem INTERNA). | `solicitacao.criar` |
| GET | `/solicitacoes/{id}` | Detalha (inclui SLA calculado). | `solicitacao.ver` |
| PATCH | `/solicitacoes/{id}` | Título, descrição, prioridade, categoria, responsável, contrato. | `solicitacao.editar` |
| POST | `/solicitacoes/{id}/inicio` · `/aguardando-cliente` · `/aguardando-terceiro` · `/retomada` | Transições. | `solicitacao.editar` |
| POST | `/solicitacoes/{id}/resolucao` | Resolve com solução. | `solicitacao.editar` |
| POST | `/solicitacoes/{id}/reabertura` · `/cancelamento` | Com motivo. | `solicitacao.editar` |
| GET | `/solicitacoes/{id}/interacoes` | Linha do tempo (internas + externas). | `solicitacao.ver` |
| POST | `/solicitacoes/{id}/interacoes` | Comentário, com anexos (multipart). | `solicitacao.comentar` |
| GET | `/solicitacoes/{id}/semelhantes` | Até 5 resolvidas semelhantes (RF-CON-02). | `solicitacao.ver` |
| POST | `/solicitacoes/{id}/acessos-portal` | Emite link sem senha para um contato. | `portal.gerenciar_acessos` |
| DELETE | `/solicitacoes/{id}/acessos-portal/{acessoId}` | Revoga link. | `portal.gerenciar_acessos` |
| GET | `/anexos/{id}/download` | `302` para URL assinada (curta). | `solicitacao.ver` |

**Filtros do GET `/solicitacoes`:** `?status=ABERTA,EM_ANDAMENTO&clienteId=&responsavelId=&prioridade=&categoriaId=&situacaoSla=EM_RISCO&busca=&abertaDe=&abertaAte=&ordem=-abertaEm`.

Decidido na implementação (6c2):
- `situacaoSla` no filtro aceita `EM_RISCO` e `VIOLADO`, resolvidos no banco pelos instantes persistidos (read side §5); solicitação pausada (AGUARDANDO_CLIENTE) não entra em nenhum dos dois. `ordem`: `abertaEm` (padrão `-abertaEm`), `numero`, `prioridade`, `prazoResolucao`. `busca`: trecho do título ou número (`#1042`).
- `PATCH` recebe título, descrição, prioridade, categoria e responsável (formulário inteiro); trocar o contrato fica para depois (modelo §4.3). Responsável precisa ser da equipe (`400` no campo).
- Transições respondem `200` com a solicitação. `POST /interacoes` aceita, por ora, JSON `{ "texto": "...", "visibilidade": "EXTERNA" }`; vira multipart com anexos no marco de anexos. Interação EXTERNA da equipe registra a primeira resposta.
- O detalhe traz também `descricao`, `categoriaId`, `situacaoSla` (fora do bloco `sla`, para solicitações sem contrato), `resolvidaEm`, `fechadaEm`, `canceladaEm` e `motivoCancelamento`.

**POST `/solicitacoes`** → `201`
```json
{ "clienteId": "c1a...", "contatoId": "ct7...", "contratoId": "k9...",
  "titulo": "Ajustar banner da campanha de outubro",
  "descricao": "Trocar a data para 15/10.", "prioridade": "MEDIA",
  "categoriaId": "cat...", "responsavelId": null }
```
Contrato de outro cliente ou não vigente → `422`. Sem `contratoId`, a API usa o contrato ativo do cliente quando houver exatamente um; senão, fica sem contrato (sem SLA).

**GET `/solicitacoes/{id}`**
```json
{
  "id": "s1...", "numero": 1042, "versao": "3419",
  "titulo": "Ajustar banner da campanha de outubro",
  "cliente": { "id": "c1a...", "nome": "Padaria Bom Grão" },
  "contato": { "id": "ct7...", "nome": "Marina" },
  "contratoId": "k9...", "prioridade": "MEDIA",
  "status": "EM_ANDAMENTO", "statusExterno": "EM_ANDAMENTO",
  "origem": "PORTAL", "responsavel": { "id": "u2...", "nome": "Daniel" },
  "abertaEm": "2026-09-30T13:02:00Z",
  "sla": {
    "prazoPrimeiraResposta": "2026-09-30T17:02:00Z", "primeiraRespostaEm": "2026-09-30T13:40:00Z",
    "prazoResolucao": "2026-10-02T15:02:00Z", "pausado": false,
    "situacao": "NO_PRAZO", "percentualConsumido": 22
  },
  "tempoApontadoMinutos": 90,
  "tarefas": [ { "id": "t1...", "titulo": "Refazer arte", "status": "FAZENDO" } ],
  "solucao": null
}
```

**POST `/solicitacoes/{id}/resolucao`**
```json
{ "solucao": { "problema": "Data errada no banner.", "causa": "Briefing antigo.",
  "texto": "Arte refeita com a data 15/10 e publicada." } }
```

**POST `/solicitacoes/{id}/interacoes`** — `multipart/form-data`: `texto`, `visibilidade` (`INTERNA`|`EXTERNA`, padrão `INTERNA`), `anexos[]` (até 5, 20 MB cada). → `201` com a interação.

**GET `/solicitacoes/{id}/semelhantes`**
```json
[ { "id": "s0...", "numero": 981, "titulo": "Banner com data errada",
    "solucaoResumo": "Arte refeita e republicada...", "resolvidaEm": "2026-08-12T19:00:00Z",
    "relevancia": 0.71 } ]
```

**POST `/solicitacoes/{id}/acessos-portal`** → `201`
```json
// request
{ "contatoId": "ct7...", "validadeDias": 30, "enviarEmail": true }
// response (o link só aparece aqui, uma vez)
{ "acessoId": "a1...", "expiraEm": "2026-10-30T17:10:00Z",
  "link": "https://acordu.app.br/portal/l/k3Jq..." }
```

---

## 8. Tarefas e projetos

### 8.1 Tarefas

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/tarefas` | Lista (`?status=&responsavelId=&solicitacaoId=&projetoId=&clienteId=&prazoAte=&minhas=true`). | `tarefa.ver` |
| POST | `/tarefas` | Cria (avulsa, de solicitação e/ou projeto). | `tarefa.criar` |
| GET | `/tarefas/{id}` | Detalha, com checklist e total apontado. | `tarefa.ver` |
| PATCH | `/tarefas/{id}` | Título, descrição, responsável, prazo, estimativa, `visivelCliente`. | `tarefa.editar` |
| POST | `/tarefas/{id}/inicio` · `/conclusao` · `/reabertura` · `/cancelamento` | Transições. | `tarefa.editar` |
| POST | `/tarefas/{id}/itens` | Adiciona item de checklist. | `tarefa.editar` |
| PATCH/DELETE | `/tarefas/{id}/itens/{itemId}` | Marca/edita/remove item. | `tarefa.editar` |

O quadro (kanban) é o mesmo `GET /tarefas` agrupado no front por `status`.

```json
// POST /tarefas → 201
{ "titulo": "Refazer arte", "solicitacaoId": "s1...", "projetoId": null,
  "responsavelId": "u2...", "prazo": "2026-10-01", "estimativaMinutos": 120,
  "visivelCliente": true }
```

### 8.2 Projetos

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET/POST | `/projetos` | Lista (`?clienteId=&status=`) / cria. | `projeto.ver` / `projeto.gerenciar` |
| GET/PATCH | `/projetos/{id}` | Detalha (com progresso) / edita. | `projeto.ver` / `projeto.gerenciar` |
| POST | `/projetos/{id}/conclusao` · `/cancelamento` | Transições. | `projeto.gerenciar` |

```json
// progresso em GET /projetos/{id}
"progresso": { "tarefasConcluidas": 7, "tarefasTotal": 12,
               "apontadoMinutos": 1510, "estimativaMinutos": 2400 }
```

---

## 9. Apontamentos e cronômetro

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/apontamentos` | Lista (`?usuarioId=&clienteId=&contratoId=&tarefaId=&de=&ate=&faturavel=`). | `apontamento.ver` |
| POST | `/apontamentos` | Registra. | `apontamento.criar` |
| PATCH | `/apontamentos/{id}` | Edita (autor ou `apontamento.editar_todos`). | `apontamento.criar` |
| DELETE | `/apontamentos/{id}` | Exclui (idem). | `apontamento.criar` |
| GET | `/cronometro` | Cronômetro ativo do usuário (ou `204`). | `apontamento.criar` |
| POST | `/cronometro/inicio` | Inicia numa tarefa (`{ "tarefaId": "..." }`). | `apontamento.criar` |
| POST | `/cronometro/parada` | Para e cria o apontamento (corpo opcional: descrição, faturável). | `apontamento.criar` |
| DELETE | `/cronometro` | Descarta sem apontar. | `apontamento.criar` |

**POST `/apontamentos`** → `201`
```json
{ "tarefaId": "t1...", "data": "2026-09-30", "duracaoMinutos": 90,
  "descricao": "Ajuste de layout e exportação", "descricaoExterna": "Arte refeita",
  "faturavel": true, "visivelCliente": true }
```
`contratoId` não é enviado: é resolvido pela tarefa e congelado (ADR 0004). Data em período fechado → `422` (`tipo: .../periodo-fechado`). Iniciar cronômetro com outro ativo → `409`.

---

## 10. Tarefas recorrentes

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET/POST | `/recorrencias` | Lista (`?clienteId=&status=`) / cria. | `recorrencia.ver` / `recorrencia.gerenciar` |
| GET/PATCH/DELETE | `/recorrencias/{id}` | Detalha / edita modelo e regra / encerra. | idem |
| POST | `/recorrencias/{id}/pausa` · `/retomada` | Transições. | `recorrencia.gerenciar` |
| GET | `/recorrencias/{id}/previa` | Próximas datas (`?quantidade=6`). | `recorrencia.ver` |

```json
// POST /recorrencias → 201
{
  "clienteId": "c1a...", "contratoId": "k9...",
  "modelo": { "titulo": "Fechamento mensal", "responsavelId": "u2...",
              "estimativaMinutos": 240, "visivelCliente": true,
              "checklist": ["Conciliar extratos", "Emitir guias", "Enviar ao cliente"] },
  "regra": { "periodicidade": "MENSAL", "diaReferencia": 31,
             "antecedenciaDias": 3, "inicio": "2026-10-01", "fim": null }
}
```
- `inicio` (opcional; padrão: hoje no fuso do tenant) é a âncora: nenhuma ocorrência antes dele, e o mês dele define o ciclo de TRIMESTRAL e ANUAL.
- `diaReferencia`: 1–7 em SEMANAL (1 = segunda, 7 = domingo); 1–31 nos demais (dia inexistente no mês vira o último dia).
- `antecedenciaDias`: 0–365.
`GET .../previa` → `["2026-10-31", "2026-11-30", "2026-12-31", "2027-01-31", "2027-02-28", "2027-03-31"]` (clamp de fim de mês visível para o usuário conferir).

---

## 11. Dashboard e notificações

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/dashboard` | Composição da tela inicial (`?de=&ate=`). | `dashboard.ver` |
| GET | `/notificacoes` | Notificações do usuário (`?naoLidas=true`). | autenticado |
| POST | `/notificacoes/{id}/leitura` · `/notificacoes/leitura` | Marca uma / todas como lidas. | autenticado |
| GET/PUT | `/notificacoes/preferencias` | Quais e-mails receber (RF-NOT-04). | autenticado |

**GET `/dashboard`**
```json
{
  "fila": { "abertas": 14, "emAndamento": 9, "aguardandoCliente": 4, "semResponsavel": 2 },
  "sla": { "emRisco": 3, "violadas": 1, "cumprimentoPeriodo": 93 },
  "horas": { "faturavelMinutos": 9120, "naoFaturavelMinutos": 1340,
             "porCliente": [ { "clienteId": "c1a...", "nome": "Padaria Bom Grão", "minutos": 1035 } ] },
  "contratos": [ { "contratoId": "k9...", "cliente": "Padaria Bom Grão", "tipo": "MENSAL_FIXO",
                   "franquiaMinutos": 1200, "consumidoMinutos": 1035, "percentual": 86 } ],
  "tarefas": { "vencidas": 2, "proximasRecorrentes": [ { "titulo": "Fechamento mensal",
               "cliente": "Contábil Silva", "data": "2026-10-31" } ] }
}
```

---

## 12. Portal do cliente (`/api/v1/portal`)

Todas as rotas aplicam o filtro por `ClienteId` (e `ContatoId` se `escopoPortal = PROPRIAS`). Sessão de **link** acessa só as rotas marcadas com ✓L, e só a sua solicitação.

| Método | Rota | Descrição | Link |
|---|---|---|---|
| GET | `/portal/eu` | Contato, cliente, identidade visual do prestador. | ✓L |
| GET | `/portal/solicitacoes` | Lista (`?statusExterno=&pagina=`). | |
| POST | `/portal/solicitacoes` | Abre solicitação (multipart: título, descrição, prioridade sugerida, anexos). | |
| GET | `/portal/solicitacoes/{id}` | Detalhe externo. | ✓L |
| GET | `/portal/solicitacoes/{id}/interacoes` | Só interações EXTERNAS. | ✓L |
| POST | `/portal/solicitacoes/{id}/interacoes` | Responde (sempre EXTERNA). | ✓L |
| POST | `/portal/solicitacoes/{id}/aprovacao` | Aprova entrega (RESOLVIDA → FECHADA). | ✓L |
| POST | `/portal/solicitacoes/{id}/reprovacao` | Reprova com motivo (→ EM_ANDAMENTO). | ✓L |
| GET | `/portal/contratos` | Contratos ativos do cliente. | |
| GET | `/portal/contratos/{id}/extrato` | Extrato do período (`?periodo=2026-09`). | |
| GET | `/portal/anexos/{id}/download` | `302` para URL assinada, só de interações externas. | ✓L |

**GET `/portal/solicitacoes/{id}`** — nada de status interno, responsável completo ou tempo não visível:
```json
{
  "id": "s1...", "numero": 1042, "titulo": "Ajustar banner da campanha de outubro",
  "statusExterno": "EM_ANDAMENTO",
  "abertaEm": "2026-09-30T13:02:00Z",
  "responsavel": { "nome": "Daniel" },
  "sla": { "prazoResolucao": "2026-10-02T15:02:00Z", "situacao": "NO_PRAZO", "pausado": false },
  "tarefasVisiveis": [ { "titulo": "Refazer arte", "status": "FAZENDO" } ],
  "tempoVisivelMinutos": 90,
  "podeAprovar": false
}
```

**GET `/portal/contratos/{id}/extrato?periodo=2026-09`**
```json
{
  "contrato": { "tipo": "MENSAL_FIXO", "franquiaMinutos": 1200 },
  "periodo": "2026-09",
  "consumidoMinutos": 1035, "saldoMinutos": 165, "percentual": 86,
  "itens": [
    { "data": "2026-09-30", "solicitacaoNumero": 1042, "solicitacaoTitulo": "Ajustar banner...",
      "descricao": "Arte refeita", "duracaoMinutos": 90 }
  ]
}
```
`consumidoMinutos` do extrato soma **apenas** apontamentos faturáveis; a lista mostra os `visivelCliente`. Um apontamento faturável marcado como não visível entra no total com a descrição genérica "Atendimento" (o cliente nunca vê um total que não consegue conferir linha a linha).

---

## 13. Formato de erro (RFC 9457)

```json
{
  "type": "https://acordu.app.br/erros/transicao-invalida",
  "title": "Transição de status inválida",
  "status": 422,
  "detail": "Não é possível ir de FECHADA para EM_ANDAMENTO.",
  "traceId": "00-4bf9..."
}
```

Tipos (`type`) do v1: `validacao` (com `errors` por campo), `transicao-invalida`, `periodo-fechado`, `contrato-incoerente`, `conflito-versao`, `cronometro-ativo`, `nao-encontrado`, `sem-permissao`, `nao-autenticado` (401), `limite-requisicoes` (429), `erro-interno` (500).

---

## 14. Códigos de status

| Código | Uso |
|---|---|
| `200` / `201` / `202` / `204` | Sucesso (202 quando dispara e-mail assíncrono, ex.: convite). |
| `302` | Download de anexo (URL assinada). |
| `400` | JSON malformado, parâmetro inválido. |
| `401` | Sem token, token inválido, link inválido/expirado. |
| `403` | Sem permissão; papel Cliente em rota interna; sessão de link fora do seu escopo. |
| `404` | Inexistente, de outro tenant **ou de outro cliente**. |
| `409` | Versão divergente (`If-Match`); cronômetro já ativo. |
| `413` | Anexo acima do limite. |
| `422` | Regra de negócio violada. |
| `429` | Rate limit (login, portal, link). |

---

## 15. Versionamento

`/api/v1`; mudança incompatível abre `/api/v2`. Entradas previstas para a v2 (e-mail e WhatsApp como origem, base de conhecimento, projeção do contrato, relatório PDF) entram como **novos** endpoints ou campos aditivos.
