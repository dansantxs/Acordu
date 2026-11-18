# Notas do testador (F14h, etapa 5)

Cenários F14-79 a F14-83 e F14-114 a F14-121. Coluna Teste da spec preenchida.

## Testes escritos

- `Api.FunctionalTests/TextosLegaisTests`: `Portal_TextosNaoPublicados_ContatoSemAceiteSemPendentesECienteRecusado` (F14-120 com os textos escondidos, convite do portal mandando versões: nada gravado), `AceiteConviteEquipe_PublicadosComVersaoVencida_400ENadaCriado` (D10 no convite: 400 no campo, sem vínculo nem conta), `Ciente_DeUmaConta_NaoAfetaOutra` (isolamento: aceite é da conta).
- Apagada com a conta (D10, pedido do revisor): `ExpurgoEmpresasTests` › `Diario_ContaComAceitesSoDaEmpresaExpurgada_AceitesSaemComAConta` e `Diario_ContaComVinculoEmOutraEmpresa_MantemOsAceites`; `ExpurgoCadastrosTests` › `Diario_CadastroAbandonadoComAceites_AceitesSaemComAConta` e `Diario_CriadorComVinculoEmOutraEmpresa_MantemOsAceites`. Citados na coluna Teste do F14-81. Sem defeito de produção: a FK em cascata apaga os aceites da conta expurgada e a conta com rastro fora mantém os dela.
- `scripts/conferir-fabrica.test.mjs` › F14-121: lê o `deploy.yml` e confere que o passo "Variáveis da API" tem `id`, normaliza (vazio → `false`, só `true`/`false`, outro valor para o deploy antes de publicar), grava o mesmo valor em `TextosLegais__Publicados` e na saída do passo, e que o build do SPA lê a saída (nunca `vars.` direto). Roda na CI (job "Ações", `node --test`). O que continua só no Actions: o valor de fato chegar aos dois destinos; conferir no primeiro deploy com a variável, fora do horário de uso.

## Ambiente

- `pnpm test` tem 3 falhas antigas desta máquina (anexo em multipart em `nova.test.tsx` e `conversa.test.tsx`; logotipo grande em `configuracoes.test.tsx`), fora do escopo.
- Os testes do estado desligado sobem outra Api sobre o mesmo banco (`NovaApiAsync`).

## Observações

- O `ciente` não recebe as versões no corpo (premissa do desenvolvedor); aba antiga do SPA depois de um deploy com versão nova grava a vigente sem ter mostrado a página. Aceito pelo desenvolvedor; vai para a descrição do PR.
