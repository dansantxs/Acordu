# Acordu — Estado

Visão geral curta, atualizada por cada PR (`docs/processo.md` §1). O histórico está no git, nos PRs e no registro dos designs. Atualizado em 26/11/2026 (F15a, canal de cada mensagem, em revisão).

## Em revisão

- **F15a — canal de cada mensagem** (#183, branch `feat/F15a-canal-mensagem`; F15-60 a F15-62): `CanalMensagem` (ACORDU, PORTAL, EMAIL, WHATSAPP) em cada interação, com a invariante do D8 (equipe só ACORDU, contato nunca); `canal` na conversa da equipe e na do portal, com o rótulo em cada mensagem; migration `CanalDasMensagens` (pelo bot) com o preenchimento das anteriores (do contato → PORTAL, da equipe → ACORDU).

## Marco em andamento

- **F15 — Omnichannel A** (#183; [spec](specs/F15-omnichannel-email/spec.md), [design](specs/F15-omnichannel-email/design.md)): G2 aprovado (PR #234, recomendações de todas as D e das 11 perguntas; ADR 0016 aceito). 15 PRs em série (F15a a F15o); o F15a está em revisão. Provisionamento manual do Daniel (DNS/MX do subdomínio de entrada, Email Routing, secret do Worker) no PR que o design indica.

## Marco concluído

- **F14 — situação da empresa e cadastro controlado** (#182; [spec](specs/F14-lancamento-controlado/spec.md), [design](specs/F14-lancamento-controlado/design.md)): G2 com o PR #186; ADRs 0014 e 0015 aceitos. 113 cenários em 19 PRs (F14a a F14s), todos com teste; fechado pelo F14s (#220). Primeiro marco conduzido pelo `/marco`.

## Onde o produto está

- Backend do v1 completo; frontend F1–F7 e marcos F8–F13 na `main`; F14 inteiro na `main`.
- **Deploy do F14** em 24/11 (`ae5f5e7`). Em 25/11 o Daniel zerou o banco de produção (schema `public` recriado e migrations aplicadas por um novo deploy) para recomeçar os testes: a migration do F14a pôs as empresas antigas em CANCELADA, e o expurgo apagaria as contas, inclusive a de operação. **Cadastro por convite aberto** (`CADASTRO_MODO=convite`); `TEXTOS_LEGAIS_PUBLICADOS` ausente (desligada). Conferidos pelo Daniel em 25/11: entrada na área da Nebrys, lista de empresas, `/cadastro` mostrando a lista de espera e a entrada chegando à área.
- **Deploy do pacote de melhorias** em 25/11 (`d6cb193`): fix #222 (link do convite sem sessão), #191 (só leitura nos controles), #213 (expurgo dos convites), #224 (convite para e-mail descartável recusado), #225 (marca de demonstração na área), #226 (redefinição de senha não sai para conta fictícia) e #208 ("Falar no WhatsApp"). Sem migration nem variável nova. O passo `expurgo-convites` roda no primeiro diário depois do deploy.
- **Produção no ar** desde o primeiro deploy (12/11). Conferidos pelo Daniel em 12 e 13/11: F8-22, F8-26, F8-32 (fumaça de ponta a ponta), F9-47 (Turnstile no cadastro), F13-31 (avisos do deploy), WIF restrito, tokens do R2 separados, Resend verificado, MFA nas cinco contas, alerta de orçamento (R$ 10/mês, 50/90/100%), token de origem (404 direto, 200 pela borda), CORS só para `acordu.app.br` e `TempoReal__Sse=false`.

## Próximos passos

1. Escolher o próximo marco (`/roadmap`, G1). Na área da Nebrys, **ativar a empresa da própria Nebrys** (está em TESTE até 25/12; se vencer, a conta de operação é expurgada com ela). Fumaça que falta do modo convite: convidar da lista de espera e cadastrar pelo link. Demonstração: criar em `/nebrys/demonstracao` e indicar a própria conta. Ligar `TEXTOS_LEGAIS_PUBLICADOS` só depois da #194, num deploy fora do horário de uso (deploy-ci §7).
2. Conferências que faltam da produção:
   - deploy: F8-27 e F8-28 (rollback por migration e por health), F8-29 (jobs do Scheduler), hostname do Turnstile;
   - backup: F8-30 e F13-32 na primeira madrugada com o `BACKUP_ATIVO`; F8-31 (restore) antes do primeiro cliente;
   - monitoramento: F13-02 (Error Reporting), trace do F13a, pilhas do Firefox e do Safari (F13d), vigia e KV (F13h);
   - riscos (deploy-ci §10): `max-instances`, limpeza do Artifact Registry e lifecycle do bucket, `acordu_app` sem DDL e autoscaling do Neon;
   - arquivos órfãos no storage se o processo cair entre o commit do expurgo e a remoção (F14i): limpeza por prefixo `{tenantId}/` como melhoria.
3. Política de privacidade e termos de uso: rascunho na `main` (F14f, #193); marcadores para depois do registro da Nebrys (#194), que também acrescenta a retenção dos convites ("Convites: o e-mail fica até 90 dias depois de usado, revogado ou vencido", #213).

## Para o Daniel confirmar

- F14s: há uma demonstração só; as contas fictícias nascem sem senha (ninguém entra com elas), e a pessoa indicada é Dona com todas as permissões na demonstração (pode mudar e apagar dados, mas nada sai por e-mail; restaurar volta tudo). A restauração mantém as sessões abertas nela e as pessoas indicadas; uma pessoa indicada que se removeu da equipe da demonstração deixa de ser indicada. Duas criações ao mesmo tempo: a segunda falha (transação serializável), sem segunda demonstração. Se preferir outro comportamento em algum desses pontos, é ajuste pequeno.
- F14s, escolha fora do design: a demonstração nasce com o envio liberado (sem isso as telas desabilitam convites com "confirme o e-mail"); o bloqueio olha a marca antes da liberação, então nada sai. Recomendação: manter.
- F14s, escolha fora do design: a criação e a restauração recusam com 422 se houver conta com senha num dos e-mails fictícios (senão essa pessoa entraria como da equipe). Recomendação: manter.
- F14s, fora dos cenários: `DELETE /nebrys/demonstracao/indicacoes/{usuarioId}` e a ação "Tirar" na tela, para desfazer uma indicação. Recomendação: manter; alternativa: tirar.

- F14r: "sem dado" para oferecer os exemplos é a regra do expurgo de cadastro, então convidar alguém para a equipe (vínculo de outra pessoa) já tira a oferta. O expurgo do cadastro abandonado continua contando os exemplos como dado (uma empresa só com exemplos não sai em 7 dias; sai pelo expurgo das só leitura). Se preferir, os dois passam a ignorar os exemplos.

- P1-06 e P1-21 com o "então" mudado no P1b (#178).
- Conferências manuais do P1 no primeiro uso de cada comando: `/roadmap` (P1-26, P1-27: usado em 12/11 para escolher o F14), `/marco` no F14 (P1-11 a P1-19, P1-23 a P1-25), `/deploy` no próximo deploy (P1-28, P1-29; o primeiro não foi pelo comando), `/triagem` no primeiro aviso e na primeira sugestão (P1-30 a P1-32). Registrar no design do P1 quando acontecer.
- Desvios registrados nos PRs: F12c sem `BroadcastChannel` (D6), F12e sem a coluna `AvisadoEsquecidoEm` (D5).
- Apagar a branch remota velha `feat/f12f-app-instalavel`.

## Limitações conhecidas do ambiente

- Sessão na nuvem: sem SDK .NET e com nuget.org bloqueado; Infrastructure e Api só compilam na CI. Validar abrindo o PR como rascunho (a CI só roda com PR aberto).
- PR em rascunho não roda E2E nem imagem (deploy-ci §3): a CI completa só roda quando o PR é marcado como pronto, e a sessão na nuvem não consegue marcar (a API REST não converte rascunho). Quem marca é o Daniel.
- Antes do push, `scripts/ci-local.sh` roda o que a CI rodaria (deploy-ci §3.1); na nuvem, sem `dotnet`, o backend fica para a CI.
- Sessão na nuvem: `gh pr create` não funciona (GraphQL bloqueado); usar `gh api repos/dansantxs/acordu/pulls`. O token não aciona workflows nem rerun (403).
- Migrations e capturas do manual são geradas por bot: commitar `.github/migration-request` ou `.github/ajuda-request` na branch; commit do bot não dispara a CI (`git commit --allow-empty`).
- Logs da CI só pelas annotations do check run (`gh api .../check-runs/<id>/annotations`).
- Depois de force push do Claude, atualizar a branch local com `git fetch` + `git reset --hard origin/<branch>`.
