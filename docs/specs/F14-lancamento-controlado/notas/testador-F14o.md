# Notas do testador (F14o, etapa 5)

Sem SDK .NET na sessão: nada compilado nem rodado; os testes rodam na CI.

Revisão: os sete cenários (F14-52 a F14-55, F14-75 a F14-77) têm teste que verifica o "então": Turnstile com a ação `lista-espera` (token do cadastro recusado e vice-versa; o cadastro segue com `registro` em ProtecaoCadastroTests), 429 por IP com cota separada, resposta igual nos dois caminhos, 404 fora da operação (inclusive RotasDaArea e DocumentoNebrys_SoAsRotasDaArea), convite com origem gravada, segundo convite 422, descarte de convidada com convite sem origem.

Lacuna fechada: `Trilha_ConvidarEDescartarConvidada_SemNomeEmpresaNemMensagem` (trilha `nebrys.*` do convite e do descarte da convidada sem dado sensível). Coluna Teste da spec atualizada em F14-77.

Observação: a entrada convidada não ganha trilha de "convite sem origem" ao descartar além do `antes.situacao`; aceitável pelo design.
