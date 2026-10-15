# ADR 0009 — Tempo real por SSE com sinais de mudança

- **Status:** Aceito
- **Data:** 14/10/2026
- **Contexto do projeto:** Acordu (v1)
- **Atende:** RF-COL-04 (atualização em tempo real da agenda e do quadro).

## Contexto

Na Agenda e no Quadro, várias pessoas arrastam itens ao mesmo tempo. Com a tela desatualizada, a próxima edição esbarra no `If-Match` (409) ou desfaz, sem saber, o que outra pessoa fez. O front já usa TanStack Query: basta saber **o que mudou** para invalidar o cache certo.

Opções avaliadas:
- **Transporte:**
  - a) WebSocket (SignalR);
  - b) SSE com `EventSource`, que não envia cabeçalho e obrigaria o token na URL ou um ticket curto;
  - c) SSE lido com `fetch`, que envia o `Authorization` como as demais chamadas.
- **Conteúdo:** dados completos do item ou só sinais.
- **Origem dos sinais:** eventos de domínio ou o change tracker da gravação.
- **Difusão:** em memória ou LISTEN/NOTIFY do PostgreSQL.

## Decisão

1. **SSE em `GET /api/v1/eventos`, lido com `fetch`** (c). É unidirecional, que é o que basta, e passa pelo Caddy sem configuração especial além de não armazenar a resposta. Autenticação igual à das outras rotas e só para a equipe (o portal não recebe).
2. **Só sinais de "mudou":** `event: mudanca`, `data: { tipo, id }`, com tipo `solicitacao`, `tarefa`, `projeto`, `recorrencia`, `contrato` ou `cliente`. O front invalida e busca de novo pela API, que já aplica permissões e filtros. Nenhum dado de negócio passa pelo canal, e a regra de quem vê o quê continua num lugar só.
3. **Sinais tirados do change tracker** na `UnitOfWork`: toda raiz incluída, alterada ou excluída que aparece numa tela. Interação e comentário sinalizam o item onde aparecem; apontamento, a tarefa. Os sinais saem **depois do commit**. Eventos de domínio não bastariam, porque editar o prazo ou arrastar o dia planejado não gera evento.
4. **Difusor em memória** (`DifusorSinais`, singleton): um canal limitado por conexão (256), que descarta o mais antigo quando enche. Uma conexão lenta perde sinal, mas nunca atrasa quem grava.
5. **Comentário de vida (`: vivo`) a cada 25 s** e `retry: 5000`, para proxies não derrubarem a conexão ociosa.

## Consequências

- Com **uma instância** da API (VM única, ADR 0006), tudo funciona sem infraestrutura nova. Com mais de uma, troca-se o difusor por LISTEN/NOTIFY do PostgreSQL, que já está na stack, mantendo a mesma interface e o mesmo formato de sinal.
- **Gravações fora da `UnitOfWork` não sinalizam:** sino, seguidores, preferências e o SQL dos jobs. O sino continua por consulta; se precisar, entra um sinal por pessoa depois.
- **Perder um sinal é tolerável:** o TanStack Query revalida ao focar a janela e o `If-Match` segue protegendo contra sobrescrita.
- **No Caddy, a rota `/api/v1/eventos` fica fora do `encode`** e usa `flush_interval -1` (deploy §2.1).
