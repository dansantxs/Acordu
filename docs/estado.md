# Acordu — Estado

Visão geral curta, atualizada por cada PR (`docs/processo.md` §1). O histórico está no git, nos PRs e no registro dos designs. Atualizado em 25/11/2026 (fix #222, sessão na abertura sem conta).

## Em revisão

- **Fix #222 — link do convite preso em "Carregando o convite…"** (bug de produção, branch `fix/convite-sessao-inicio`): sem sessão, a primeira carga recebia 401 do refresh e tratava isso como encerramento, que limpava o cache do TanStack Query e derrubava a consulta que a tela de link já tinha feito. Agora só o fim de uma sessão ativa é `encerramento` (limpa o cache e avisa as outras abas); abrir sem sessão fica `anonima` com motivo `inicio`, sem limpar nem avisar. Cobre também `/convite`, `/portal/convite` e `/conta/confirmacao` (que confirmava duas vezes). Testes de regressão em `features/sessao/linksSemSessao.test.tsx`.
- **F14s — empresa de demonstração** (#182, último PR do F14): na área da Nebrys (`/nebrys/demonstracao`), criar a empresa de demonstração com os dados fictícios do manual (o mesmo `demonstracao.json`), ATIVA e fora do ciclo de situação, que nunca vence nem é expurgada; restaurar volta aos dados iniciais sem tocar outra empresa; indicar quem entra nela como Dono, só com conta que já existe (sem conta: recusa no campo, nada criado). Nada sai dela por e-mail: convite e link por e-mail → `422 demonstracao-sem-envio`, link sem e-mail é emitido, avisos descartados (F14-87 a F14-90, F14-110). `/auth/eu` ganha `tenant.demonstracao`, e a casca mostra a faixa da demonstração. Sem migration (a marca veio no F14l); OpenAPI (`nebrys.json`, `interno.json`) e tipos atualizados à mão e com `pnpm api:tipos`. Spec do F14 com status Concluída.

## Marco concluído

- **F14 — situação da empresa e cadastro controlado** (#182; [spec](specs/F14-lancamento-controlado/spec.md), [design](specs/F14-lancamento-controlado/design.md)): G2 com o PR #186; ADRs 0014 e 0015 aceitos. 113 cenários em 19 PRs (F14a a F14s), todos com teste; o F14s fecha o marco quando for mesclado. Primeiro marco conduzido pelo `/marco`.

## Onde o produto está

- Backend do v1 completo; frontend F1–F7 e marcos F8–F13 na `main`; F14 na `main` de F14a a F14r (o F14s fecha o marco).
- **Produção no ar** desde o primeiro deploy (12/11). Conferidos pelo Daniel em 12 e 13/11: F8-22, F8-26, F8-32 (fumaça de ponta a ponta), F9-47 (Turnstile no cadastro), F13-31 (avisos do deploy), WIF restrito, tokens do R2 separados, Resend verificado, MFA nas cinco contas, alerta de orçamento (R$ 10/mês, 50/90/100%), token de origem (404 direto, 200 pela borda), CORS só para `acordu.app.br` e `TempoReal__Sse=false`.

## Próximos passos

1. Merge do F14s, que conclui o F14; depois, escolher o próximo marco (`/roadmap`, G1). Para usar a demonstração em produção: deploy com o F14s, criar em `/nebrys/demonstracao` e indicar a própria conta. **Com o F14q na `main`, abrir o cadastro por convite é trocar a variável `CADASTRO_MODO` do GitHub para `convite` e fazer um deploy (G4)**: `/cadastro` passa a mostrar a lista de espera, e só quem recebe o link da Nebrys cria empresa. Com `aberto`, o cadastro de hoje segue. Ligar `TEXTOS_LEGAIS_PUBLICADOS` só depois da #194, num deploy fora do horário de uso (deploy-ci §7). Email Routing de `contato@acordu.app.br` ativo e testado pelo Daniel em 16/11.
2. Conferências que faltam da produção:
   - deploy: F8-27 e F8-28 (rollback por migration e por health), F8-29 (jobs do Scheduler), hostname do Turnstile;
   - backup: F8-30 e F13-32 na primeira madrugada com o `BACKUP_ATIVO`; F8-31 (restore) antes do primeiro cliente;
   - monitoramento: F13-02 (Error Reporting), trace do F13a, pilhas do Firefox e do Safari (F13d), vigia e KV (F13h);
   - riscos (deploy-ci §10): `max-instances`, limpeza do Artifact Registry e lifecycle do bucket, `acordu_app` sem DDL e autoscaling do Neon;
   - arquivos órfãos no storage se o processo cair entre o commit do expurgo e a remoção (F14i): limpeza por prefixo `{tenantId}/` como melhoria.
3. Política de privacidade e termos de uso: rascunho na `main` (F14f, #193); marcadores para depois do registro da Nebrys (#194).
4. Expurgo dos convites de empresa e das entradas convidadas da lista de espera (retenção dos e-mails): #213.

## Para o Daniel confirmar

- F14s: há uma demonstração só; as contas fictícias nascem sem senha (ninguém entra com elas), e a pessoa indicada é Dona com todas as permissões na demonstração (pode mudar e apagar dados, mas nada sai por e-mail; restaurar volta tudo). A restauração mantém as sessões abertas nela e as pessoas indicadas; uma pessoa indicada que se removeu da equipe da demonstração deixa de ser indicada. Duas criações ao mesmo tempo: a segunda falha (transação serializável), sem segunda demonstração. Se preferir outro comportamento em algum desses pontos, é ajuste pequeno.
- F14s, escolha fora do design: a demonstração nasce com o envio liberado (sem isso as telas desabilitam convites com "confirme o e-mail"); o bloqueio olha a marca antes da liberação, então nada sai. Recomendação: manter.
- F14s, escolha fora do design: a criação e a restauração recusam com 422 se houver conta com senha num dos e-mails fictícios (senão essa pessoa entraria como da equipe). Recomendação: manter.
- F14s, fora dos cenários: `DELETE /nebrys/demonstracao/indicacoes/{usuarioId}` e a ação "Tirar" na tela, para desfazer uma indicação. Recomendação: manter; alternativa: tirar.
- F14s, fora do design: `EmpresaNebrysDto` não traz a marca de demonstração, então na lista da área ela aparece como ATIVA comum e o detalhe oferece ações que dão 422. Recomendação: incluir a marca depois, ajustando o teste do F14-62.

- F14r: "sem dado" para oferecer os exemplos é a regra do expurgo de cadastro, então convidar alguém para a equipe (vínculo de outra pessoa) já tira a oferta. O expurgo do cadastro abandonado continua contando os exemplos como dado (uma empresa só com exemplos não sai em 7 dias; sai pelo expurgo das só leitura). Se preferir, os dois passam a ignorar os exemplos.

- F14p: a Nebrys ainda emite convite de empresa para e-mail de domínio descartável, e o cadastro com esse convite é recusado no e-mail (F14-60 vale com ou sem convite). Se preferir, a emissão na área passa a recusar também (melhoria pequena).

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
