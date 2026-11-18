# Notas do testador (F14k, etapa 5)

Parecer: aprovado. Nenhum defeito no código de produção.

Testes que faltavam e foram acrescentados em `AreaNebrysTests` (só teste):
- F14-63 "outro papel": as sessões da comparação com a rota inexistente passaram a incluir o Membro; novos métodos/caminhos na teoria (PUT, PATCH, HEAD, OPTIONS, POST na coleção, barra final, caixa mista no id).
- F14-63: `POST /nebrys/sessao` pedido por Dono, Membro, contato, link e anônimo dá o mesmo 404 de rota inexistente.
- F14-61: criador, equipe e ausência de expurgo/cancelamento nas situações ATIVA e CANCELADA (antes só TESTE).
- Trilha: motivos `email-desconhecido`, `email-nao-confirmado` e `senha` em `nebrys.sessao.falha` sem tenant (antes só `nao-operador` e `bloqueado`).

Observações para a descrição do PR: nenhuma bloqueante. Cuidado de teste: errar a senha 5 vezes na conta operadora compartilhada da fixture a bloqueia e derruba os demais testes; os testes novos usam e-mail inexistente para isso.

## Rodada 1 (hash descartável)

`SessaoOperacaoTempoTests › AbrirSessao_TodaRecusa_ConfereUmHashDeSenha` troca o `IPasswordHasher` por um que conta `VerifyHashedPassword` e exige exatamente 1 conferência, com 404, em quatro recusas: e-mail sem conta, senha vazia, conta bloqueada e senha errada. Cobre os caminhos que o ajuste mudou. `nao-operador` e `email-nao-confirmado` só ocorrem depois do `CheckPasswordAsync`, então já custam o hash real (sem teste de contagem próprio; ressalva menor). Os testes de `AreaNebrys` seguem valendo. Citado na coluna Teste do F14-63. Parecer mantido: aprovado.
