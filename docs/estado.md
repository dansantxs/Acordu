# Acordu — Estado

Visão geral curta, atualizada por cada PR (`docs/processo.md` §1). O histórico está no git, nos PRs e no registro dos designs. Atualizado em 12/11/2026 (#180).

## Em revisão

- **Correção do #180** — o bundle das migrations falhava no deploy sem `dotnet restore`; script único para deploy e CI.

## Marco em andamento

- Nenhum. **P1 — fábrica agêntica** ([spec](specs/P1-fabrica-agentica/spec.md), [design](specs/P1-fabrica-agentica/design.md)) concluído com o P1c (#179). Próximo marco pelo `/roadmap`.

## Onde o produto está

- Backend do v1 completo; frontend F1–F7 e marcos F8–F13 na `main` (F13 concluído com o #174).
- **Deploy (F8):** provisionado até os secrets: `acordu-prod`, WIF, Neon, domínio `acordu.app.br` no Cloudflare (SSL estrito), R2 com os dois tokens, Resend verificado, Turnstile, token do Cloudflare e os três webhooks do Discord. Primeiro deploy (12/11) parou duas vezes: na autenticação (condição do WIF com o nome do repo em minúsculas, corrigida no console para `repository_id`) e nas migrations (#180). A produção ainda não tem revisão.

## Próximos passos

1. Usar a fábrica: `/roadmap` para escolher o próximo marco (G1) e `/marco` para conduzir.
2. Retomar o provisionamento no passo 4, já com os três webhooks do Discord e a permissão *Workers KV Storage: Edit* no token do Cloudflare. Registrar só `acordu.app.br` agora (`acordu.com.br` depois, só redireciona). Conferir o autoscaling do Neon até 1 CU.
3. Primeiro deploy e as conferências que ficaram para ele: F9-46/47, agrupamento do Error Reporting, trace, pilhas do Firefox/Safari, vigia (F13).
4. **F14** (spec a escrever depois do provisionamento): situação da empresa (teste, ativa, vencida, cancelada), só leitura e expurgo depois do vencimento, cadastro por convite ou lista de espera no início, CPF/CNPJ único para o teste, e-mail descartável bloqueado, área da Nebrys, empresa de demonstração e dados de exemplo. Decidido em 10/11: sem limite de pessoas na equipe durante o teste.

## Para o Daniel confirmar

- P1-06 e P1-21 com o "então" mudado no P1b (#178).
- Conferências manuais do P1 no primeiro uso de cada comando: `/roadmap` (P1-26, P1-27), `/marco` no próximo marco (P1-11 a P1-19, P1-23 a P1-25), `/deploy` no primeiro deploy (P1-28, P1-29), `/triagem` no primeiro aviso e na primeira sugestão (P1-30 a P1-32). Registrar no design do P1 quando acontecer.
- Desvios registrados nos PRs: F12c sem `BroadcastChannel` (D6), F12e sem a coluna `AvisadoEsquecidoEm` (D5).
- Apagar a branch remota velha `feat/f12f-app-instalavel`.

## Limitações conhecidas do ambiente

- Sessão na nuvem: sem SDK .NET e com nuget.org bloqueado; Infrastructure e Api só compilam na CI. Validar abrindo o PR como rascunho (a CI só roda com PR aberto).
- Sessão na nuvem: `gh pr create` não funciona (GraphQL bloqueado); usar `gh api repos/dansantxs/acordu/pulls`. O token não aciona workflows nem rerun (403).
- Migrations e capturas do manual são geradas por bot: commitar `.github/migration-request` ou `.github/ajuda-request` na branch; commit do bot não dispara a CI (`git commit --allow-empty`).
- Logs da CI só pelas annotations do check run (`gh api .../check-runs/<id>/annotations`).
- Depois de force push do Claude, atualizar a branch local com `git fetch` + `git reset --hard origin/<branch>`.
