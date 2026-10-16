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

Convenções gerais, com as adaptações marcadas como (Acordu):

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
- **Transições de estado** são sub-recursos de ação (`POST /solicitacoes/{id}/resolucao`), não `PATCH` de campo.
- **(Acordu) `PATCH` recebe todos os campos editáveis do recurso** (o formulário inteiro); campo opcional ausente ou nulo é apagado. Com `If-Match`, não há perda de atualização. Recurso criado → `201` com o detalhe e `Location`; inativação/exclusão → `204`.
- **Erros:** Problem Details (RFC 9457), §13.

---

## 2. Autenticação e sessão

Rotas: `POST /auth/registro`, `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/tenant-ativo`, confirmação de e-mail e recuperação de senha.

Sessão (Acordu, formalizado no ADR 0007):
- As rotas que abrem sessão (`registro`, `login`, `refresh`, `tenant-ativo`) respondem `{ "accessToken": "eyJ...", "expiraEm": "..." }` (JWT de 60 min) e entregam o **refresh token só em cookie** `acordu_refresh` — HttpOnly, Secure, SameSite=Strict, `Path=/api/v1/auth`, 30 dias. Nunca no corpo: fica fora do alcance de JavaScript. SPA e API estão no mesmo site, então o cookie é first-party.
- `POST /auth/registro` → `201`: `{ nome, email, senha, nomeEmpresa }`; cria usuário, tenant (Dono), já abre a sessão e envia a confirmação de e-mail (o login **não** exige e-mail confirmado; `/auth/eu` traz `emailConfirmado` para a SPA avisar). Campo inválido, e-mail em uso ou senha curta (< 10) → `400 validacao` com o campo em `errors`.
- `POST /auth/login` → `200`; credencial errada, conta bloqueada (5 falhas, 15 min) ou usuário sem vínculo → `401`, sem distinguir. Abre no último tenant usado.
- `POST /auth/refresh` (só o cookie) → `200` com novo access token e **novo** cookie (rotação). Reapresentar um refresh já trocado revoga todas as sessões do usuário naquele tenant → `401`.
- `POST /auth/logout` → `204`; revoga o refresh e apaga o cookie.
- `POST /auth/confirmacao-email` `{ token }` (público) → `204`; token inválido, expirado ou usado → `401`. Link `{App__UrlPublica}/conta/confirmacao/{token}`, válido por 48 h.
- `POST /auth/confirmacao-email/reenvio` (com conta) → `202`; invalida o link anterior; e-mail já confirmado não recebe nada.
- `POST /auth/senha/esqueci` `{ email }` (público) → sempre `202`, tenha o e-mail conta ou não. Link `{App__UrlPublica}/conta/senha/{token}`, válido por 1 h; novo pedido invalida o anterior.
- `POST /auth/senha/redefinir` `{ token, novaSenha }` (público) → `204` e apaga o cookie: troca a senha, confirma o e-mail, desbloqueia a conta e revoga **todas** as sessões do usuário (em todos os tenants). Senha fraca → `400` em `novaSenha` (o link continua valendo); token inválido → `401`.
- `POST /auth/tenant-ativo` `{ tenantId }` (autenticado) → `200`; sem vínculo no tenant → `403`. Revoga o refresh anterior.
- Mudança de papel/permissão vale no próximo refresh (o access token carrega as permissões).

Específico do Acordu:

| Método | Rota | Descrição |
|---|---|---|
| POST | `/auth/portal/link` | Troca o token de um link de acesso por uma sessão restrita a uma solicitação (RF-POR-08). Público, com rate limit. |
| POST | `/auth/portal/convite/consulta` | Dados do convite do portal para a tela de aceite. Público, com rate limit. |
| POST | `/auth/portal/convite` | Aceita o convite: cria a conta (ou confere a senha da existente), vincula o contato e abre a sessão. Público, com rate limit. |
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
Contato ou cliente inativado também invalida o link. A sessão de link não tem refresh nem usuário: `/auth/eu` e `/auth/tenant-ativo` respondem `403` a ela.

**Convite do portal** (decidido na implementação, segurança §4):
- O token chega no link do e-mail (`{App__UrlPublica}/portal/convite/{token}`); a SPA o tira da URL na primeira carga. Vale 7 dias a partir do envio.
- `POST /auth/portal/convite/consulta` `{ token }` → `200` `{ nomePrestador, nomeContato, email, temConta }`. `temConta` diz se a tela pede nome e senha (conta nova) ou só a senha (conta existente).
- `POST /auth/portal/convite` `{ token, nome?, senha }` → `201` com o mesmo corpo e cookie do login. Conta nova: `nome` (ou o nome do contato) e senha (≥ 10), e-mail já confirmado. Conta existente: a senha precisa ser a dela (o token sozinho não adota uma conta); errada → `400` em `senha`, contando para o bloqueio. E-mail que já tem vínculo no tenant → `422`.
- Token inválido, expirado, revogado ou já usado → `401`, sem distinguir. Rotas públicas ignoram um `Bearer` enviado junto.

---

## 3. Usuários, convites e papéis

Superfície de equipe (`/usuarios`, `/convites`, `/papeis`), com o aceite do convite em `/auth` (decidido em 07/10, ver abaixo). O papel **Cliente** não pode ser atribuído por `/convites` nem por troca de papel — contatos entram pelo §4.2.

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/equipe` | Pessoas da equipe só com `[{ id, nome }]`, por nome: para escolher responsável e mencionar (decidido em 16/10). | qualquer papel interno |
| GET | `/usuarios` | Equipe do tenant (contatos ficam nos clientes): `[{ usuarioId, nome, email, papel: { id, nome }, dono, capacidadeSemanalMinutos }]`. | `usuario.convidar` ou `usuario.remover` |
| PATCH | `/usuarios/{id}/papel` | `{ papelId }` → `200` com a pessoa. Vale no próximo refresh dela. | `papel.gerenciar` |
| PUT | `/usuarios/{id}/capacidade` | `{ capacidadeSemanalMinutos }` (0 a 10080; padrão 2400 = 40h para quem entra na equipe) → `200` com a pessoa. Base do planejamento (agenda e capacidade, marco 13). Fora da faixa → `400`. | `papel.gerenciar` |
| DELETE | `/usuarios/{id}` | Remove do tenant e revoga as sessões dela aqui → `204`. A conta global continua. | `usuario.remover` |
| GET | `/convites` | Pendentes: `[{ id, email, papel, criadoEm, expiraEm }]` (`expiraEm` nulo até o e-mail sair). | `usuario.convidar` |
| POST | `/convites` | `{ email, papelId }` → `202` com o convite; e-mail com link de aceite. | `usuario.convidar` |
| DELETE | `/convites/{id}` | Revoga → `204`. | `usuario.convidar` |
| POST | `/auth/convite/consulta` | Público: `{ token }` → `{ nomeTenant, email, papel, temConta }`. | — |
| POST | `/auth/convite` | Público: `{ token, nome?, senha }` → `201` com a sessão (como o login). | — |

Regras:
- **Sempre ≥ 1 Dono:** rebaixar ou remover o último vínculo com o papel padrão Dono → `422`. Papel customizado com todas as permissões não conta como Dono.
- Convite: papel do tipo CLIENTE → `422`; e-mail que já tem vínculo no tenant (equipe ou contato) → `422`; reconvidar o mesmo e-mail revoga o pendente anterior. Token como o do portal (gerado no envio, só o hash no banco, 7 dias).
- Aceite: em vez de `POST /convites/{id}/aceite`, segue o padrão do portal: só o token, no corpo, em `/auth` (o Id na URL vaza em log e histórico). Conta nova exige `nome` e nasce com e-mail confirmado; conta existente exige a senha dela (`400` em `senha`, conta para o bloqueio). Token inválido, expirado, revogado ou usado → `401`.
- Pessoa que não é da equipe deste tenant (inexistente, de outro tenant ou contato de cliente) → `404`.

Papéis (6e2, todos com `papel.gerenciar`):

| Método | Rota | Descrição |
|---|---|---|
| GET | `/papeis` | `[{ id, nome, tipo, padrao, permissoes, pessoas }]`, padrões primeiro (Dono, Membro, Leitor, Cliente). |
| GET | `/papeis/permissoes` | Catálogo do que um papel sob medida pode ter (as internas, §3.1 da segurança). |
| POST | `/papeis` | `{ nome, permissoes }` → `201`. Sempre INTERNO. |
| PATCH | `/papeis/{id}` | Formulário inteiro `{ nome, permissoes }` → `200`. Vale no próximo refresh de quem tem o papel. |
| DELETE | `/papeis/{id}` | `204`; revoga os convites pendentes com o papel. |

- Nome repetido no tenant (sem diferenciar maiúsculas) → `400` em `nome`; permissão fora do catálogo interno ou lista vazia → `400` em `permissoes`.
- Papel padrão não é alterado nem excluído → `422 transicao-invalida`. Papel em uso (alguém com ele) não é excluído → `422`.

---

## 4. Clientes e contatos

### 4.1 Clientes

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/clientes` | Lista (`?busca=&ativo=&etiqueta=&responsavelId=`). | `cliente.ver` |
| GET | `/clientes/etiquetas` | Etiquetas em uso no tenant, em ordem alfabética. | `cliente.ver` |
| POST | `/clientes` | Cria. | `cliente.gerenciar` |
| GET | `/clientes/{id}` | Detalha, com contatos. | `cliente.ver` |
| PATCH | `/clientes/{id}` | Nome, documento, observações. | `cliente.gerenciar` |
| DELETE | `/clientes/{id}` | Inativa (soft). | `cliente.gerenciar` |
| POST | `/clientes/{id}/reativacao` | Reativa o cliente inativo → `200` com o detalhe. Os acessos ao portal revogados na inativação não voltam (decidido em 16/10). | `cliente.gerenciar` |

```json
// POST /clientes → 201 (só "nome" é obrigatório)
{ "nome": "Padaria Bom Grão", "razaoSocial": "Bom Grão Panificadora Ltda", "documento": "12.345.678/0001-90",
  "inscricaoEstadual": "ISENTO", "email": "contato@bomgrao.com", "telefone": "(18) 3333-0000",
  "endereco": { "cep": "19700-000", "logradouro": "Rua das Flores", "numero": "120", "complemento": null,
                "bairro": "Centro", "cidade": "Bastos", "uf": "SP" },
  "responsavelId": "0f3…", "etiquetas": ["Varejo"], "observacoes": null }
```

Cadastro ampliado (decidido em 16/10, RF-CLI-01, 06 e 07):
- O `PATCH` recebe o formulário inteiro, como antes: campo ausente ou nulo apaga.
- O detalhe devolve os mesmos campos, com `responsavel` como `{ id, nome }` em vez do Id. A lista devolve também `razaoSocial`, `responsavel` e `etiquetas`.
- Validação, com o erro no campo:
  - `endereco`: quando presente, exige CEP (8 dígitos), logradouro, cidade e UF válida; o erro vem com o caminho aninhado (`endereco.cep`);
  - `inscricaoEstadual`: números e pontuação, ou `ISENTO`;
  - `email`: formato mínimo;
  - `responsavelId`: precisa ser da equipe, conferido só quando o valor muda (quem saiu da equipe continua aparecendo até alguém trocar);
  - `etiquetas`: até 10, de até 30 caracteres; repetidas sem diferenciar maiúsculas viram uma, com a grafia da primeira.
- `busca` procura também na razão social; `etiqueta` filtra pela grafia exata, como aparece em `/clientes/etiquetas`.

### 4.2 Contatos e acesso ao portal

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| POST | `/clientes/{id}/contatos` | Adiciona contato. | `cliente.gerenciar` |
| PATCH | `/clientes/{id}/contatos/{contatoId}` | Edita. | `cliente.gerenciar` |
| DELETE | `/clientes/{id}/contatos/{contatoId}` | Inativa. | `cliente.gerenciar` |
| POST | `/clientes/{id}/contatos/{contatoId}/reativacao` | Reativa o contato → `200` com o contato. Cliente inativo → `422 transicao-invalida`; e-mail já usado por outro contato ativo → `422`. Sem acesso ao portal: convida-se de novo. | `cliente.gerenciar` |
| POST | `/clientes/{id}/contatos/{contatoId}/acesso-portal` | Convida o contato para o portal. | `portal.gerenciar_acessos` |
| PATCH | `/clientes/{id}/contatos/{contatoId}/acesso-portal` | Altera `escopoPortal`. | `portal.gerenciar_acessos` |
| DELETE | `/clientes/{id}/contatos/{contatoId}/acesso-portal` | Revoga acesso (mantém histórico). | `portal.gerenciar_acessos` |

```json
// POST .../acesso-portal → 202 (convite enviado por e-mail); corpo: o contato
{ "escopoPortal": "TODAS_DO_CLIENTE" }
```
E-mail já em uso por outro contato ativo do mesmo cliente → `422`.

Decidido na implementação:
- O contato no detalhe do cliente traz `acessoPortal`: `null`, `{ "situacao": "CONVIDADO", "escopoPortal": "...", "conviteExpiraEm": "..." }` (nulo até o e-mail sair) ou `{ "situacao": "ATIVO", "escopoPortal": "...", "conviteExpiraEm": null }`.
- `POST`: `escopoPortal` obrigatório. Reconvidar revoga o convite pendente anterior (o link antigo para de valer). Contato que já tem acesso, contato ou cliente inativo, ou e-mail que já tem vínculo neste tenant (equipe ou contato de outro cliente) → `422`.
- `PATCH` `{ escopoPortal }` → `200` com o contato: altera o acesso (vale no próximo refresh do contato) ou o convite pendente; sem nenhum dos dois → `422`.
- `DELETE` → `204`, idempotente: revoga convites pendentes, links por solicitação, o vínculo e os refresh tokens do contato no tenant. A conta global dele continua existindo e ele pode ser convidado de novo. Inativar o contato faz o mesmo; inativar o cliente, para todos os contatos.

### 4.3 Arquivos do cliente (marco 16b, RF-COL-05)

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/clientes/{id}/arquivos?pagina=&tamanhoPagina=` | Todos os anexos do cliente, do mais novo: `{ itens: [{ id, nomeArquivo, tipoConteudo, tamanhoBytes, enviadoEm, autor, autorEhCliente, interno, origem: { tipo, id, titulo, numero } }], pagina, tamanhoPagina, total }`. | `solicitacao.ver` ou `tarefa.ver` |

Decidido no marco 16 (14/10):
- **Somente leitura, uma lista só.** Junta os anexos das interações das solicitações do cliente (exige `solicitacao.ver`) e dos comentários das tarefas dele (exige `tarefa.ver`); quem tem só uma das permissões vê só aquela origem.
- **`interno`:** verdadeiro para nota interna e para comentário de tarefa, que o cliente não vê.
- **Download:** o mesmo `GET /anexos/{id}/download`.
- **Portal:** sem aba de arquivos no v1; o cliente vê os anexos dentro de cada solicitação.

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

Decidido na implementação:
- `GET /configuracoes/calendario` e `GET /categorias` liberados a qualquer pessoa da equipe; escrita e `/configuracoes/portal` exigem `configuracao.gerenciar`.
- Erros de calendário vêm no campo (`fusoHorario`, `expediente`: horário fora de `HH:mm`, fim antes do início, dia repetido, semana sem dia útil).
- **Mudar o calendário não recalcula prazos de solicitações já abertas**: eles foram fixados com o calendário da época; só o que vier depois usa o novo.
- `PUT /configuracoes/portal` `{ "nomeExibido": "...", "diasFechamentoAutomatico": 5 }` (1 a 60; guardados no próprio tenant); a resposta traz também `nomeTenant` e `logoUrl`.
- `PUT /configuracoes/portal/logo` — multipart, campo `logo`: PNG, JPEG ou WebP (pelo conteúdo) até 512 KB → `200` com a configuração. Tipo errado → `400` em `errors.logo`; acima do limite → `413`. `DELETE /configuracoes/portal/logo` remove (→ `200`). `logoUrl` é uma URL assinada de 1 hora (o bucket é privado); o portal recebe a mesma em `/portal/eu` (`prestador.logoUrl`). Cada envio usa um arquivo novo e o anterior é apagado.
- Categorias: `POST`/`PATCH` `{ "nome": "...", "ativa": true }` (`ativa` nulo mantém; PATCH reativa), `DELETE` inativa; nome repetido no tenant (sem diferenciar maiúsculas) → `400` em `nome`; categoria inativa não entra em nova solicitação (`400` em `categoriaId`).

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
| GET | `/anexos/{id}/download` | `302` para URL assinada (curta). Interação ou comentário de tarefa (§8.1a). | `solicitacao.ver` ou `tarefa.ver` |

**Filtros do GET `/solicitacoes`:** `?status=ABERTA,EM_ANDAMENTO&clienteId=&responsavelId=&prioridade=&categoriaId=&situacaoSla=EM_RISCO&busca=&abertaDe=&abertaAte=&ordem=-abertaEm`.

Decidido na implementação (6c2):
- `situacaoSla` no filtro aceita `EM_RISCO` e `VIOLADO`, resolvidos no banco pelos instantes persistidos (read side §5); solicitação pausada (AGUARDANDO_CLIENTE) não entra em nenhum dos dois. `ordem`: `abertaEm` (padrão `-abertaEm`), `numero`, `prioridade`, `prazoResolucao`. `busca`: trecho do título ou número (`#1042`).
- `PATCH` recebe título, descrição, prioridade, categoria e responsável (formulário inteiro). Responsável precisa ser da equipe (`400` no campo).
- `PUT /solicitacoes/{id}/contrato` `{ "contratoId": "k9..." | null }` (`solicitacao.editar`, `If-Match` opcional) → `200` com a solicitação. O contrato precisa ser do mesmo cliente e vigente hoje (`422 contrato-incoerente`); resolvida ou final → `422 transicao-invalida`. SLA: com prazo correndo, recalcula pelo início do ciclo com os prazos do novo contrato (mantém pausas); sem SLA antes, começa agora; `null` remove contrato e SLA. Apontamentos já feitos não mudam. Fora do `PATCH` de propósito: o formulário inteiro sem o campo removeria o contrato.
- Transições respondem `200` com a solicitação. `POST /interacoes` aceita JSON `{ "texto": "...", "visibilidade": "EXTERNA" }` (sem anexos) ou multipart (abaixo). Interação EXTERNA da equipe registra a primeira resposta.
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

**POST `/solicitacoes/{id}/interacoes`** — `multipart/form-data`: `texto`, `visibilidade` (`INTERNA`|`EXTERNA`, padrão `INTERNA`), `anexos` (repetido; até 5, 20 MB cada). → `201` com a interação. Também aceita JSON sem anexos.
- O tipo do arquivo é detectado pelo conteúdo (PNG, JPEG, GIF, WebP, PDF, ZIP, Office novo e antigo, texto UTF-8/CSV); o declarado pelo navegador é ignorado. Fora da lista → `422 anexo-invalido`; acima de 20 MB → `413 anexo-grande-demais`.
- Interação (aqui e na linha do tempo) traz `anexos: [{ id, nomeArquivo, tipoConteudo, tamanhoBytes }]`. `GET /anexos/{id}/download` → `302` para URL assinada de 5 minutos, sempre como `attachment`.

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
  "link": "https://acordu.app.br/portal/l/k3Jq...", "emailEnviado": true }
```

Decidido na implementação (6d3):
- `contatoId`: contato ativo do cliente da solicitação (senão `400` no campo). `validadeDias` de 1 a 90, padrão 30. `enviarEmail` nulo envia. Um link por (solicitação, contato): emitir de novo revoga o anterior. Solicitação FECHADA ou CANCELADA, ou cliente inativo → `422`.
- O e-mail sai logo depois do commit, na própria requisição (o token não pode ir para a outbox); falha no envio não desfaz a emissão e volta como `emailEnviado: false`.
- `GET /solicitacoes/{id}/acessos-portal` (`portal.gerenciar_acessos`) → links válidos `[{ acessoId, contatoId, contatoNome, emitidoEm, expiraEm }]`, sem o token. `DELETE .../{acessoId}` → `204`, idempotente; a sessão aberta pelo link para de valer na requisição seguinte.

---

## 8. Tarefas e projetos

### 8.1 Tarefas

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/tarefas` | Lista (`?status=&responsavelId=&solicitacaoId=&projetoId=&clienteId=&prazoAte=&minhas=true`). | `tarefa.ver` |
| POST | `/tarefas` | Cria (avulsa, de solicitação e/ou projeto). | `tarefa.criar` |
| GET | `/tarefas/{id}` | Detalha, com checklist e total apontado. | `tarefa.ver` |
| PATCH | `/tarefas/{id}` | Título, descrição, responsável, prazo, estimativa, `visivelCliente`, `planejadaPara`. | `tarefa.editar` |
| POST | `/tarefas/{id}/inicio` · `/conclusao` · `/reabertura` · `/cancelamento` | Transições. | `tarefa.editar` |
| POST | `/tarefas/{id}/itens` | Adiciona item de checklist. | `tarefa.editar` |
| PATCH/DELETE | `/tarefas/{id}/itens/{itemId}` | Marca/edita/remove item. | `tarefa.editar` |

O quadro (kanban) é o mesmo `GET /tarefas` agrupado no front por `status`.

`planejadaPara` (opcional, decidido em 14/10, marco 13) é o dia em que a tarefa está planejada para ser feita: o plano da semana e a linha do tempo arrastam este dia, nunca o `prazo` combinado. Como o `PATCH` é o formulário inteiro, omitir o campo limpa o dia planejado. Volta em `GET /tarefas` e no detalhe.

```json
// POST /tarefas → 201
{ "titulo": "Refazer arte", "solicitacaoId": "s1...", "projetoId": null,
  "responsavelId": "u2...", "prazo": "2026-10-01", "estimativaMinutos": 120,
  "visivelCliente": true, "planejadaPara": "2026-09-29" }
```

### 8.1a Comentários de tarefa (marco 14a, RF-COL-01)

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/tarefas/{id}/comentarios` | Do mais antigo: `[{ id, autor: { id, nome }, texto, registradoEm, anexos: [{ id, nomeArquivo, tipoConteudo, tamanhoBytes }] }]`. | `tarefa.ver` |
| POST | `/tarefas/{id}/comentarios` | JSON `{ texto }` ou multipart (`texto`, `anexos`) → `201` com o comentário. Texto ou ao menos um anexo; anexos com as regras do §7. | `tarefa.editar` |
| DELETE | `/tarefas/{id}/comentarios/{comentarioId}` | Só o autor → `204` (os demais `403`); os arquivos saem do storage depois. | `tarefa.editar` |

Decidido no marco 14 (14/10): comentário de tarefa é só da equipe (nunca no portal, nem em tarefa visível ao cliente), imutável como as interações, e sem permissão própria: comentar exige `tarefa.editar` (um `tarefa.comentar` mexeria no catálogo e nos papéis já semeados). O download dos anexos é o mesmo `GET /anexos/{id}/download`, que agora aceita `solicitacao.ver` (anexo de interação) ou `tarefa.ver` (anexo de comentário).

**Menções (14b, RF-COL-02):** em comentário de tarefa e em interação **interna** da solicitação, o texto pode citar pessoas como `@{usuarioId}` (o front troca pelo nome na exibição e monta o token na digitação, a partir de `GET /equipe`). Só vale mencionar quem é da equipe e pode ver o alvo (`tarefa.ver` ou `solicitacao.ver`); fora disso, menção em interação EXTERNA ou mais de 20 pessoas → `400` em `errors.texto`. O texto volta como foi gravado, com os tokens.

### 8.1b Seguidores (marco 14c, RF-COL-03)

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/solicitacoes/{id}/seguidores` · `/tarefas/{id}/seguidores` | Quem segue: `[{ id, nome }]`, por nome. | `solicitacao.ver` · `tarefa.ver` |
| PUT | `…/seguidores/eu` | Passa a seguir → `204` (idempotente). | idem |
| DELETE | `…/seguidores/eu` | Deixa de seguir → `204`. | idem |

Decidido no marco 14 (14/10):
- **Quem segue automaticamente:**
  - na solicitação: quem abriu pela equipe, o responsável (e cada novo responsável), quem registra interação e quem é mencionado;
  - na tarefa: quem criou (nenhum, se veio de recorrência), o responsável, quem comenta e quem é mencionado.
  - Só pessoas da equipe; o contato do cliente nunca segue.
- **Deixar de seguir** vale até o próximo gatilho: comentar de novo, ser mencionado ou virar responsável faz seguir outra vez.
- **Avisos:** são os da categoria `SEGUIDAS` (§12b). Os seguidores ficam fora dos agregados, numa tabela própria (`seguidores`), como o sino.

### 8.2 Projetos

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET/POST | `/projetos` | Lista (`?clienteId=&status=`) / cria. | `projeto.ver` / `projeto.gerenciar` |
| GET/PATCH | `/projetos/{id}` | Detalha (com progresso) / edita. | `projeto.ver` / `projeto.gerenciar` |
| POST | `/projetos/{id}/conclusao` · `/cancelamento` | Transições. | `projeto.gerenciar` |

`inicio` (opcional, decidido em 14/10, marco 13) é o início planejado: com ele o projeto vira uma barra `inicio`–`prazo` na linha do tempo; sem ele aparece só no prazo. Não pode ser depois do prazo (`400` em `errors.inicio`). Está no corpo do `POST`/`PATCH`, na lista e no detalhe.

```json
// progresso em GET /projetos/{id}
"progresso": { "tarefasConcluidas": 7, "tarefasTotal": 12,
               "apontadoMinutos": 1510, "estimativaMinutos": 2400 }
```

---

Decidido na implementação (6c3):
- `POST /tarefas` aceita também `clienteId` para tarefa avulsa; com solicitação/projeto, o cliente é herdado (e precisa coincidir). Projeto concluído ou cancelado não recebe tarefas (`400` em `projetoId`). `visivelCliente` sem cliente → `400` no campo.
- `PATCH /tarefas/{id}` e `/projetos/{id}` recebem o formulário inteiro; `PATCH .../itens/{itemId}` recebe `{ "texto": "...", "feito": true }` (`feito` nulo mantém).
- Transições de tarefa e projeto respondem `200` com o recurso; itens respondem com o item.

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

> **Contrato do apontamento (marco 12):** o da origem da tarefa (solicitação, projeto ou recorrência), só se a data cai na vigência dele e ele não está SUSPENSO; senão `contratoId: null` (avulso). Fica congelado no registro.

**POST `/apontamentos`** → `201`
```json
{ "tarefaId": "t1...", "data": "2026-09-30", "duracaoMinutos": 90,
  "descricao": "Ajuste de layout e exportação", "descricaoExterna": "Arte refeita",
  "faturavel": true, "visivelCliente": true }
```
`contratoId` não é enviado: é resolvido pela tarefa e congelado (ADR 0004). Origem do contrato: solicitação, senão projeto, senão recorrência da tarefa; tarefa avulsa → sem contrato (sem período fechado). Editar/excluir de outra pessoa sem `apontamento.editar_todos` → `403`. `POST /cronometro/parada` sem corpo usa o título da tarefa como descrição, `faturavel: true`, data de hoje no fuso do tenant; responde `201` com o apontamento. `GET /cronometro` traz `decorridoMinutos`. Data em período fechado → `422` (`tipo: .../periodo-fechado`). Iniciar cronômetro com outro ativo → `409`.

---

## 10. Tarefas recorrentes

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET/POST | `/recorrencias` | Lista (`?clienteId=&status=`) / cria. | `recorrencia.ver` / `recorrencia.gerenciar` |
| GET/PATCH/DELETE | `/recorrencias/{id}` | Detalha / edita contrato, modelo e regra / encerra (204; fica como histórico). | idem |
| POST | `/recorrencias/{id}/pausa` · `/retomada` | Transições. | `recorrencia.gerenciar` |
| GET | `/recorrencias/{id}/previa` | Próximas datas (`?quantidade=6`, 1–24). | `recorrencia.ver` |

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
- `inicio` (opcional; padrão: hoje no fuso do tenant) é a âncora: nenhuma ocorrência antes dele, e o mês dele define o ciclo de TRIMESTRAL e ANUAL. Início no passado só ancora o ciclo: a primeira ocorrência é a primeira ≥ hoje (sem tarefas retroativas).
- `diaReferencia`: 1–7 em SEMANAL (1 = segunda, 7 = domingo); 1–31 nos demais (dia inexistente no mês vira o último dia).
- `antecedenciaDias`: 0–365.
- `PATCH` recebe `contratoId`, `modelo` e `regra` completos (o cliente não muda; `regra.inicio` ausente mantém o atual). Vale para as próximas tarefas: as já geradas não mudam. Regra diferente recalcula a próxima data a partir de hoje; regra igual não mexe nela. `If-Match` opcional, como nas demais edições.
- Validação: cliente ativo; contrato, se houver, do mesmo cliente e não encerrado; responsável da equipe (checado só quando muda). Erro de campo aninhado vem com o caminho (`regra.diaReferencia`).
- Detalhe: `{ id, clienteId, clienteNome, contratoId, modelo, regra, proximaData, status, versao }`; listagem paginada com `titulo`, `responsavelId`, `periodicidade`, `proximaData` e `status` (ativas primeiro, pela próxima data).
- Prévia: ATIVA a partir da próxima data; PAUSADA, as datas que valeriam se retomasse hoje; ENCERRADA, lista vazia.

- **Geração (RF-REC-03):** no job `tick` (a cada 5 min), com o "hoje" no fuso de cada tenant: assim que `data de referência − antecedência ≤ hoje`, a tarefa é criada copiando o modelo (prazo = data de referência, checklist nova). Cada recorrência com suas tarefas num commit; a próxima data avança junto, e o índice único (recorrência, data) barra duplicata. Cliente inativo: as datas passam sem gerar e sem acumular. Responsável que saiu da equipe: a tarefa nasce sem responsável. O contrato não é olhado na geração: o apontamento usa o contrato da recorrência (RF-APT-03).

`GET .../previa` → `["2026-10-31", "2026-11-30", "2026-12-31", "2027-01-31", "2027-02-28", "2027-03-31"]` (clamp de fim de mês visível para o usuário conferir).

---

## 11. Dashboard e notificações

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/dashboard` | Composição da tela inicial (`?de=&ate=`). | `dashboard.ver` |
| GET | `/notificacoes` | Notificações do usuário (`?naoLidas=true&pagina=&tamanhoPagina=`). | autenticado |
| POST | `/notificacoes/{id}/leitura` · `/notificacoes/leitura` | Marca uma / todas como lidas. | autenticado |
| GET/PUT | `/notificacoes/preferencias` | Quais e-mails receber (RF-NOT-04). | autenticado |

**Notificações (11b).** O sino recebe os mesmos avisos internos dos e-mails (§12b): nova solicitação, atribuição, SLA em risco/violado e consumo de contrato. Só a equipe (contato → 403); cada pessoa vê as suas.
- `GET /notificacoes` → `{ itens: [{ id, categoria, titulo, link, criadaEm, lidaEm }], pagina, tamanhoPagina, total, naoLidas }`, mais recentes primeiro; `categoria`: `NOVA_SOLICITACAO`, `ATRIBUICAO`, `SLA`, `CONSUMO`, `MENCAO`, `SEGUIDAS`; `link` aponta para a solicitação, a tarefa ou o contrato no app.
- `POST /notificacoes/{id}/leitura` e `POST /notificacoes/leitura` (todas) → `204`; de outra pessoa → `404`. Notificações com mais de 90 dias são apagadas pelo job diário.
- `GET/PUT /notificacoes/preferencias` `{ categorias: [{ categoria, email }] }`: liga ou desliga o **e-mail** de uma categoria (no PUT, categoria ausente volta ao padrão: ligada, exceto `SEGUIDAS`, que nasce desligada); o sino continua. E-mails de conta e os enviados ao contato não entram.

**GET `/dashboard`** (`?de=&ate=`, datas no fuso do tenant; padrão: o mês corrente; até 366 dias, senão `400` em `errors.ate`)
```json
{
  "de": "2026-10-01", "ate": "2026-10-31",
  "fila": { "abertas": 14, "emAndamento": 9, "aguardandoCliente": 4, "aguardandoTerceiro": 1, "semResponsavel": 2 },
  "sla": { "emRisco": 3, "violadas": 1, "cumprimentoPeriodo": 93 },
  "horas": { "faturavelMinutos": 9120, "naoFaturavelMinutos": 1340,
             "porCliente": [ { "clienteId": "c1a...", "nome": "Padaria Bom Grão", "minutos": 1035 } ] },
  "contratos": [ { "contratoId": "k9...", "cliente": "Padaria Bom Grão", "tipo": "MENSAL_FIXO",
                   "franquiaMinutos": 1200, "consumidoMinutos": 1035, "percentual": 86 } ],
  "tarefas": { "vencidas": 2, "proximasRecorrentes": [ { "recorrenciaId": "r1...", "titulo": "Fechamento mensal",
               "cliente": "Contábil Silva", "data": "2026-10-31" } ] }
}
```
- **Período** vale para `cumprimentoPeriodo` (resolvidas com SLA cujo `resolvidaEm` cai no período; nulo sem nenhuma) e `horas`. Fila, SLA em aberto (`emRisco`, `violadas`: em aberto, não pausadas), contratos e tarefas são do momento.
- **Por permissão:** bloco nulo sem a permissão de leitura da área — `fila`/`sla`: `solicitacao.ver`; `horas`: `apontamento.ver`; `contratos`: `contrato.ver`; `tarefas`: `tarefa.ver` (e `proximasRecorrentes` nulo sem `recorrencia.ver`).
- `horas.porCliente`: os 10 clientes com mais minutos faturáveis. `contratos`: ATIVOS vigentes hoje, consumo do mês corrente (no banco de horas, o acumulado), maior percentual primeiro. `tarefas.vencidas`: A_FAZER/FAZENDO com prazo antes de hoje; `proximasRecorrentes`: as 5 recorrências ATIVAS com a próxima data mais cedo.

## 11b. Agenda da equipe (marco 13, RF-PLN-03)

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/agenda?de=&ate=&clienteId=&responsavelId=` | Dias do período, equipe e itens com data. Padrão: da segunda-feira desta semana ao domingo da seguinte; até 62 dias (`400 errors.ate`). | qualquer uma de `solicitacao.ver`, `tarefa.ver`, `projeto.ver`, `recorrencia.ver`, `contrato.ver` |

```json
// GET /agenda → 200
{ "de": "2026-10-12", "ate": "2026-10-25", "hoje": "2026-10-14",
  "dias": [ { "data": "2026-10-12", "util": false, "feriado": true }, ... ],
  "equipe": [ { "id": "u1...", "nome": "Ana Lima" } ],
  "itens": [
    { "tipo": "SOLICITACAO", "id": "s1...", "titulo": "Banner", "numero": 128, "inicio": "2026-10-14", "fim": "2026-10-14",
      "situacao": "EM_RISCO", "responsavel": { "id": "u1...", "nome": "Ana Lima" }, "cliente": { "id": "c1...", "nome": "Padaria Bom Grão" } },
    { "tipo": "TAREFA", "id": "t1...", "titulo": "Arte do cardápio", "inicio": "2026-10-15", "fim": "2026-10-15", "prazo": "2026-10-16",
      "situacao": "NO_PRAZO", "estimativaMinutos": 120, "solicitacaoId": "s2...", ... },
    { "tipo": "PROJETO", "inicio": "2026-10-15", "fim": "2026-10-23", ... },
    { "tipo": "RECORRENCIA", "id": "r1...", "inicio": "2026-10-16", "situacao": "PREVISTO", ... },
    { "tipo": "CONTRATO", "id": "k1...", "marco": "CONSUMO_ALTO", "percentual": 86, "situacao": "EM_RISCO", ... } ] }
```

Decidido no marco 13 (14/10):
- Lista única de itens; o agrupamento por pessoa ou por cliente é do front. Cada tipo só vem com a sua permissão de leitura (como os blocos do dashboard).
- **Tipos e datas:**
  - solicitação em aberto com SLA, no dia do prazo de solução (fuso do tenant);
  - tarefa não cancelada no dia planejado ou, sem ele, no prazo (sem os dois fica fora);
  - projeto EM_ANDAMENTO de `inicio` a `prazo` (sem início, só no prazo);
  - recorrência ATIVA, com as próximas ocorrências calculadas pela regra a partir da próxima data;
  - contrato ATIVO, com o marco `FIM_VIGENCIA` no último dia da vigência e, hoje, `CONSUMO_ALTO` (≥ 80%) ou `CONSUMO_ESGOTADO` (≥ 100%) do período corrente.
- **Situação:**
  - solicitação: `NO_PRAZO`, `EM_RISCO` ou `VIOLADO`, pelas regras da lista (read side §5), ou `PAUSADO`;
  - tarefa e projeto: `NO_PRAZO`, `VENCIDO` (prazo antes de hoje) ou `CONCLUIDO`;
  - recorrência e fim de vigência: `PREVISTO`;
  - consumo: `EM_RISCO` (alto) ou `VIOLADO` (esgotado).
- **Atrasados:** se o período inclui hoje, as solicitações violadas e as tarefas e projetos em aberto com data anterior vêm junto, com a data real (o front os mostra na borda esquerda).
- `responsavelId` restringe a solicitações, tarefas e recorrências da pessoa; projetos e contratos, que não têm responsável, ficam de fora. `clienteId` vale para todos.
- Sem paginação: o período curto é o limite.

## 11d. Agenda assinável — ICS (marco 15a, RF-COL-04)

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/agenda/assinatura` | `{ ativa, discreta, criadaEm }` do link da própria pessoa (sem a URL). | qualquer permissão de leitura da agenda (§11b) |
| POST | `/agenda/assinatura` | `{ discreta }` → `201 { url, discreta }`. Gera o link e substitui o anterior; é a **única** vez em que a URL com o token aparece. | idem |
| PATCH | `/agenda/assinatura` | `{ discreta }` → `200` com a situação; troca o modo sem trocar o link (`404` sem link). | idem |
| DELETE | `/agenda/assinatura` | Revoga → `204` (`404` sem link). | idem |
| GET | `/agenda/{token}.ics` | Feed `text/calendar` **sem sessão** (segurança §5.4). `404` para token desconhecido ou revogado, ou para quem saiu da equipe. `Cache-Control: private, max-age=900`. | pelo token |

Decidido no marco 15 (14/10):
- O feed traz só o que é da pessoa, de 30 dias atrás a 90 à frente, e só os tipos que o papel dela vê:
  - tarefas em aberto no dia planejado (ou no prazo), como dia inteiro;
  - solicitações em aberto no instante do prazo de solução, como evento de 30 minutos;
  - próximas ocorrências das recorrências dela, como dia inteiro e "(prevista)".
- Título padrão `{título} · {cliente}`; no modo discreto, "Tarefa", "Prazo de solicitação" ou "Tarefa recorrente", sem descrição. Cada evento leva o link do item no app (`URL`).
- `UID` estável por item (`tarefa-{id}@acordu`, `solicitacao-{id}@acordu`, `recorrencia-{id}-{aaaammdd}@acordu`): o calendário atualiza o evento em vez de duplicar.

---

## 11a. Auditoria

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/auditoria` | Trilha do tenant, mais recentes primeiro (`?de=&ate=&acao=&usuarioId=&alvoId=&pagina=&tamanhoPagina=`). | `auditoria.ver` |

```json
// GET /auditoria?acao=login.sucesso → 200
{ "itens": [ { "id": "01a1...", "ocorridoEm": "2026-10-13T17:02:11Z", "acao": "login.sucesso",
               "usuario": { "id": "u1...", "nome": "Ana" }, "alvoTipo": null, "alvoId": null, "dados": null, "ip": "177.10.2.3" } ],
  "pagina": 1, "tamanhoPagina": 50, "total": 1 }
```
- `de`/`ate`: datas no fuso do tenant, inclusivas. `usuario` nulo: o sistema (job) ou uma sessão de link do portal.
- Ações (`acao`, alvo, `dados`). Login com falha não aparece (fica sem tenant).

| Área | Ações | Alvo | `dados` |
|---|---|---|---|
| Sessão | `login.sucesso` | — | — |
| | `portal.link.troca` | solicitação | `acessoPortalId`, `contatoId` |
| Equipe | `equipe.convite`, `equipe.convite.revogacao` | convite | `email`, `papel` |
| | `equipe.papel.troca` | usuário | `de`, `para` (nomes dos papéis) |
| | `equipe.remocao` | usuário | `papel` |
| | `equipe.capacidade` | usuário | `de`, `para` (minutos por semana) |
| Agenda | `agenda.link.emissao` · `agenda.link.revogacao` | usuário | `discreta` (emissão) |
| Portal | `portal.acesso.concessao`, `portal.acesso.escopo`, `portal.acesso.revogacao` | contato | `clienteId`, `escopo` |
| | `portal.link.emissao`, `portal.link.revogacao` | solicitação | `acessoPortalId`, `contatoId` (emissão: `validadeDias`) |
| Contratos | `contrato.criacao` | contrato | `clienteId`, `tipo`, `termos` (vigência, franquia, SLA) |
| | `contrato.edicao` | contrato | `antes`, `depois` (termos) |
| | `contrato.suspensao`, `contrato.reativacao`, `contrato.encerramento` | contrato | encerramento: `fim` |
| | `contrato.ajuste_saldo` | contrato | `ajusteId`, `quantidadeMinutos`, `motivo`, `data` |
| | `contrato.fechamento_periodo` | contrato | `periodo` |
| Apontamentos | `apontamento.edicao` | apontamento | `antes`, `depois` |
| | `apontamento.exclusao` | apontamento | `antes` |
| Solicitações | `solicitacao.status` | solicitação | `de`, `para` (status interno) |
| | `solicitacao.responsavel` | solicitação | `de`, `para` (ids) |
| | `solicitacao.contrato` | solicitação | `de`, `para` (ids dos contratos) |

- Status e responsável são auditados por qualquer caminho (equipe, portal, retomada pela resposta do contato, fechamento automático); `usuario` nulo no job e na sessão de link. Links emitidos automaticamente nos e-mails ao contato (§12a) não são auditados: é o sistema agindo.
- Registros com mais de 12 meses são expurgados (segurança §8).

---

## 11c. Capacidade da semana (marco 13, RF-PLN-04)

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/capacidade?semana=` | Horas planejadas × capacidade de cada pessoa da equipe numa semana (segunda a domingo). `semana` aceita qualquer data da semana; sem ela, a semana de hoje. | `tarefa.ver` |

```json
// GET /capacidade?semana=2026-10-14 → 200
{ "semana": "2026-10-12", "ate": "2026-10-18",
  "pessoas": [
    { "pessoa": { "id": "u1...", "nome": "Ana Lima" }, "capacidadeMinutos": 2400, "percentual": 77,
      "carga": { "planejadoMinutos": 1860, "tarefas": 6, "tarefasSemEstimativa": 1, "solicitacoes": 2,
                 "porDia": [ { "data": "2026-10-12", "planejadoMinutos": 0 }, ... ] } } ],
  "semResponsavel": { "planejadoMinutos": 60, "tarefas": 1, "tarefasSemEstimativa": 0, "solicitacoes": 1, "porDia": [ ... ] } }
```

Decidido no marco 13 (14/10):
- **Planejado:** soma das estimativas das tarefas em aberto (A_FAZER e FAZENDO) da pessoa, cada uma no dia planejado ou, sem ele, no prazo dentro da semana.
- **O que conta só em quantidade:**
  - tarefa sem estimativa (`tarefasSemEstimativa`);
  - solicitação, que não tem estimativa: `solicitacoes` conta as em aberto com prazo de solução na semana e vem nulo sem `solicitacao.ver`.
- **O que não entra:**
  - tarefas atrasadas de semanas anteriores (a agenda as mostra);
  - tarefas concluídas;
  - recorrências ainda não geradas.
- **`percentual`:** planejado sobre capacidade, nulo com capacidade zero. Acima de 100 indica sobrecarga.
- **`semResponsavel`:** reúne as tarefas e solicitações sem responsável, ou cujo responsável saiu da equipe.

---

## 11e. Tempo real — SSE (marco 15b, RF-COL-04, ADR 0009)

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/eventos` | `text/event-stream` com os sinais de mudança do tenant. | equipe (qualquer papel interno; contato `403`) |

```
retry: 5000

event: mudanca
data: {"tipo":"tarefa","id":"t1..."}

: vivo
```

Decidido no marco 15 (14/10):
- O front lê com `fetch` (que envia o `Authorization`), não com `EventSource`.
- **Só sinais:** `tipo` ∈ `solicitacao`, `tarefa`, `projeto`, `recorrencia`, `contrato`, `cliente`, mais o `id`. O front invalida as consultas daquele item (e as listas e agenda que o contêm) e busca de novo pela API.
- **Origem:** toda gravação de caso de uso, depois do commit. Interação e comentário chegam como o item onde aparecem, e apontamento chega como a tarefa.
- **Não sinalizam:** sino, seguidores e preferências.
- **Comentário `: vivo` a cada 25 s.** Conexão lenta pode perder sinal; o front revalida ao focar a janela.

---

## 11f. Feed de atividade (marco 16a, RF-COL-05)

| Método | Rota | Descrição | Permissão |
|---|---|---|---|
| GET | `/atividades?clienteId=&pessoaId=&antes=` | Do mais recente, 50 por página; `proximo` é o cursor (`antes`) da página seguinte, nulo no fim. | `solicitacao.ver` ou `tarefa.ver` |

```json
// GET /atividades → 200
{ "itens": [
    { "id": "a1...", "tipo": "TAREFA_STATUS", "ocorridaEm": "2026-10-14T18:02:11Z",
      "autor": { "id": "u1...", "nome": "Ana Lima" }, "autorEhCliente": false,
      "alvo": { "tipo": "TAREFA", "id": "t1...", "titulo": "Arte do banner", "numero": null },
      "cliente": { "id": "c1...", "nome": "Padaria Bom Grão" },
      "detalhe": { "statusDe": "A_FAZER", "statusPara": "FAZENDO", "responsavel": null } } ],
  "proximo": 1234 }
```

Decidido no marco 16 (14/10):
- **Tipos:** `SOLICITACAO_ABERTA`, `SOLICITACAO_ATRIBUIDA`, `SOLICITACAO_STATUS`, `RESPOSTA_DO_CLIENTE`, `RESPOSTA_AO_CLIENTE`, `NOTA_INTERNA`, `TAREFA_CRIADA`, `TAREFA_ATRIBUIDA`, `TAREFA_STATUS` e `COMENTARIO_TAREFA`. O front monta a frase; o `alvo` traz o título **atual**. Apontamentos não entram.
- **Autor:** `autor` nulo = sistema (job, recorrência); `autorEhCliente` marca o contato.
- **Filtros:** `pessoaId` filtra pelo autor da equipe; `clienteId` alimenta "Últimos movimentos" na ficha do cliente.
- **Visibilidade:** cada linha só aparece para quem pode ver o alvo (solicitação ou tarefa). Alvo que não aparece mais pelos filtros some do feed. O portal não acessa.
- **Gravação e retenção:** cada linha é gravada depois do commit, uma por mensagem da outbox (`RegistroDeAtividades`). O job `expurgo-notificacoes` apaga também as atividades com mais de 90 dias (`atividadesRemovidas` na resposta).

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

Decidido na implementação (6d2):
- **Sessão de link (6d3):** rotas sem ✓L → `403`; outra solicitação → `404`; acesso revogado ou expirado → `401` na requisição seguinte. `/portal/eu` traz `solicitacaoId` (nulo na sessão com conta). Resposta pelo link tem como autor o contato (nome do contato nas interações). `POST /portal/solicitacoes` tem rate limit por usuário (`RateLimit__Portal__PorMinuto`, padrão 10).
- **Sessão:** a cada requisição do portal, o contato e o escopo são lidos do banco (contato ativo, de cliente ativo, com vínculo). Acesso revogado → `401 nao-autenticado` já na requisição seguinte; mudança de escopo vale na hora nas rotas do portal (o token só é atualizado no próximo refresh).
- **Escopo PROPRIAS:** solicitações em que o contato é o contato da solicitação (abrir pelo portal grava o contato logado, então "as que abriu" é a mesma regra). Fora do escopo → `404`. `/portal/contratos` e o extrato → `403` (`veContratos` em `/portal/eu` diz se a tela mostra).
- `GET /portal/eu` → `{ contato: { id, nome, email }, cliente: { id, nome }, prestador: { nome }, escopoPortal, veContratos }`.
- `GET /portal/solicitacoes` → página de `{ id, numero, titulo, statusExterno, abertaEm, podeAprovar }`, mais recentes primeiro; `statusExterno` aceita lista separada por vírgula.
- `POST /portal/solicitacoes/{id}/interacoes` aceita JSON `{ texto }` ou multipart (`texto`, `anexos`), com as mesmas regras de anexo da equipe e rate limit do portal. Interações do portal trazem `anexos`; `GET /portal/anexos/{id}/download` só serve anexo de interação EXTERNA de solicitação visível ao contato (escopo e link), senão 404.
- `POST /portal/solicitacoes` `{ titulo, descricao?, prioridade? }` em JSON, ou multipart com os mesmos campos e `anexos` → `201` com o detalhe. Os anexos viram a primeira interação EXTERNA, do contato, sem texto; anexo inválido → nada é criado. Origem PORTAL, prioridade padrão MEDIA, contrato = o único ATIVO vigente do cliente (senão sem SLA).
- Detalhe: inclui `descricao`; `sla` nulo sem contrato; `podeAprovar` só em RESOLVIDA e com a permissão.
- `GET .../interacoes` → `[{ id, texto, autorNome, daEquipe, registradaEm }]` (só EXTERNAS). `POST` `{ texto }` → `201` com a interação; em AGUARDANDO_CLIENTE retoma a solicitação.
- `aprovacao` e `reprovacao` `{ motivo }` → `200` com o detalhe; fora de RESOLVIDA → `422 transicao-invalida`; motivo vazio → `400`.
- `GET /portal/contratos` → contratos ATIVOS vigentes hoje `[{ id, tipo, inicio, fim, franquiaMinutos }]`. Extrato sem `periodo` usa o mês corrente no fuso do tenant; `saldoMinutos` e `percentual` seguem o read side §3 (nulos sem franquia).

---

## 12a. E-mails ao contato (RF-NOT-01, 7c1)

Sem rota própria: saem depois do commit quando a solicitação tem contato ativo (de cliente ativo).

| Quando | Assunto |
|---|---|
| Aberta (qualquer origem) | "Recebemos sua solicitação #N" (protocolo) |
| Mensagem EXTERNA da equipe | "Nova mensagem na solicitação #N" (com o texto) |
| Status vira AGUARDANDO_CLIENTE | "Precisamos de você na solicitação #N" |
| Resolvida | "Solicitação #N resolvida: confira e aprove" (com o prazo do fechamento automático) |

- Remetente com o nome do prestador. A resposta do próprio contato não gera e-mail para ele.
- Link: contato com conta → `{App__UrlPublica}/portal/solicitacoes/{id}`; sem conta → link de acesso sem senha (30 dias) **novo a cada e-mail**, revogando o anterior da mesma solicitação e contato. Solicitação fechada ou cancelada não ganha link novo.

## 12b. E-mails internos (RF-NOT-02, 7c2)

| Quando | Para quem |
|---|---|
| Solicitação aberta (equipe ou portal) | Responsável; sem ele, quem tem `solicitacao.editar`. Nunca quem abriu. |
| Responsável alterado | O novo responsável, se não atribuiu a si mesmo. |
| Menção (`MENCAO`, 14b) em interação interna ou comentário de tarefa | Cada pessoa mencionada que ainda é da equipe (nunca o autor). Sino com "{autor} mencionou você na solicitação #N …" ou "na tarefa …"; e-mail com um trecho de até 280 caracteres em que as menções viram `@Nome`. Um aviso por (interação ou comentário, pessoa). |
| Novidade no que a pessoa segue (`SEGUIDAS`, 14c): interação na solicitação, comentário na tarefa, mudança de status | Quem segue o item e ainda pode vê-lo, menos quem causou o evento e quem foi mencionado no mesmo texto (recebe a `MENCAO`). E-mail desligado por padrão; o sino sempre. |
| SLA em risco (75% do prazo útil) ou violado | Responsável; sem ele, quem tem `solicitacao.editar`. Verificado no `tick`; situação = a pior entre primeira resposta e resolução (read side §5); um alerta por (solicitação, ciclo do SLA, situação); reabrir inicia novo ciclo. |

O `tick` responde também `alertasSla` (solicitações alertadas), `tarefasRecorrentes` (tarefas geradas, §10) e `falhas` (tenants com erro em algum dos passos, logados).

**Consumo de contrato (RF-CTR-07, 7c3):** depois de cada apontamento (registro, edição, exclusão) ou ajuste de saldo, se o contrato cruzar **80%** ou **100%** para cima, quem tem `contrato.gerenciar` recebe "Contrato de {cliente} atingiu N%…", com consumido, franquia e saldo. Um aviso por (contrato, período, limiar); cair abaixo rearma. Período: o mês no MENSAL_FIXO; o contrato inteiro (acumulado até hoje) no BANCO_HORAS; AVULSO e sem franquia não avisam. Limiares fixos no v1 (configuráveis depois).

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

Tipos (`type`) do v1: `validacao` (com `errors` por campo), `transicao-invalida`, `periodo-fechado`, `contrato-incoerente`, `anexo-invalido` (422: tipo não permitido, vazio, mais de 5), `anexo-grande-demais` (413), `conflito-versao`, `cronometro-ativo`, `nao-encontrado`, `sem-permissao`, `nao-autenticado` (401), `limite-requisicoes` (429), `erro-interno` (500).

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
