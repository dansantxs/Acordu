# ADR 0002 — Portal do cliente: contato como usuário com escopo por cliente

- **Status:** Aceito
- **Data:** 30/09/2026
- **Contexto do projeto:** Acordu (v1)

## Contexto

O portal do cliente é diferencial central do produto: o contato da empresa atendida abre solicitações, acompanha status e SLA, responde, aprova entregas e vê o extrato de horas do contrato.

Isso cria um **segundo nível de isolamento**. No modelo multi-tenant básico (ADR 0001), todo usuário de um tenant vê todos os dados do tenant. No Acordu, o tenant é o prestador, e dentro dele há vários clientes que **não podem se ver**: o contato do cliente A nunca pode enxergar nada do cliente B, e em alguns casos nem as solicitações de colegas do próprio cliente.

Além disso, cada solicitação mistura itens que o cliente pode ver (comentários externos, tarefas marcadas como visíveis, horas faturáveis) com itens que ele nunca pode ver (notas internas, horas não faturáveis, discussões da equipe).

## Decisão

1. **Identidade:** o contato do cliente é um `Usuario` comum (ASP.NET Core Identity), com um `Vinculo` no tenant do prestador que carrega:
   - papel **Cliente** (semeado, com permissões só de portal: `portal.solicitacao.*`, `portal.extrato.ver`);
   - `ClienteId` (obrigatório para o papel Cliente, proibido nos demais);
   - `EscopoPortal`: `PROPRIAS` (só o que ele abriu) ou `TODAS_DO_CLIENTE`.
   O mesmo login pode ser membro de um tenant e contato em outro (vínculo N-N do ADR 0001).

2. **Isolamento em duas camadas:**
   - Camada 1 — `TenantId` via *global query filter* (ADR 0001).
   - Camada 2 — **`ClienteId` via global query filter adicional**, ativo quando o `ITenantContext` indica papel Cliente. Toda raiz ligada a cliente carrega `ClienteId` desnormalizado (`Solicitacao`, `Interacao`, `Tarefa`, `Apontamento`, `Projeto`, `Contrato`, `AcessoPortal`), para o filtro não depender de joins.
   - `EscopoPortal = PROPRIAS` é aplicado na **Application** (casos de uso do portal filtram por `ContatoId`).

3. **Visibilidade de conteúdo:** cada interação, tarefa e apontamento tem visibilidade explícita (INTERNA/EXTERNA ou `VisivelCliente`), com **padrão interno**. Os casos de uso do portal vivem num namespace próprio (`Acordu.Application.Portal`) com **DTOs próprios**, que simplesmente não têm campos internos — o vazamento vira erro de compilação, não de revisão.

4. **Rotas separadas:** endpoints do portal em `/api/v1/portal/*`, com política de autorização que exige papel Cliente; endpoints internos rejeitam papel Cliente (403).

5. **Acesso sem conta (link por solicitação):** `AcessoPortal` com token aleatório de 32 bytes, guardado como hash, com expiração e revogação, escopo de **uma** solicitação. Gera uma sessão restrita (claims `acessoPortalId`, `solicitacaoId`, `clienteId`), sem acesso ao extrato nem a outras solicitações.

## Consequências

**Positivas**
- Um único modelo de identidade (sem "usuário de portal" paralelo), reaproveitando Identity, convites, lockout e refresh token.
- Defesa em profundidade: filtro no banco + DTOs sem campos internos + rotas separadas.
- O contato pode evoluir de link sem senha para conta completa sem perder histórico.

**Negativas / riscos**
- **Risco de vazamento entre clientes** se alguma consulta contornar o segundo filtro (ex.: `IgnoreQueryFilters`, SQL cru no read side). Mitigação obrigatória:
  - teste de isolamento por cliente (RNF-SEG-02) em integração **e** API, cobrindo listar, ler, comentar e baixar anexo;
  - consultas SQL cruas do read side recebem `TenantId` **e** `ClienteId` como parâmetros obrigatórios (helper único, revisado);
  - proibido `IgnoreQueryFilters` fora da Infrastructure, com teste de arquitetura que falha se aparecer.
- `ClienteId` desnormalizado precisa ser mantido coerente (definido na criação, imutável; tarefa herda de solicitação/projeto).
- Dois filtros globais tornam as queries um pouco mais pesadas; índices compostos começam por `(TenantId, ClienteId, ...)` nas tabelas consultadas pelo portal.

## Alternativas consideradas

- **Cliente como tenant próprio:** isolamento "de graça", mas quebra o modelo (o prestador precisaria de acesso cruzado a todos os tenants dos clientes). Rejeitado.
- **Usuários de portal em tabela separada, fora do Identity:** duplicaria autenticação, recuperação de senha e lockout. Rejeitado.
- **Filtrar por cliente só na Application:** mais simples, porém uma query esquecida vaza dados. Rejeitado como camada única; mantido só para o escopo `PROPRIAS`.
- **Row-Level Security do PostgreSQL:** defesa forte, mas acopla a regra ao banco e complica o pool de conexões (definir variáveis de sessão por request). Reavaliar se o produto crescer.
