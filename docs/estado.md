# Acordu — Estado

Visão geral curta, atualizada por cada PR (`docs/processo.md` §1). O histórico está no git, nos PRs e no registro dos designs. Atualizado em 13/11/2026 (spec do F14).

## Em revisão

- **Spec e design do F14** — aguardando o G2: perguntas 1 a 19 da §4 da spec, decisões D1 a D15 do design e aceite dos ADRs 0014 e 0015 (propostos).

## Marco em andamento

- **F14 — situação da empresa e cadastro controlado** (#182; [spec](specs/F14-lancamento-controlado/spec.md), [design](specs/F14-lancamento-controlado/design.md)): 111 cenários em 19 PRs (F14a a F14s). Para abrir o cadastro por convite bastam F14a a F14q; dados de exemplo e demonstração por último. Primeiro marco conduzido pelo `/marco`.

## Onde o produto está

- Backend do v1 completo; frontend F1–F7 e marcos F8–F13 na `main`.
- **Produção no ar** desde o primeiro deploy (12/11). Conferidos pelo Daniel em 12 e 13/11: F8-22, F8-26, F8-32 (fumaça de ponta a ponta), F9-47 (Turnstile no cadastro), F13-31 (avisos do deploy), WIF restrito, tokens do R2 separados, Resend verificado, MFA nas cinco contas, alerta de orçamento (R$ 10/mês, 50/90/100%), token de origem (404 direto, 200 pela borda), CORS só para `acordu.app.br` e `TempoReal__Sse=false`.

## Próximos passos

1. G2 do F14; depois o `/marco F14 182` segue pelo F14a.
2. Conferências que faltam da produção:
   - deploy: F8-27 e F8-28 (rollback por migration e por health), F8-29 (jobs do Scheduler), hostname do Turnstile;
   - backup: F8-30 e F13-32 na primeira madrugada com o `BACKUP_ATIVO`; F8-31 (restore) antes do primeiro cliente;
   - monitoramento: F13-02 (Error Reporting), trace do F13a, pilhas do Firefox e do Safari (F13d), vigia e KV (F13h);
   - riscos (deploy-ci §10): `max-instances`, limpeza do Artifact Registry e lifecycle do bucket, `acordu_app` sem DDL e autoscaling do Neon.
3. Política de privacidade e termos de uso: ainda não existem; entram no F14 (F14f), com revisão do Daniel, de preferência com advogado.

## Para o Daniel confirmar

- P1-06 e P1-21 com o "então" mudado no P1b (#178).
- Conferências manuais do P1 no primeiro uso de cada comando: `/roadmap` (P1-26, P1-27: usado em 12/11 para escolher o F14), `/marco` no F14 (P1-11 a P1-19, P1-23 a P1-25), `/deploy` no próximo deploy (P1-28, P1-29; o primeiro não foi pelo comando), `/triagem` no primeiro aviso e na primeira sugestão (P1-30 a P1-32). Registrar no design do P1 quando acontecer.
- Desvios registrados nos PRs: F12c sem `BroadcastChannel` (D6), F12e sem a coluna `AvisadoEsquecidoEm` (D5).
- Apagar a branch remota velha `feat/f12f-app-instalavel`.

## Limitações conhecidas do ambiente

- Sessão na nuvem: sem SDK .NET e com nuget.org bloqueado; Infrastructure e Api só compilam na CI. Validar abrindo o PR como rascunho (a CI só roda com PR aberto).
- Sessão na nuvem: `gh pr create` não funciona (GraphQL bloqueado); usar `gh api repos/dansantxs/acordu/pulls`. O token não aciona workflows nem rerun (403).
- Migrations e capturas do manual são geradas por bot: commitar `.github/migration-request` ou `.github/ajuda-request` na branch; commit do bot não dispara a CI (`git commit --allow-empty`).
- Logs da CI só pelas annotations do check run (`gh api .../check-runs/<id>/annotations`).
- Depois de force push do Claude, atualizar a branch local com `git fetch` + `git reset --hard origin/<branch>`.
