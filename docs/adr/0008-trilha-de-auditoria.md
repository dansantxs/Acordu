# ADR 0008 — Trilha de auditoria explícita nos casos de uso

- **Status:** Aceito
- **Data:** 13/10/2026
- **Contexto do projeto:** Acordu (v1)
- **Atende:** RNF-AUD-01 e segurança §8.

## Contexto

O v1 precisa registrar quem fez o quê nas ações sensíveis: login, equipe e papéis, acesso ao portal, contratos e saldo, edição e exclusão de apontamentos, e status, responsável e contrato das solicitações. O registro serve ao Dono do prestador (quem mexeu no saldo, quem apagou um apontamento) e à operação (tentativas de login).

Havia três formas de capturar:
- **a) chamada explícita** nos casos de uso, gravada na mesma transação da ação;
- **b) eventos de domínio** já existentes;
- **c) interceptor genérico do EF** sobre o change tracker.

Os eventos não trazem quem agiu e não cobrem tudo (login e convites acontecem fora do domínio). O interceptor registraria cada coluna alterada sem o nome da ação, com ruído e sem o "valor anterior" no formato que interessa.

## Decisão

1. **`IAuditoria.Registrar(acao, alvoTipo, alvoId, dados)`**, chamada pelos casos de uso (Application). O registro entra na unidade de trabalho corrente e é gravado no mesmo `SalvarAsync` da ação: só fica auditado o que foi efetivado, e uma falha da ação não deixa rastro falso.
2. **Quem, tenant, quando e IP vêm do contexto** (`ITenantContext`, `TimeProvider`, `IOrigemRequisicao`). `usuario` nulo = o sistema (job) ou uma sessão de link do portal.
3. **Ações com nome estável** (`AcoesAuditoria`, formato `área.ação`); `dados` em JSON para o detalhe (valor anterior, motivo). Nunca segredo nem token.
4. **Login acontece antes do tenant:** sucesso fica no tenant da sessão emitida (o Dono vê); falha fica **sem tenant**, com o e-mail digitado, o motivo e o IP, invisível pelos filtros globais (só a operação, no banco).
5. **Tabela fora do domínio** (`auditoria`, Infrastructure): só inclusão; filtros globais de tenant e de contexto Cliente (contato nunca lê).
6. **Retenção de 12 meses**, por um job diário (`/internal/jobs/expurgo-auditoria`): a trilha guarda IP e e-mail, então fica só o tempo necessário (LGPD).
7. **IP real atrás do Caddy:** `UseForwardedHeaders` confiando no último salto do `X-Forwarded-For` quando `Proxy__ConfiarEncaminhamento=true` (a API não tem porta exposta; só o Caddy a alcança). Corrige também o rate limit por IP.

## Consequências

- Cada caso de uso sensível precisa lembrar de chamar `Registrar`: os testes funcionais de cada área conferem a trilha.
- Leitura em `GET /auditoria` (`auditoria.ver`, só Dono e papéis customizados), paginada e filtrável.
- Mudar o nome de uma ação quebra filtros salvos no front: os nomes são contrato.
