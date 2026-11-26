# Notas do testador — F15b (etapa 5)

Cenários F15-01, F15-03, F15-04, F15-06, F15-07, F15-09, F15-93 a F15-96. Sessão sem SDK .NET: testes C# só rodam na CI.

- O desenvolvedor já cobria o "então" de cada cenário (funcional da API, caso de uso, domínio, opções, script da migration, trilha, front). Li as assertivas e não achei fraca: 403 com `type`, 422 com `type`, nada muda conferido por releitura e pela trilha, isolamento entre tenants (A liga, B não), permissão antes de só leitura e de domínio ausente.
- Acrescentei em `RecebimentoEmailTests` `EditarEmpresa_RecebimentoLigado_RespostaTrazOEnderecoESegueLigado`: o `PUT /configuracoes/empresa` devolve o endereço e o recebimento, e editar a empresa não desliga nem vai à trilha do recebimento (F15-95).
- Coluna Teste: F15-95 ganhou o teste novo; as demais já estavam preenchidas e conferem com os nomes existentes.
- Ressalvas: (1) nada testa que o `Up` gerado pelo bot chama `ScriptsDeDados.PreencherCodigoEntradaDasEmpresas`; conferir na revisão da migration (a linha vem depois do `CreateIndex`). (2) O laço de `ServicoAutenticacao` em si segue sem teste; `MigrationsTests.CodigoDeEntrada_ColisaoNaTransacao_SorteiaOutroEGravaSemPerderOAnterior` reproduz o mecanismo (transação, gravação anterior, colisão, `SortearOutroCodigoEntrada`, nova gravação, commit) sem gancho de produção. Premissa não validada sem SDK: o savepoint automático do EF mantém a gravação anterior e o contexto Sistema aceita `Add` de Tenant. (3) Corpo com `ligado` de tipo errado (string) não testado: depende do tratamento global de JSON inválido.
- Front: `pnpm test` (775 ok), `pnpm lint`, `pnpm format:check`; `conferir-specs` e `node --test scripts/*.test.mjs` (17 ok) verdes.
- Não validado: compilação e execução dos testes C#.
