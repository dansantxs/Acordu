# Notas do testador — F15d

Sem SDK .NET: testes C# só validados na CI (lidos com cuidado). `conferir-specs` ok; `node --test scripts/*.test.mjs` 19 verdes; Worker `pnpm typecheck` ok e `pnpm test` 21 verdes.

Cobertura: F15-05, F15-19, F15-22 e F15-89 já tinham testes que verificam o "então" (Api, unidade, integração, arquitetura e Worker); a coluna Teste do desenvolvedor confere com o código. Isolamento por tenant do `EmailRecebido` coberto em `IsolamentoTenantTests`, `IsolamentoClienteTests` e na integração (mesma chave em outra empresa).

Lacunas cobertas em `tests/Acordu.Api.FunctionalTests/EntradaEmailTests.cs`:
- `Entregar_AcimaDoTamanhoSemTokenCerto_404NaoRevelaOLimite` (token antes do tamanho);
- `Entregar_EmPartesAcimaDoTamanhoParaDestinoDescartado_202SemLerNemGravar` (premissa do D12: desligada e desconhecida não leem o corpo);
- `Entregar_MesmaEntregaEmParalelo_TodasAceitasEUmRegistroSo` (corrida real: nunca 5xx, um registro);
- `Entregar_CorpoQueNaoEEmail_202SemCorpo` (menor 6 com lixo não vazio, não só corpo vazio).

Ressalvas:
- O lixo não vazio pode ser aceito pelo MimeKit como mensagem sem cabeçalhos (vira `IGNORADO` em vez de `Malformada`); o teste só afirma `202`, o que o plano exige.
- O caminho "empresa ficou só leitura entre a consulta e o commit" (`EmpresaSomenteLeituraException` → `SomenteLeitura`) só tem a guarda da persistência testada (`EmailRecebido_EmpresaSoLeitura_GuardaRecusa`), não o `RecebedorDeEmail` com a corrida.
- Log sem endereço na API não tem teste (só o do Worker).
- Migration `EmailsRecebidos` (bot) e o comportamento do `setReject` e do carimbo no Email Routing real ficam para a CI e o provisionamento.
