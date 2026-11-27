# Notas do testador — F15e

Cobertura do desenvolvedor conferida cenário a cenário (F15-85, F15-87, F15-88, F15-108; D11, D12, D16): os testes verificam o "então" (IGNORADO, nada criado nem respondido, desfecho no log, log sem endereço/assunto/domínio, expurgo em gravável e só leitura, duas empresas, retenção configurada, teste de arquitetura).

Testes acrescentados:
- Api `EntradaEmailTests`: `Entregar_EnvelopeVazioSemNada_DevolucaoDescartada` (X-Email-Remetente presente e vazio) e `Entregar_SemCabecalhoDeEnvelope_SegueComoEmailComum` (ausente não é devolução); o helper `EntregarAsync` aceita envelope nulo.
- Api `AvisosDosJobsTests.Diario_SemFalhas_ResumoEmDeployUmaVezPorDia`: ordem dos passos (atividades, e-mails recebidos, cadastros) e `emailsRecebidosRemovidos` no resumo do #deploy.

Ressalvas: sem SDK .NET, nada compilado nem rodado (só a CI). Um cabeçalho vazio via TestHost pode ser entregue como ausente; se o teste do envelope vazio falhar na CI, é a ponte de teste, não o código. O `removidos` dos testes de integração e do diário usa `>=` porque o banco é compartilhado. `noreply` como "sistema de entrega" é leitura do desenvolvedor (ver nota dele). Rodados: `conferir-specs` ok, `node --test scripts/*.test.mjs` 19/19.

- Depois da CI (orquestrador): `Entregar_EnvelopeVazioSemNada_DevolucaoDescartada` falhou como previsto, porque o TestHost entrega o cabeçalho vazio como ausente; o mesmo pode acontecer na borda. O Worker passa a mandar o envelope vazio como `<>` (com teste em `index.test.ts`), e o teste da Api saiu: o caso `<>` da theory `Entregar_EmailAutomatico_...` cobre a devolução.
