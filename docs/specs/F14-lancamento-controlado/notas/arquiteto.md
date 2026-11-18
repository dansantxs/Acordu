# Notas do arquiteto (F14, etapa 3)

Pontos do código que o design usa, para quem implementar. Sem decisão nova aqui: as decisões estão no design.md.

- Guarda de gravação: `AcorduDbContext.GarantirIsolamento` (SaveChanges). A de só leitura fica ao lado (ADR 0015). Gravações fora do change tracker hoje: `RegistroSeguidores` (seguir, da pessoa, D4), `ConsultasNotificacoes` (marcar o sino lido), `ServicoAutenticacao` (liberação do envio, F9a), `RegistroSugestoes` e os expurgos. Nenhuma é gravação de negócio da empresa.
- Jobs por tenant: todos passam por `Infrastructure/Agendamento/ExecutorPorTenant` (recorrências, alertas de SLA, cronômetros esquecidos, fechamento automático). Filtrar as empresas graváveis ali cobre o F14-22 num ponto só. O `ProcessadorOutbox` define o contexto por mensagem (`DefinirEquipe(mensagem.TenantId)`): o descarte da empresa só leitura entra antes de chamar os manipuladores.
- Avisos automáticos: `Application/Common/EnvioATerceiros.PodeAvisarAsync` (F9b) é o portão de `AvisosAoContato` e `AvisoInterno`. A situação e a demonstração entram ali.
- Recorrências: `GeracaoRecorrencias` já pula as datas sem gerar quando o cliente está inativo. O `GravavelDesde` segue o mesmo caminho (F14-24).
- Expurgo: `Persistence/Cadastros/ExpurgoCadastros` (transação serializável, `TiposComTenant`, `TemRastroForaAsync`, sugestões anonimizadas, logotipo depois do commit). O `ApagadorDeEmpresa` (D6) sai daqui. Hoje só o criador é avaliado para apagar a conta; no F14 são todas as pessoas com vínculo na empresa.
- Diário: `Api/Jobs/JobsEndpoints.MapDiario` e `PassosDiario`. As contagens vão por índice para o resumo do Discord (`AvisosDosJobs.DiarioAsync`): incluir os passos novos nos dois lugares.
- Documento: `Domain/Common/Documento.Normalizar` (CPF e CNPJ alfanumérico) já existe e é `internal`. O cadastro precisa dele pela Application (expor um VO ou um método público).
- Turnstile: `Infrastructure/Seguranca/VerificadorTurnstile` confere a ação `registro`. A lista de espera precisa da ação como parâmetro.
- Dados de demonstração: `apps/web/ajuda/demonstracao.ts` (509 linhas) cria tudo pela API, com equipe por convite lido no Mailpit. O JSON do D13 precisa das mesmas entidades com datas relativas ao dia.
- Front: as faixas do F9 (`casca.test.tsx`, empresa não liberada) são o modelo das faixas de situação. Os botões desabilitados com `aria-describedby` (F9e) são o modelo do `podeGravar`.
- Testes de arquitetura das tabelas globais: `tests/Acordu.ArchitectureTests/OperacaoTests.cs`. O da `Sugestao` libera o `ExpurgoCadastros` pelo nome (`^ExpurgoCadastros`) e passa a liberar o `ApagadorDeEmpresa` no F14i. As tabelas novas do F14 ganham testes no mesmo molde (design D4).
- Sessão da empresa apagada: o F9 já cobre (`ExpurgoCadastrosTests › Refresh_DepoisDoExpurgo_Retorna401` e `Eu_ComAccessTokenDaEmpresaExpurgada_Retorna401`). O F14-33 reaproveita o mesmo caminho.

## 18/11 — aceite atrás dos textos publicados (D16, F14h)

Pontos para quem implementar o F14h; a decisão está no D16 do design.

- `deploy.yml`: hoje o passo "SPA (build)" lê `vars.TEXTOS_LEGAIS_PUBLICADOS` direto. Passa a ler a saída do passo "Variáveis da API" (dar `id` ao passo e gravar o valor normalizado em `$GITHUB_OUTPUT`), e o mesmo valor entra no dicionário `variaveis` como `TextosLegais__Publicados`. Valor inválido entra na lista `faltam`, com o nome da variável.
- Ordem do deploy (revisão, health, borda, build, Pages): a API sobe antes do SPA. É a origem da janela da virada do D16; não mudar a ordem neste PR.
- API: `OpcoesTextosLegais` na Application, composta na `ComposicaoApi` como as `OpcoesEmpresas` (o helper de lá não sobe com valor inválido). `app.Configuration.GetValue("TempoReal:Sse", true)` é o precedente de booleano, mas sem a validação: aqui a validação conta para o F14-121.
- Front: `TEXTOS_LEGAIS_PUBLICADOS` de `ambiente.ts` já existe; a caixa de aceite e o aviso de "ciente" passam pelo mesmo ponto que o `LinksLegais` e o `exigirTextosLegaisPublicados`. O `legalEscondido.test.tsx` é o modelo dos testes do estado desligado.
- Portal (F14-120): `/auth/portal/convite` e `/portal/eu` não ganham nada; o teste confere que nada é gravado em `aceites_textos` pelas rotas do contato, com a configuração ligada.
- F14-119 sem migration: os pendentes saem da comparação com a constante `TextosLegais`, e a falta de linha conta como pendente.
