# Acordu — Segurança e Modelo de Acesso (v1)

| | |
|---|---|
| **Versão** | 0.1 |
| **Data** | 06/10/2026 |
| **Escopo** | Identidade, autenticação, autorização (RBAC + escopo por cliente), sessões do portal, proteção de dados, anexos e auditoria. |
| **Base** | Modelo de acesso do Saldu (ADRs 0007/0008 herdados) + ADR 0002 do Acordu. |
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

Igual ao Saldu (`Usuario` global gerido pelo Identity, `Tenant`, `Vinculo` N-N, `Papel` como conjunto de `Permissao`), com a extensão do ADR 0002:

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
- Um mesmo login pode ser Membro no próprio tenant e Cliente em outro (ex.: um freelancer que também contrata uma agência). A troca de tenant ativo (herdada) resolve.

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
| **Leitor** | INTERNO | Todas as `*.ver` internas + `dashboard.ver`. |
| **Cliente** | CLIENTE | Todas as `portal.*`. |

Efeito do `EscopoPortal` sobre o papel Cliente (aplicado na Application, além das permissões):

| Escopo | Solicitações visíveis | Extrato |
|---|---|---|
| `TODAS_DO_CLIENTE` | Todas do cliente | Sim |
| `PROPRIAS` | Só as que ele abriu ou em que é o contato (abrir pelo portal grava o contato logado: é a mesma regra) | **Não** (o extrato expõe pedidos de colegas); nem a lista de contratos |

### 3.3 Guardas (invariantes)
- **≥ 1 Dono por tenant** (herdado).
- Papel padrão não é excluível; papel em uso não é excluível sem realocação (herdado).
- Não é possível atribuir papel CLIENTE por `/convites` nem papel INTERNO pelo convite de contato.
- Revogar acesso de contato remove o `Vinculo` e **revoga todos os refresh tokens e links** dele naquele tenant.
- Inativar cliente revoga os acessos de todos os seus contatos.

---

## 4. Autenticação

Herdada do Saldu (Identity, confirmação de e-mail, lockout, política de senha, recuperação por e-mail via Resend).

Fluxos específicos:
- **Registro:** cria `Usuario` + `Tenant` (o prestador) + `Vinculo` Dono + `CalendarioUtil` padrão + papéis padrão.
- **Convite de contato para o portal:** equipe convida → e-mail com link de aceite → contato define senha (ou, se já tem conta, confirma com a senha dela) → `Vinculo` com papel Cliente, `ClienteId` e escopo; `Contato.UsuarioId` preenchido.
  - Token do convite como o do link (32 bytes, base64url, só o SHA-256 no banco), **gerado no envio do e-mail**, nunca em claro na outbox. Validade de 7 dias a partir do envio; reconvidar ou revogar invalida.
  - Conta existente exige a senha dela: o token prova acesso ao e-mail, mas não autoriza adotar uma conta que pode ser da equipe de outro prestador. A falha conta para o bloqueio.
  - Conta criada no aceite já nasce com e-mail confirmado (o token chegou por ele).
  - E-mail que já tem vínculo no tenant (equipe ou contato de outro cliente) é recusado no convite e de novo no aceite: um vínculo por usuário e tenant.
- **Link de acesso por solicitação** (sem conta): ver §5.3.

---

## 5. Sessões e tokens

### 5.1 Equipe e contatos com conta
Herdado: access token JWT curto (30–60 min) + refresh token persistido e revogável. Claims: `sub`, `tenant_id`, `papel`, `permissoes`; **(Acordu)** para Cliente, também `cliente_id` e `escopo_portal`.
- **(Acordu) Portal:** a cada requisição em `/api/v1/portal`, a Api confere no banco que o usuário ainda é contato ativo do cliente, com vínculo, e usa o escopo do vínculo (não o da claim). Revogação vale na requisição seguinte, sem esperar o access token expirar; uma consulta indexada por requisição.

### 5.2 Troca de tenant
Herdada. Um usuário Cliente em um tenant e Membro em outro troca de tenant e recebe token com o papel correspondente.

### 5.3 Sessão de link (RF-POR-08)
- Token do link: 32 bytes aleatórios (CSPRNG), base64url; no banco, **só o SHA-256**.
- Validade padrão 30 dias; revogável; um link por (solicitação, contato), reemitir revoga o anterior.
- `POST /auth/portal/link` troca o token por um **access token de 60 min, sem refresh**. Claims: `tenant_id`, `cliente_id`, `acesso_portal_id`, `solicitacao_id`, `tipo = LINK_SOLICITACAO`, permissões fixas (`portal.solicitacao.ver`, `.comentar`, `.aprovar`).
- A cada requisição, o middleware confere se o `AcessoPortal` continua válido (não revogado nem expirado) — revogação vale imediatamente, sem esperar o token expirar.
- Se o contato ganhar conta depois, o link continua funcionando até expirar.
- **(Implementação)** A conferência por requisição lê o `AcessoPortal` (não revogado, não expirado) e o contato dele (ativo, de cliente ativo). Sem `sub` no token: interação registrada pelo link tem como autor o Id do contato. O e-mail do link sai logo após o commit, na requisição (o link também volta na resposta da emissão), nunca pela outbox.

---

## 6. Proteção de dados

- **Em trânsito:** HTTPS obrigatório (Caddy), HSTS.
- **Em repouso:** volume da OCI criptografado; object storage criptografado pela OCI.
- **Coluna:** sem criptografia de coluna no v1 (herdado; textos precisam de busca full-text).
- **LGPD:** o prestador é **controlador** dos dados dos seus clientes; o Acordu é **operador**. Exportação e exclusão por tenant sob solicitação; exclusão de contato anonimiza nome/e-mail nas interações, preservando o histórico do trabalho.
- **Nada de dado de cliente em log e no Discord:** só identificadores e status (herdado).

---

## 7. Anexos

- Upload via API (a API faz streaming para o object storage); nunca upload direto do navegador ao bucket no v1.
- Limites: 20 MB por arquivo, 5 por interação; tipos permitidos por lista (imagens, PDF, documentos Office, texto, zip). Conferência pelo conteúdo (*magic bytes*), não só pela extensão.
- Chave no bucket: `{tenantId}/{solicitacaoId}/{anexoId}` — sem nome original na chave.
- **Download:** a API verifica permissão (e visibilidade EXTERNA, no portal) e responde `302` para URL pré-assinada de **5 minutos**. O bucket é privado.
- `Content-Disposition: attachment` sempre (evita executar HTML/SVG no navegador).
- Antivírus fora do v1 (registrado como risco; avaliar ClamAV em container quando houver clientes pagantes).

---

## 8. Auditoria (v1)

Quem, tenant, ação, alvo, quando (IP opcional). Não audita leituras.

- Login: sucesso e falha; troca de link por sessão.
- Usuários: convite, remoção, troca de papel.
- Portal: concessão/revogação de acesso de contato, emissão/revogação de link, alteração de escopo.
- Contratos: criação, edição, suspensão/encerramento, ajuste de saldo, fechamento de período.
- Apontamentos: edição e exclusão (com valor anterior).
- Solicitações: mudança de status, de responsável e de contrato.

---

## 9. Requisitos adicionais

- **Rate limiting:** login, `/auth/portal/link` (por IP e por token), abertura de solicitação pelo portal, upload de anexos.
- **Headers de segurança:** HSTS, CSP (sem `unsafe-inline` no portal), `X-Content-Type-Options`, `Referrer-Policy: same-origin` (o token do link não pode vazar por Referer — a SPA troca o token e remove-o da URL na primeira carga).
- **Testes obrigatórios:**
  - cross-tenant (herdado) e **cross-cliente** (ADR 0002), em integração e API;
  - papel Cliente em rota interna → 403;
  - sessão de link acessando outra solicitação → 404; link revogado → 401 na requisição seguinte;
  - nenhuma resposta do portal com interação/tarefa/apontamento interno;
  - Membro sem `contrato.gerenciar` → 403 ao criar contrato.
