# ADR 0014 — Área da Nebrys: sessão de operação sem tenant

- **Status:** Proposto (design do F14, D5; aceito ou recusado no G2 do F14)
- **Data:** 13/11/2026
- **Contexto do projeto:** Acordu (marco F14, RF-NEB-01, RF-NEB-02)
- **Complementa, sem mudar o texto:** ADR 0001 (identidade e multiusuário), ADR 0002 (isolamento), ADR 0008 (trilha de auditoria) e ADR 0012 (tabelas globais).

## Contexto

Até o F13, toda sessão do Acordu pertence a um tenant: a da equipe e a do contato com conta (ADR 0001, `tenant_id` no token) e a do link do portal (ADR 0002, item 5). As tabelas globais (ocorrências de erro, marcas de operação, sugestões; ADR 0012) não têm rota que as leia.

O F14 cria a operação da Nebrys: ativar, prorrogar, cancelar e reativar empresas, emitir convites de empresa, tratar a lista de espera, liberar um CPF/CNPJ e cuidar da empresa de demonstração. A operação precisa ver **várias** empresas, mas só os dados da relação com a Nebrys (nome, documento, situação, datas, criador, tamanho da equipe), nunca os dados de negócio do prestador (RNF-PRIV-01). Usuários de empresa, inclusive Donos, e contatos não podem nem saber que a área existe (404).

O risco é o do ADR 0002 ao contrário: qualquer atalho que dê à operação um contexto de tenant, ou um "tenant da Nebrys" que enxerga os outros, abre um caminho dos filtros globais para os dados de negócio.

## Decisão

1. **Quem é operador:** lista de e-mails de contas existentes na variável `Nebrys__Operadores` (configuração por ambiente, ADR 0010). Sem a variável, ninguém é operador e a área inteira responde 404. Não há tela para conceder o papel. A conta precisa ter o e-mail confirmado.
2. **Sessão de operação, sem tenant:** `POST /api/v1/nebrys/sessao` (`email`, `senha`) confere a senha pelo Identity (bloqueio e limite de `/auth` valem) e a lista do item 1, e devolve um access token de 30 min com `tipo = OPERACAO`, `sub` e **sem `tenant_id`**, sem refresh e sem cookie (como a sessão de link, ADR 0007 item 6). Credencial errada ou conta fora da lista → 404, igual a rota inexistente. O SPA guarda o token só em memória; recarregar pede a senha de novo.
3. **Contexto vazio por construção:** a sessão de operação não define tenant no `ContextoTenant`. Os filtros globais (ADR 0001 e 0002) continuam valendo e não devolvem nada de negócio; a guarda de gravação recusa gravar em tenant. O que a área lê vem de consultas próprias na Infrastructure, pontos revisados de `IgnoreQueryFilters` (convenções §1), que projetam só os campos da relação.
4. **Rotas e casos de uso próprios:** `/api/v1/nebrys/*`, num grupo com um filtro que responde 404 a qualquer sessão que não seja de operação (equipe, contato, link ou anônimo), antes de qualquer outra resposta. A sessão de operação recebe 404 em todas as rotas fora da área. Casos de uso em `Acordu.Application.Nebrys`, com DTOs próprios. Um teste de arquitetura impede esse namespace de referenciar os tipos de negócio (clientes, contatos, solicitações, tarefas, horas, contratos, anexos) e seus DTOs. Documento OpenAPI próprio (`openapi/nebrys.json`), fora do `interno.json`; área própria no SPA (`apps/web/src/nebrys`, carregada sob demanda).
5. **Ação sobre uma empresa:** muda só o `Tenant` (situação e datas) ou entidades globais. Leva `If-Match` com a versão do `Tenant` (`xmin`, convenções §1), e a divergência dá `409 conflito-versao`, como as transições do contrato §1: o diário e outra ação podem ter mudado a empresa depois da leitura. Empresa inexistente ou já expurgada → `404`. Quando precisa gravar no tenant (empresa de demonstração, que é da própria Nebrys), roda num escopo com o tenant fixado, como os jobs (`ExecutorPorTenant`): é o sistema agindo, com a guarda de gravação e os filtros valendo.
6. **Trilha:** toda ação da área entra na auditoria do ADR 0008, **sem tenant** (como o login com falha, item 4 daquele ADR), com o operador em `usuario`, ação `nebrys.*`, o alvo (empresa, convite, entrada da lista ou documento liberado) e o antes e o depois em `dados`. Fica fora do alcance de qualquer empresa e sobrevive ao expurgo dela; a retenção de 12 meses do ADR 0008 vale.
7. **Tabelas globais lidas pela área:** convites de empresa, lista de espera, registro de documentos que já tiveram teste e aceites dos textos legais são globais (padrão `Sugestao`: sem `EntidadeBase`, sem filtro, sem `TenantId`).
   - Referência a empresa (`EmpresaId`) só onde houver motivo legítimo. Nenhuma das quatro tem: o convite e a entrada da lista existem antes da empresa, o aceite é da conta, e o **registro de documentos não tem referência à empresa nem à pessoa** (só o hash com chave e as datas), para sobreviver ao expurgo sem identificar ninguém.
   - A área lê essas tabelas e o `Tenant`.
   - Ocorrências de erro, marcas de operação e sugestões continuam sem rota: os testes de arquitetura do ADR 0012 ficam como estão.
   - Cada tabela nova ganha um teste de arquitetura no mesmo molde, que libera o tipo só para `Acordu.Application.Nebrys`, a consulta da área na Infrastructure e a feature que grava (cadastro, lista de espera, aceite).

## Consequências

**Positivas**
- O isolamento do ADR 0002 não ganha exceção: a sessão de operação não tem tenant, então os filtros não abrem nada, e o que ela vê está declarado nas consultas da área.
- Defesa em camadas, como no portal: filtro de rota (404), DTOs sem campos de negócio, teste de arquitetura, filtros globais com contexto vazio.
- Uma tela da Nebrys futura (sugestões, erros) segue o mesmo modelo, sem decisão nova.

**Negativas / riscos**
- Sem segundo fator (fora de escopo no F14): a senha do operador é a barreira. Mitigação: lista fechada por variável, sessão curta sem refresh, bloqueio do Identity, toda ação na trilha. Volta com mais gente na operação.
- Trocar o operador exige nova revisão no Cloud Run (a variável muda pelo `deploy.yml`).
- A trilha sem tenant guarda e-mails de convites e da lista de espera por 12 meses; a política de privacidade diz isso.

## Alternativas consideradas

- **"Tenant da Nebrys" com um papel que vê os outros tenants:** quebra o modelo do ADR 0002 (um tenant enxergando outro) e exigiria exceção nos filtros. Rejeitado.
- **Flag de operador no banco (tabela ou claim do Identity):** permite conceder pela área, mas cria um papel a proteger e uma tela de concessão. Fica para quando houver mais de um operador.
- **Cloudflare Access na frente de `/api/v1/nebrys`:** segundo fator de graça, mas prende a autenticação ao provedor e exige validar o JWT do Access na API (ADR 0010, sem dependência de provedor). Rejeitado no nível 0.
- **Sessão de operação a partir da sessão da equipe** (trocar de "empresa" para a área): reaproveita o cookie de refresh, mas o refresh é por tenant (ADR 0007) e misturaria as duas sessões no mesmo navegador. Rejeitado.
