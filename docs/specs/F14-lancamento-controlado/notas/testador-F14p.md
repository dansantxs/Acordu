# Notas do testador (F14p, etapa 5)

Sem SDK .NET na sessão: nada foi compilado nem rodado aqui; os testes só rodam na CI. Conferido por leitura (usings, nomes da `ApiFixture`, `OperadorNebrys`, `ConviteEmpresa.Email/UsadoEm/Pendente`). `node scripts/conferir-specs.mjs`: Specs ok.

## Cobertura por cenário

- F14-50: `CadastroAbertoTests` cobre o 201 (TESTE, Dono, confirmação, documento e aceite) e cada regra do F14 recusando no campo. Sem lacuna.
- F14-60: domínio, subdomínio, maiúsculas, IDN (`xn--`, com acento), ponto final, e-mail comum passando, comentários e linhas vazias, arquivo embutido carregando: cobertos (`DominiosDescartaveisTests`, `EmailDescartavelTests`, unitário da inscrição). Lista de espera recusa sem gravar, mesma mensagem nos dois (`MensagensDeCadastro.EmailDescartavel`): coberto.
- Lacuna achada e coberta: "antes do convite ser consumido" só era testado com token inexistente. Novos testes em `EmailDescartavelTests`:
  - `Registro_ConviteValidoComEmailDescartavel_400NoEmailEOConviteContinuaPendente` (modo convite e modo aberto): convite real emitido para e-mail descartável, 400 no `email`, sem erro em `convite`, contagens iguais e convite com `UsadoEm` nulo e `Pendente`.
  - `Registro_ModoConviteSemConviteComEmailDescartavel_400NoEmail`: sem convite no modo convite, o 400 é do e-mail, não do convite.
- F14-70: as 10 ações (4 de empresa, liberação de documento, 3 de convite, 2 de lista) estão no `Acoes` do `TrilhaNebrysTests`; os dois testes de cobertura (constantes `nebrys.*` menos as da sessão; rotas não GET do `openapi/nebrys.json` menos a sessão) quebram com ação ou rota nova sem caso. Sem lacuna.

## Observações para a descrição do PR

- Licença da lista: CC0 1.0 (o PR precisa citar).
- A emissão de convite de empresa na área não confere a lista (premissa do desenvolvedor, ponto para o Daniel); o teste novo documenta que o cadastro recusa depois.
- Lista de espera com descartável responde 400 (não o 202 do F14-53), por decisão do desenvolvedor.
- Tudo não compilado localmente: a CI é a primeira compilação.
