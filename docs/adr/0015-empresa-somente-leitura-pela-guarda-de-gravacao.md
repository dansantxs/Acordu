# ADR 0015 — Empresa só leitura pela guarda de gravação

- **Status:** Aceito (G2 do F14, PR #186, 13/11/2026; design do F14, D2)
- **Data:** 13/11/2026
- **Contexto do projeto:** Acordu (marco F14, RF-EMP-01, RF-EMP-02)
- **Complementa, sem mudar o texto:** ADR 0001 (guarda de gravação por tenant) e ADR 0008 (trilha na mesma transação).

## Contexto

No F14 a empresa ganha uma situação (TESTE, ATIVA, VENCIDA, CANCELADA). VENCIDA e CANCELADA ficam **só leitura**: a equipe e o portal leem tudo o que já podiam, mas nada da empresa é gravado, nenhum e-mail sai e os jobs não agem nela. Seguem a conta da pessoa, a sessão, o sino e o "Sugerir melhoria". A permissão é conferida antes da situação (sem ela, 403). A volta a ATIVA vale na requisição seguinte.

A regra vale para toda gravação, de hoje e dos marcos seguintes. Uma tabela nova ou uma rota nova não pode escapar dela por esquecimento.

## Decisão

1. **A guarda fica na gravação.** O `AcorduDbContext` já recusa gravar em outro tenant antes de qualquer SQL (`IsolamentoVioladoException`). Ele passa a recusar também incluir, alterar ou remover entidade de um tenant só leitura, com `EmpresaSomenteLeituraException`. A situação é lida do banco uma vez por escopo, e não do token, para a mudança valer na requisição seguinte. A guarda roda no `SaveChanges`, depois da permissão, do 404 e das regras do caso de uso: a ordem "permissão antes da situação" sai sem código em cada caso de uso, e nada é gravado.
2. **Classificação obrigatória.** Toda entidade com `TenantId` é classificada como **da empresa** (bloqueada) ou **da pessoa ou rastro** (passa): sino e preferências de aviso, link da agenda, seguidores, refresh tokens, trilha de auditoria. Um teste de integração falha se uma entidade nova não estiver classificada, como a `ClassificacaoDoExpurgo` do F9. O `Tenant` é da empresa: só a mudança de situação, feita pelo sistema ou pela área da Nebrys, passa, num escopo marcado para isso.
3. **Gravação fora do change tracker** (`ExecuteUpdate`, `ExecuteDelete`) só nos pontos já revisados (convenções §1). Cada ponto novo declara a classificação do que toca.
4. **Jobs:** o `ExecutorPorTenant` percorre só as empresas graváveis. Recorrências, alertas de SLA, cronômetros esquecidos e fechamento automático não rodam nas só leitura. Os passos que existem por causa da situação (vencimento, avisos de situação, expurgo) têm laço próprio.
5. **E-mail:** a outbox descarta, com contagem no log, as mensagens de empresa só leitura (não adia: não há envio retroativo, como na decisão 6 do F9). `EnvioATerceiros.PodeAvisarAsync` também devolve falso para ela. Os e-mails da conta (senha, confirmação) e os avisos de situação aos Donos não passam por ali e continuam saindo.
6. **Erro:** `422` com `type` `empresa-somente-leitura` para a equipe. No portal (contato ou link), `422` com `type` `portal-indisponivel` e mensagem neutra ("No momento não é possível enviar por aqui; fale com {prestador}"), que não revela a situação comercial do prestador ao cliente dele.
7. **Situação nunca vaza entre empresas:** a guarda olha o tenant da entidade gravada, não o da pessoa. Quem tem vínculo em A (só leitura) e em B grava em B.

## Consequências

**Positivas**
- Uma barreira só, no mesmo ponto da barreira de isolamento, cobrindo equipe, portal, jobs e manipuladores da outbox. Rota nova nasce bloqueada; isentar algo é uma classificação explícita e revisada.
- A precedência 403 → 404 → 400 → só leitura sai da ordem natural do pedido.

**Negativas / riscos**
- Uma consulta a mais por escopo que grava (a situação do tenant). Pequena e indexada pela chave.
- O erro chega no fim do caso de uso: um anexo já enviado ao storage antes do commit é removido como em qualquer falha de gravação (melhor esforço, segurança §7).
- Validação de corpo (400) aparece antes da recusa de só leitura. É aceitável: nada é gravado.
- O front precisa saber da situação para desabilitar as ações antes (`/auth/eu` e `/portal/eu`). A recusa da API é a rede de proteção.

## Alternativas consideradas

- **Filtro de rota por método HTTP**, com as rotas isentas marcadas: barato e visível na Api, mas roda antes das checagens de permissão que ficam na Application (o 403 viraria só leitura) e não cobre jobs nem a outbox. Rejeitado como camada única.
- **Checagem explícita em cada caso de uso** (como `EnvioATerceiros.GarantirLiberadoAsync`): ordem exata, mas são dezenas de pontos e um esquecido grava. Rejeitado.
- **Situação como claim do token:** a política de autorização resolveria sem consulta, mas a mudança só valeria no próximo refresh (até 60 min, ADR 0007), contra "na requisição seguinte". Rejeitado.
- **Row-Level Security do PostgreSQL:** mesma objeção do ADR 0002 (variáveis de sessão no pool em modo transação do Neon). Rejeitado.
