# Notas do testador — F15a (etapa 5)

Cenários F15-60, F15-61, F15-62. Sessão sem SDK .NET: testes C# só rodam na CI.

- O desenvolvedor já cobria o "então" de cada cenário (funcional da API da equipe e do portal, domínio, persistência, script da migration, front). Li e não achei assertiva fraca.
- Acrescentei em `CanalDasMensagensTests`: abertura no portal com anexo grava a mensagem como PORTAL (equipe e portal veem); contato de outro cliente recebe 404 na conversa (isolamento). Em `PortalLinkTests.Emitir_Trocar_EUsarAsRotasDoLink`: resposta pela sessão de link traz `canal = PORTAL` (resposta e leitura da equipe).
- Spec: coluna Teste do F15-61 ganhou os dois testes novos.
- Migration: o teste F15-62 roda `ScriptsDeDados.PreencherCanalDasInteracoes` direto (zera `canal` e confere), então independe de a linha ser acrescentada ao `Up`. Ressalva: nada testa que o `Up` gerado pelo bot chama o script; conferir na revisão da migration (`defaultValue: ""` e `migrationBuilder.Sql(ScriptsDeDados.PreencherCanalDasInteracoes)`).
- Front: `pnpm test` (773 ok), `pnpm lint`, `pnpm format:check` e `conferir-specs` verdes.
- Não validado: compilação e execução dos testes C# (CA1305/CA1859 etc.); os novos seguem o padrão dos vizinhos.
