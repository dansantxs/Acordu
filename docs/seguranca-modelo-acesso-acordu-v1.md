# Acordu — Segurança e Modelo de Acesso (v1)

| | |
|---|---|
| **Versão** | 0.1 |
| **Data** | 06/10/2026 |
| **Escopo** | Identidade, autenticação, autorização (RBAC + escopo por cliente), sessões do portal, proteção de dados, anexos e auditoria. |
| **Base** | ADR 0001 (identidade e multiusuário), ADR 0002 (portal) e ADR 0007 (transporte da sessão). |
| **Natureza** | Especificação. Decisões e contratos, **sem implementação**. |

---

## 1. Princípios

- **Dois isolamentos:** entre tenants (prestadores) e entre clientes do mesmo tenant. Ambos impostos na consulta, não só na tela.
- **Seguro por omissão:** item novo nasce **interno**; tornar visível ao cliente é sempre uma ação explícita.
- **Autenticação ≠ autorização.** Autorização por permissão, na Application, por caso de uso.
- **Least privilege**, inclusive para o próprio contato do cliente.
- **Sessão de link é a mais restrita do sistema:** uma solicitação, sem extrato, sem listagem.

---

## 2. Modelo de identidade e acesso

Modelo base do ADR 0001 (`Usuario` global gerido pelo Identity, `Tenant`, `Vinculo` N-N, `Papel` como conjunto de `Permissao`), com a extensão do ADR 0002:

```plantuml
@startuml
hide empty members
skinparam classAttributeIconSize 0
class Usuario <<identity>>
class Tenant <<aggregate root>>
class Vinculo <<membership>> {
  +UsuarioId
  +TenantId
  +PapelId
  +ClienteId: Guid?
  +EscopoPortal: EscopoPortal?
  +CapacidadeSemanal: Duracao?  // só equipe (planejamento)
}
class Papel <<aggregate root>> {
  +TenantId, Nome, Padrao
  +Tipo: TipoPapel  // INTERNO | CLIENTE
}
class Permissao <<value object>> { +Chave }
class Contato <<entity>> { +UsuarioId? }
class AcessoPortal <<aggregate root>> { +SolicitacaoId, +ContatoId, +TokenHash }

Usuario "1" -- "*" Vinculo
Tenant "1" -- "*" Vinculo
Papel "1" -- "*" Vinculo
Papel "*" -- "*" Permissao
Contato "0..1" -- "1" Usuario
AcessoPortal "*" -- "1" Contato
@enduml
```

- **Papel do tipo CLIENTE** só pode conter as permissões **do portal** (§3.1); papel INTERNO, só as **internas**. A divisão é pelo catálogo, não pelo prefixo: `portal.gerenciar_acessos` é interna. Garantido no domínio de `Papel`.
- `Vinculo.ClienteId` e `EscopoPortal`: obrigatórios quando o papel é CLIENTE, proibidos nos demais.
- Um mesmo login pode ser Membro no próprio tenant e Cliente em outro (ex.: um freelancer que também contrata uma agência). A troca de tenant ativo (ADR 0001) resolve.

---

## 3. Autorização — RBAC

### 3.1 Catálogo de permissões (v1)

**Internas**
```
cliente.ver | cliente.gerenciar
contrato.ver | contrato.gerenciar | contrato.ajustar_saldo | contrato.fechar_periodo
solicitacao.ver | solicitacao.criar | solicitacao.editar | solicitacao.comentar
tarefa.ver | tarefa.criar | tarefa.editar
projeto.ver | projeto.gerenciar
apontamento.ver | apontamento.criar | apontamento.editar_todos
recorrencia.ver | recorrencia.gerenciar
dashboard.ver
configuracao.gerenciar
portal.gerenciar_acessos
usuario.convidar | usuario.remover
papel.gerenciar
auditoria.ver
```

**Do portal** (só em papel CLIENTE)
```
portal.solicitacao.ver | portal.solicitacao.criar | portal.solicitacao.comentar
portal.solicitacao.aprovar
portal.extrato.ver
```

### 3.2 Papéis padrão (semeados em cada tenant)

| Papel | Tipo | Permissões |
|---|---|---|
| **Dono** | INTERNO | Todas as internas. |
| **Membro** | INTERNO | Todas as internas **exceto**: `contrato.gerenciar`, `contrato.ajustar_saldo`, `contrato.fechar_periodo`, `apontamento.editar_todos`, `configuracao.gerenciar`, `usuario.*`, `papel.gerenciar`, `auditoria.ver`. |
| **Leitor** | INTERNO | Todas as `*.ver` internas **exceto `auditoria.ver`** + `dashboard.ver`. (Auditoria é dado de gestão: fica com o Dono e papéis customizados.) |
| **Cliente** | CLIENTE | Todas as `portal.*`. |

Efeito do `EscopoPortal` sobre o papel Cliente (aplicado na Application, além das permissões):

| Escopo | Solicitações visíveis | Extrato |
|---|---|---|
| `TODAS_DO_CLIENTE` | Todas do cliente | Sim |
| `PROPRIAS` | Só as que ele abriu ou em que é o contato (abrir pelo portal grava o contato logado: é a mesma regra) | **Não** (o extrato expõe pedidos de colegas); nem a lista de contratos |

### 3.3 Guardas (invariantes)
- **≥ 1 Dono por tenant**.
- Papel padrão não é excluível; papel em uso não é excluível sem realocação.
- Não é possível atribuir papel CLIENTE por `/convites` nem papel INTERNO pelo convite de contato.
- Revogar acesso de contato remove o `Vinculo` e **revoga todos os refresh tokens e links** dele naquele tenant.
- Inativar cliente revoga os acessos de todos os seus contatos.
- **Empresa só leitura** (RF-EMP-02, F14b, ADR 0015): em VENCIDA ou CANCELADA, a guarda de gravação do `AcorduDbContext`, ao lado da de isolamento, recusa incluir, alterar ou remover o que é da empresa (`422 empresa-somente-leitura`). Como roda no `SaveChanges`, a ordem é permissão (`403`), `404`, regras do caso de uso e só então a situação, sem código em cada caso de uso; nada é gravado. A situação é lida do banco uma vez por escopo, não do token, e vale pelo tenant da entidade gravada: quem tem vínculo em outra empresa grava lá. Toda entidade com `TenantId` é classificada (`ClassificacaoSomenteLeitura`, conferida por teste) como da empresa ou da pessoa ou rastro; tipo novo sem classificação nasce bloqueado. Da pessoa ou rastro: sino, preferências de aviso, link da agenda, seguidores, refresh tokens, trilha de auditoria, feed de atividade e outbox. Gravações fora do change tracker que seguem no só leitura: marcar o sino, seguir, liberar o envio na confirmação do e-mail, a sugestão (global), os expurgos e a marca dos avisos de situação (F14j, condicionada à versão da linha). Os jobs por tenant percorrem só as empresas graváveis; a outbox descarta as mensagens de empresa só leitura e os avisos automáticos não saem dela (F14d), mas os e-mails da conta (confirmação, redefinição de senha) e os avisos de situação aos Donos (antes do fim do teste, no vencimento e antes do expurgo; F14j, RF-EMP-04), que não passam pela outbox, continuam saindo: os avisos vão só aos Donos e, antes da liberação do envio, só ao criador (RF-AUT-06). Na superfície do contato (`/portal/*`, `/auth/portal/*`; F14c), a recusa sai como `422 portal-indisponivel` com mensagem neutra ("fale com {prestador}"), escolhida pela rota, para o cliente do prestador não ler a situação comercial dele; `/portal/eu` só diz `envioDisponivel`. Aceitar convite antigo (equipe ou portal) é gravação da empresa: o `Vinculo` é barrado e a transação inteira volta, sem conta nova nem consumo do convite. A troca do link de acesso só grava trilha e passa; a sessão de link só lê.
- **Efeitos automáticos não pedem a permissão do alvo** (modelo §3.1, F11): a permissão é checada na ação que a pessoa faz, e as transições que o sistema deriva dela valem sem checagem própria. Hoje: criar tarefa (`tarefa.criar`) ou iniciar o cronômetro (`apontamento.criar`) tira a solicitação de ABERTA sem `solicitacao.editar`; iniciar o cronômetro põe a tarefa A_FAZER em FAZENDO sem `tarefa.editar`; a primeira resposta EXTERNA (`solicitacao.comentar`) faz o mesmo na solicitação. Ação conjunta escolhida pela pessoa (concluir ou cancelar tarefas junto) não é efeito automático: pede as duas permissões.

---

## 4. Autenticação

ASP.NET Core Identity (ADR 0001: confirmação de e-mail, lockout, política de senha, recuperação por e-mail via Resend).

**(Acordu, 7b, decidido em 09/10):**
- **Login não exige e-mail confirmado:** bloquear trava quem está experimentando o produto; quem registrou com o e-mail de outra pessoa perde a conta para o dono real pela redefinição de senha. A SPA avisa pelo `emailConfirmado` do `/auth/eu`. Conta criada por convite já nasce confirmada.
- **Tokens próprios** (`tokens_conta`), não os do Identity: 32 bytes, só o SHA-256 no banco, uso único, um novo pedido revoga os anteriores da mesma finalidade; confirmação 48 h, redefinição 1 h. Sem dependência das chaves do DataProtection (que se perderiam a cada recriação do container).
- E-mails da conta saem na própria requisição, depois do commit (são pedidos do próprio usuário, repetíveis e sem tenant para a outbox); falha no envio vai para o log.
- `senha/esqueci` não revela se o e-mail tem conta. Redefinir revoga todos os refresh tokens do usuário e zera o bloqueio.

**(Acordu, proteção contra cadastro em massa, decidido em 04/11; RF-AUT-06, RF-AUT-07, RNF-SEG-10):** uma pessoa com vários e-mails podia criar empresas sem limite e usar convites e links para mandar e-mail a qualquer endereço com o domínio do Acordu, estragando a reputação de envio de que os clientes reais dependem.
- **Envio a terceiros só com e-mail confirmado:** enquanto o criador da empresa não confirmar o e-mail, a empresa não envia nada a outra pessoa (convites, link do portal, avisos). O login continua sem exigir confirmação. A empresa guarda o criador e a data da liberação (`Tenant.EnvioLiberadoEm`), gravada na primeira confirmação, pelo link ou pela redefinição de senha, e permanente (spec do F9, decisão 2). Convites e links recusados respondem `422 email-nao-confirmado`, depois da checagem de permissão.
- **Turnstile + limite de cadastro:** desafio do Cloudflare no formulário, conferido pela API, e limite próprio de cadastros por IP.
- **Expurgo:** cadastro sem confirmação em 7 dias e sem dados é apagado pelo job diário. Como o teste nunca foi usado, a linha do registro do documento criada por ele sai junto, e o documento pode abrir outro teste; a liberação que ele tenha usado não volta (decisão do Daniel, 18/11) (decisão 16 do G2 do F14, F14-104).
- **Expurgo da empresa só leitura (RF-EMP-03, F14i; design do F14, D6):** a empresa VENCIDA ou CANCELADA há 30 dias (`Empresas__DiasAteExpurgo`) é apagada pelo diário: todas as tabelas com `TenantId` (dados, vínculos, convites, acessos ao portal, sessões, trilha, feed, sino), os anexos e o logotipo no storage (pelas chaves lidas antes de apagar, depois do commit; a falha fica no log) e as contas sem presença em outra empresa (vínculo, sessão, contato ou empresa que ela criou). Sugestões ficam sem a empresa e a pessoa; o registro do documento fica, só com o HMAC. A remoção é `ExecuteDelete` sem filtros e fora das guardas, num ponto revisado (`ApagadorDeEmpresa`, o mesmo do cadastro abandonado), uma empresa por transação serializável com a situação repetida no `DELETE`. A sessão aberta cai: o refresh token saiu com a empresa (`401`) e o `/auth/eu` não acha mais o vínculo (`401`). A trilha da Nebrys (sem tenant, ADR 0014) não é apagada pelo expurgo.
- **Um teste por documento (RF-AUT-09, F14g; design do F14, D8 e decisão 8 do G2):** o cadastro exige CPF ou CNPJ, e um documento que já teve teste em outra empresa, existente ou expurgada, é recusado com a orientação de falar com a Nebrys (`contato@acordu.app.br`), salvo uma liberação da Nebrys ainda não usada (vale para um cadastro só).
  - O documento em claro fica só na empresa (`Tenant.Documento`), enquanto ela existe. O registro global (`documentos_teste`) guarda só o **HMAC-SHA256 do documento normalizado**, sem referência à empresa nem à pessoa, com a data do primeiro teste e a da liberação. SHA-256 puro de um CPF se desfaz por força bruta (10⁹ combinações); com a chave fora do banco, o hash não identifica ninguém sozinho.
  - Chave em segredo (`Documentos__ChaveHmac`, ≥ 32 bytes, secret `API_DOCUMENTOS_CHAVE`; a API não sobe sem ela, e o `deploy.yml` para antes de publicar). Em desenvolvimento e nos testes há uma chave fixa. **A chave não gira:** os documentos originais não ficam, então não há como recalcular os hashes; trocar ou perder a chave zera a regra para os documentos das empresas já expurgadas (as que existem ainda têm o documento, mas o registro antigo deixa de casar).
  - A liberação (área da Nebrys, F14l) calcula o HMAC do documento digitado e não o guarda; a trilha registra a ação sem o documento.
  - O que a recusa revela: que aquele CPF ou CNPJ já teve teste no Acordu (não quando nem em que empresa). A enumeração fica limitada pelo Turnstile e pelo limite de 3 cadastros por hora por IP, que contam as tentativas recusadas.
- Fora, por ora: lista de domínios descartáveis (entra no F14p) e período de teste atrelado a cartão (entra com a cobrança, v3).

Fluxos específicos:
- **Registro:** cria `Usuario` + `Tenant` (o prestador, com o documento) + `Vinculo` Dono + `CalendarioUtil` padrão + papéis padrão, e cria ou usa o registro global do documento, na mesma transação.
- **Convite para a equipe (6e1):** equipe com `usuario.convidar` convida um e-mail com um papel INTERNO → e-mail com link de aceite → `POST /auth/convite` cria a conta (ou confere a senha da existente) e o `Vinculo` com o papel do convite. Mesmas regras de token do convite do portal; o convite é entidade própria (`ConviteEquipe`), não um vínculo pendente. Quem convida lista os papéis (`GET /papeis`, 22/10) para escolher; criar, editar e excluir papel e ver o catálogo continuam só com `papel.gerenciar`. Convidar com o papel Dono exige também `papel.gerenciar` (23/10): sem ela, quem convida não entrega todas as permissões.
- **Convite de contato para o portal:** equipe convida → e-mail com link de aceite → contato define senha (ou, se já tem conta, confirma com a senha dela) → `Vinculo` com papel Cliente, `ClienteId` e escopo; `Contato.UsuarioId` preenchido.
  - Token do convite como o do link (32 bytes, base64url, só o SHA-256 no banco), **gerado no envio do e-mail**, nunca em claro na outbox. Validade de 7 dias a partir do envio; reconvidar ou revogar invalida.
  - Conta existente exige a senha dela: o token prova acesso ao e-mail, mas não autoriza adotar uma conta que pode ser da equipe de outro prestador. A falha conta para o bloqueio.
  - Conta criada no aceite já nasce com e-mail confirmado (o token chegou por ele).
  - E-mail que já tem vínculo no tenant (equipe ou contato de outro cliente) é recusado no convite e de novo no aceite: um vínculo por usuário e tenant.
- **Link de acesso por solicitação** (sem conta): ver §5.3.

---

## 5. Sessões e tokens

### 5.1 Equipe e contatos com conta
Base (ADR 0001): access token JWT curto (30–60 min) + refresh token persistido e revogável. **Transporte (ADR 0007):** access token no corpo e em memória no SPA, enviado por `Bearer`; refresh só em cookie HttpOnly, Secure, SameSite=Strict, restrito a `/api/v1/auth`, com rotação e detecção de reuso. Claims: `sub`, `tenant_id`, `papel`, `permissoes`; **(Acordu)** para Cliente, também `cliente_id` e `escopo_portal`.
- **(Acordu) Portal:** a cada requisição em `/api/v1/portal`, a Api confere no banco que o usuário ainda é contato ativo do cliente, com vínculo, e usa o escopo do vínculo (não o da claim). Revogação vale na requisição seguinte, sem esperar o access token expirar; uma consulta indexada por requisição.

### 5.2 Troca de tenant
Um usuário Cliente em um tenant e Membro em outro troca de tenant e recebe token com o papel correspondente.

### 5.3 Sessão de link (RF-POR-08)
- Token do link: 32 bytes aleatórios (CSPRNG), base64url; no banco, **só o SHA-256**.
- Validade padrão 30 dias; revogável; um link por (solicitação, contato), reemitir revoga o anterior.
- `POST /auth/portal/link` troca o token por um **access token de 60 min, sem refresh**. Claims: `tenant_id`, `cliente_id`, `acesso_portal_id`, `solicitacao_id`, `tipo = LINK_SOLICITACAO`, permissões fixas (`portal.solicitacao.ver`, `.comentar`, `.aprovar`).
- A cada requisição, o middleware confere se o `AcessoPortal` continua válido (não revogado nem expirado) — revogação vale imediatamente, sem esperar o token expirar.
- Se o contato ganhar conta depois, o link continua funcionando até expirar.
- **(Implementação)** A conferência por requisição lê o `AcessoPortal` (não revogado, não expirado) e o contato dele (ativo, de cliente ativo). Sem `sub` no token: interação registrada pelo link tem como autor o Id do contato. O e-mail do link sai logo após o commit, na requisição (o link também volta na resposta da emissão), nunca pela outbox.

### 5.4 Link da agenda assinável (RF-COL-04, marco 15a)
- **O que é:** um link secreto por pessoa da equipe e tenant, para Google, Outlook e afins, que buscam a URL sem login: `GET /api/v1/agenda/{token}.ics`.
- **Token:** gerado como o da sessão de link (32 bytes de CSPRNG em base64url), com só o SHA-256 no banco (`assinaturas_agenda`).
  - Emitir de novo troca o token e o anterior para de funcionar.
  - A pessoa pode revogar, e emissão e revogação vão para a auditoria (`agenda.link.emissao`, `agenda.link.revogacao`).
- **Somente leitura:** o feed não abre sessão nem dá acesso a nenhuma outra rota.
  - Mostra só os itens da própria pessoa e só os tipos que o papel atual dela pode ver; a checagem é refeita a cada busca, e quem saiu da equipe recebe `404`.
  - Token desconhecido ou revogado também dá `404`, sem distinguir.
  - Limite de 30 buscas por minuto por IP (`RateLimit__Agenda__PorMinuto`).
- **Risco aceito:** o conteúdo vai para um calendário de terceiro. O padrão leva título e cliente, e na descrição só o prazo e o link para o Acordu, nunca o texto de interações.
- **Modo discreto:** opcional, com títulos genéricos ("Tarefa", "Prazo de solicitação"), sem cliente e sem descrição. Pode ser trocado sem trocar o link.

---

## 6. Proteção de dados

- **Em trânsito:** HTTPS obrigatório (Cloudflare na borda do SPA e da API; Google entre o Worker e o Cloud Run; TLS até o Neon), HSTS.
- **Em repouso:** criptografia gerenciada pelos provedores (Neon, R2). Dados hospedados nos EUA (ADR 0010): a transferência internacional consta na política de privacidade.
- **Política de privacidade e termos de uso (F14f, RF-AUT-11, RNF-PRIV-03):** páginas públicas `/privacidade` e `/termos` do SPA, com versão e data de vigência (convenções §4), ligadas no entrar e nas telas de acesso, no aceite de convite do portal e no pé do portal (só a política, para o contato). A política diz os papéis (Nebrys controladora dos dados de cadastro e conta; prestador controlador e Nebrys operadora dos dados dos clientes dele), os fornecedores e a hospedagem nos EUA, e a retenção: expurgo da empresa só leitura em 30 dias e do cadastro abandonado em 7 (RF-EMP-03, RF-AUT-07), só o hash do documento depois do expurgo, backups de 14 dias, trilha de auditoria de 12 meses, inclusive a da Nebrys (ADR 0008, ADR 0014), e avisos e atividade de 90 dias. Canal do titular: `contato@acordu.app.br`. Prazo ou fornecedor novo que trata dado pessoal muda a política (e a versão) no mesmo PR. **Aceite (F14h, D10 e D16 do F14):** só com os textos publicados (`TextosLegais__Publicados`, a mesma variável que mostra as páginas), o cadastro e o aceite de convite da equipe exigem o aceite dos dois textos na versão vigente, e a conta guarda a versão de cada um e a data (tabela global `aceites_textos`, apagada com a conta); versão nova pede "ciente", sem bloquear o uso. Com os textos não publicados, nada de aceite é pedido nem gravado. O contato do portal nunca aceita nem marca "ciente" (a relação dele é com o prestador).
- **Coluna:** sem criptografia de coluna no v1 (textos precisam de busca full-text).
- **LGPD:** o prestador é **controlador** dos dados dos seus clientes; o Acordu é **operador**. Exportação e exclusão por tenant sob solicitação; exclusão de contato anonimiza nome/e-mail nas interações, preservando o histórico do trabalho.
- **Nada de dado de cliente em log e no Discord:** só identificadores e status. O erro inesperado vai ao log com a rota modelo (`/api/v1/tarefas/{id}`), o método e os ids de tenant e usuário; o caminho com valores, a query, o corpo e os cabeçalhos ficam de fora (F13a, RNF-OPS-01). No Discord, o erro vai pela impressão digital (tipo, quadro do código, rota modelo e versão), sem a mensagem da exceção (F13b). Os webhooks são segredo: só em variável de ambiente, e o log do HttpClient do Discord fica desligado porque registraria a URL.
- **Exceção, sugestões (F13e, decisão 4 da spec do F13):** o texto da sugestão vai a #sugestoes com o número, a tela e o nome da empresa (a empresa é cliente da Nebrys, não dado do prestador), nunca com o nome ou o e-mail da pessoa. A janela avisa para não escrever dados de clientes. A sugestão fica fora dos tenants, e o expurgo de um cadastro ou de uma empresa só leitura tira dela a empresa e a pessoa; a mensagem que já saiu em #sugestoes (nome da empresa e texto) fica no Discord, e num MEI o nome da empresa pode ser o da pessoa.
- **App instalável (F12f):** o service worker só guarda a página "Sem conexão" e o ícone; nenhuma resposta da API nem página do app fica no aparelho, então um computador compartilhado não guarda dados de outra empresa ou cliente.
- **Notificação do navegador (F12c):** a escolha (por empresa e pessoa, para quem entra depois no mesmo computador não herdar), as marcas de "já mostrada" e o intervalo do lembrete do cronômetro (F12d) ficam no `localStorage` do navegador, só com ids e números; o texto da notificação é o título do aviso do sino, que a pessoa já vê. Desligar no Acordu não revoga a permissão do navegador (ela é do site); a pessoa revoga nas configurações do navegador.

---

## 7. Anexos

- Upload via API (a API faz streaming para o object storage); nunca upload direto do navegador ao bucket no v1.
- Limites: 20 MB por arquivo, 5 por interação; tipos permitidos por lista (imagens, PDF, documentos Office, texto, zip). Conferência pelo conteúdo (*magic bytes*), não só pela extensão.
- Chave no bucket: `{tenantId}/{solicitacaoId}/{anexoId}` — sem nome original na chave.
- **Download:** a API verifica permissão (e visibilidade EXTERNA, no portal) e devolve (`200 { url }`) uma URL pré-assinada de **5 minutos**, que o navegador abre; o bucket recebe a requisição sem o token da API. O bucket é privado.
- `Content-Disposition: attachment` sempre (evita executar HTML/SVG no navegador).
- Antivírus fora do v1 (registrado como risco; avaliar ClamAV em container quando houver clientes pagantes).
- **(Implementação, 10a)** `AWSSDK.S3` no endpoint S3 configurado (hoje o R2, região `auto`; path style, checksums só quando exigidos, upload sem `aws-chunked`). Arquivos validados (quantidade, tamanho, tipo pelo conteúdo) **antes** de enviar qualquer um; enviados antes do commit e removidos (melhor esforço) se a gravação falhar — órfão eventual fica no bucket (o R2 não tem versionamento; ADR 0010 §8). Tipos detectados por tabela própria de assinaturas (`TiposDeArquivo`); a extensão só desempata ZIP × Office, OLE × Office antigo e texto × CSV.

---

## 8. Auditoria (v1)

Quem, tenant, ação, alvo, quando (IP opcional). Não audita leituras. Mecanismo, retenção e IP atrás do proxy: [ADR 0008](adr/0008-trilha-de-auditoria.md).

- Registro na mesma transação da ação (`IAuditoria`, chamada nos casos de uso); ações com nome estável (lista no contrato §11a). Status e responsável da solicitação pelos eventos, num manipulador dentro da transação, para cobrir todos os caminhos.
- Login com sucesso fica no tenant da sessão emitida; com falha, **sem tenant** (e-mail digitado, motivo, IP), visível só para a operação. Troca de link por sessão fica no tenant do link, sem usuário.
- Leitura: `GET /auditoria` com `auditoria.ver` (Dono e papéis customizados). Contato nunca lê.
- Retenção: 12 meses (job diário de expurgo), porque guarda IP e e-mail.


- Login: sucesso e falha; troca de link por sessão.
- Usuários: convite, remoção, troca de papel, capacidade semanal.
- Portal: concessão/revogação de acesso de contato, emissão/revogação de link, alteração de escopo.
- Contratos: criação, edição, suspensão/encerramento, ajuste de saldo, fechamento de período.
- Apontamentos: edição e exclusão (com valor anterior).
- Solicitações: mudança de status, de responsável e de contrato.

---

## 9. Requisitos adicionais

- **Rate limiting:** login, `/auth/portal/link` (por IP e por token), abertura de solicitação pelo portal, upload de anexos.
  - Sugestões (`POST /api/v1/sugestoes`, F13e): 5 por hora por pessoa, contadas no banco (valem entre instâncias) → `429 limite-requisicoes`.
  - Relato de erro do app (`POST /api/v1/erros-web`, pública, F13d): 10 por minuto por IP, IPv6 por /64 (`RateLimit__ErrosWeb__PorMinuto`), corpo de até 16 KB. Vai sem cookie nem token; a API limpa e-mails, ids, números longos e a query antes de registrar, e o app não manda id de contato, cliente ou link.
  - As rotas públicas de `/auth` (login, convites, recuperação de senha) e o reenvio da confirmação: 10 por minuto por IP (`RateLimit__Auth__PorMinuto`).
  - **Cadastro (`/auth/registro`): limite próprio**, 3 por hora por IP (padrão, `RateLimit__Registro__PorHora`; IPv6 agrupado por /64, para trocar de endereço dentro do prefixo não renovar a cota), mais o Turnstile (RNF-SEG-10, 04/11). Substitui o de `/auth` nessa rota: o ASP.NET aplica uma política por rota, e os 3 por hora já são mais restritos que os 10 por minuto.
  - **Turnstile (F9c):** conferido no servidor por HTTP (`siteverify`), sem SDK, com sucesso, ação `registro` e hostname. Fail-closed: sem resposta do Cloudflare (ou com o segredo do servidor recusado, que vai para o log como erro), o cadastro é recusado. Desligado sem `Turnstile__Segredo`. As chaves de teste do Cloudflare não servem com ele ligado: a resposta delas traz outro hostname e é recusada.
  - **Renovação da sessão (`/auth/refresh`): limite próprio, 30 por minuto por refresh token** (`RateLimit__Refresh__PorMinuto`; a partição é o hash do cookie, e sem cookie o IP). Fica fora do limite por IP porque cada carga de página renova: um escritório atrás de um IP só, ou alguém com várias abas, esgotava o limite do login e caía na tela de entrar com a sessão ainda válida (achado da fumaça do F1c3; decidido em 15/10). O risco de abuso é baixo: sem um refresh válido a resposta é 401, e reapresentar um token já trocado revoga as sessões (ADR 0007).
- **Borda da API (ADR 0010 §3, F8b):** com `Proxy__TokenOrigem`, a API responde 404 a toda requisição sem o `X-Origem-Token` do Worker de `api.acordu.app.br`, antes de proxy, autenticação e rate limit. O IP do rate limit e da auditoria vem do `X-Forwarded-For` com `Proxy__Saltos=2` (Worker + front do Cloud Run), que só vale com o token ativo; sem ele, um salto. CORS só para `Cors__Origem` (`https://acordu.app.br`), com credenciais.
- **Segredos de produção no GitHub (F8f):** ficam como secrets do repositório; qualquer workflow do próprio repositório pode lê-los. Risco aceito no nível 0, com escrita só do Daniel e MFA (`deploy-ci-acordu-v1.md` §7).
- **Headers de segurança:** HSTS, CSP (sem `unsafe-inline` no portal), `X-Content-Type-Options`, `Referrer-Policy: same-origin` (o token do link não pode vazar por Referer — a SPA troca o token e remove-o da URL na primeira carga).
  - **CSP, `connect-src`:** `'self'`, `https://api.acordu.app.br` (a API fica noutra origem do mesmo site, ADR 0010), `https://viacep.com.br` e `https://brasilapi.com.br`. O SPA consulta o CEP direto no ViaCEP para preencher o endereço do cliente (decidido em 16/10), e o CNPJ na BrasilAPI (item abaixo). O `pnpm csp:conferir` confere essa lista exata, sem curinga. As chamadas vão sem cookie e sem Referer, então cada serviço só recebe o dado público que consulta (o ViaCEP, o CEP; a BrasilAPI, o CNPJ); se falhar, a pessoa preenche à mão. O script inline do tema em `index.html` entra na CSP por hash, não por `unsafe-inline`. A CSP e os demais cabeçalhos do SPA vão no arquivo `_headers` do Cloudflare Pages; os da API, num middleware da própria API.
  - **Consulta de CNPJ (RF-CLI-08, 04/11):** feita do navegador na BrasilAPI (`https://brasilapi.com.br/api/cnpj/v1/{cnpj}`, dados abertos da Receita; spec do F10, decisão 1a, sem provedor de reserva), como o CEP: a origem entra no `connect-src`, a chamada vai sem cookie e sem Referer, e só o CNPJ (dado público de empresa) sai do Acordu. Pela API do Acordu não, no nível 0: gastaria CPU do Cloud Run e somaria todos os usuários no limite por IP do provedor. CPF nunca é enviado a terceiros.
  - **Política completa do SPA (F8d, `apps/web/public/_headers`):** `default-src 'self'`; `script-src 'self'` + hash do script de tema + `https://challenges.cloudflare.com` (Turnstile do cadastro, F9e); `style-src 'self'` + hashes dos dois `<style>` fixos que o React Aria injeta (toque e rolagem de modal), **sem `unsafe-inline`** (os `style={}` do React vão pelo CSSOM, que a CSP não bloqueia); `img-src 'self' data: https://*.r2.cloudflarestorage.com` (logotipo do portal por URL assinada); `font-src 'self'`; `frame-src https://challenges.cloudflare.com` (o desafio é um iframe); `frame-ancestors 'none'`; `base-uri` e `form-action 'self'`; `object-src 'none'`. Mais HSTS (1 ano, subdomínios), `X-Frame-Options: DENY` e `Permissions-Policy` sem câmera, microfone, localização e pagamento. `connect-src 'self' https://api.acordu.app.br https://viacep.com.br https://brasilapi.com.br` (item acima). A CI confere os hashes contra o build, o Cloudflare em `script-src` e `frame-src`, a lista exata do `connect-src` sem curinga e a ausência de `unsafe-inline` (`pnpm csp:conferir`). Os clientes HTTP usam `credentials: 'include'`: o cookie da sessão só vai às rotas de `/api/v1/auth` pelo caminho dele, e a API só aceita a origem do SPA com credenciais (CORS).
- **Testes obrigatórios:**
  - cross-tenant (ADR 0001) e **cross-cliente** (ADR 0002), em integração e API;
  - papel Cliente em rota interna → 403;
  - sessão de link acessando outra solicitação → 404; link revogado → 401 na requisição seguinte;
  - nenhuma resposta do portal com interação/tarefa/apontamento interno;
  - Membro sem `contrato.gerenciar` → 403 ao criar contrato.
