# Notas do desenvolvedor (F14o, etapa 4)

Premissas técnicas do F14o (F14-52 a F14-55, F14-75 a F14-77; D12, D14; ADR 0014 itens 6 e 7). Nenhuma muda cenário nem decisão do design.

## Ambiente e o que falta gerar

- Sessão sem SDK .NET: nada de C# foi compilado nem rodado aqui. Domain, Application, Infrastructure, Api e todos os testes .NET só rodam na CI. Assinaturas conferidas por leitura do código.
- **Migration pelo bot:** `.github/migration-request` com `ListaDeEspera`. O que ela precisa gerar (aditiva, sem preenchimento): tabela `entradas_lista_espera` (`id` uuid PK, `nome` varchar(200), `email` varchar(254) com índice único `ix_entradas_lista_espera_email`, `nome_empresa` varchar(200), `mensagem` varchar(1000) nula, `criada_em`, `convidada_em` nula, `xmin` como versão) e, em `convites_empresa`, a coluna `entrada_lista_espera_id` uuid nula com índice e chave estrangeira `ON DELETE SET NULL`. Até o bot gerar, os testes que sobem o banco falham. Localmente: `dotnet tool restore && dotnet ef migrations add ListaDeEspera --project src/Acordu.Infrastructure --startup-project src/Acordu.Api --output-dir Persistence/Migrations`.
- **OpenAPI à mão**, no formato do gerado (conferido: o `json.dumps` com indentação 2 refaz os dois arquivos byte a byte; schemas em ordem ordinal; caminhos na ordem do registro). `interno.json`: `POST /api/v1/lista-espera` logo depois de `/erros-web` (rota direta no grupo `/api/v1`, mesma fonte de endpoints), tag `Operação` (a seção do contrato é a §16; evita mexer na ordem das tags do documento), `ListaEsperaRequest`. `nebrys.json`: `/lista-espera`, `/lista-espera/{id}/convite`, `/lista-espera/{id}`, `EntradaListaEsperaNebrysDto`, `ResultadoPaginadoOfEntradaListaEsperaNebrysDto`, `SituacaoEntradaListaEspera`. Tipos do SPA regerados com `pnpm api:tipos`. Se o `ContratoOpenApiTests` acusar diferença, regenerar depois da migration (`ACORDU_ATUALIZAR_OPENAPI=1 dotnet test --filter ContratoOpenApiTests` e `pnpm api:tipos`, ou `.github/openapi-request`).

## Rota pública (F14-52 a F14-55)

- `POST /api/v1/lista-espera` `{ nome, email, nomeEmpresa, mensagem?, tokenTurnstile? }` → **`202` sem corpo** nos dois caminhos (nova ou repetida), como o `senha/esqueci` (rota que também não pode revelar se o e-mail existe). Sem `Location`, sem id.
- **Turnstile com ação por rota:** `IVerificacaoHumana.VerificarAsync(token, ip, acao)`; `AcoesVerificacaoHumana.Registro` (`registro`) e `.ListaEspera` (`lista-espera`). O `OpcoesTurnstile.Acao` fixo saiu. O `400` em `errors.tokenTurnstile` virou o `DesafioHumano.ExigirAsync` da Api, usado pelo cadastro e pela lista (mesmas mensagens). Registrado como convenção (§3). O widget do F14q precisa mandar `action: 'lista-espera'`.
- **Limite:** política `lista-espera` (`RateLimit__ListaEspera__PorHora`, padrão 3, por IP com IPv6 por /64, `ChaveDoIp`), separada da do cadastro. Recusas contam (o limitador vem antes do corpo).
- **Validação:** no domínio (`EntradaListaEspera.Registrar`), na ordem do formulário: `nome`, `email`, `nomeEmpresa`, `mensagem`; o primeiro que falha volta em `errors`. Mensagens próprias para obrigatório vazio ("Informe o seu nome." etc.); e-mail pela `Common.Email.Normalizar`. Limites: nome e empresa 200 (`LimitesTexto.Nome`), mensagem 1.000 (`LimitesTexto.MensagemListaEspera`, novo).
- **"Não duplica" (F14-53):** o `RegistroListaEspera` insere direto e deixa o índice único decidir: o `23505` do índice (nome conferido) tira a entrada do contexto e a resposta segue igual. Não lê antes, então a corrida entre envios simultâneos também cai no índice. A entrada que já estava **não muda** (nome, empresa e mensagem novos são ignorados). Premissa: atualizar a mensagem de quem reenvia seria útil, mas abriria a escrita em dado de outra pessoa a quem só sabe o e-mail.
- A inscrição grava pelo `SaveChanges` direto (sem a unidade de trabalho): não há trilha (rota pública, sem operador) nem evento.
- Nenhum e-mail sai para quem entrou na lista (fora dos cenários; o aviso à Nebrys está fora de escopo na spec).

## Área da Nebrys (F14-75 a F14-77)

- `GET /nebrys/lista-espera` (paginada, `criada_em asc, id asc`: ordem de chegada), com `situacao` calculada (`AGUARDANDO`/`CONVIDADA` pela `ConvidadaEm`) e `conviteId` (o convite mais recente com essa origem, nulo se não há). Sem detalhe por id (a tela do F14q usa a lista); sem filtro por situação.
- `POST /nebrys/lista-espera/{id}/convite` → `201` com o corpo e o `Location` da emissão (`EnvioConviteEmpresaDto`). Passa pelo `ConvitesEmpresaNebrys.EmitirDaListaAsync`: a mesma emissão do F14n (validade, link, e-mail depois do commit, `emailEnviado`), com a **entrada de origem** gravada no convite (`ConviteEmpresa.EntradaListaEsperaId`, D12) e a entrada marcada `CONVIDADA` na mesma gravação.
  - Entrada já convidada → `422 transicao-invalida` (conferida antes do convite em aberto, para a mensagem ser a da entrada). Premissa: para convidar de novo quem já foi convidado, a Nebrys reenvia ou revoga o convite (lista de convites) ou emite direto para o e-mail.
  - E-mail com convite em aberto (regra do F14n) → `422`, e a entrada continua aguardando.
- `DELETE /nebrys/lista-espera/{id}` → `204`: apaga a entrada, aguardando ou convidada. A chave estrangeira `convites_empresa.entrada_lista_espera_id` é `ON DELETE SET NULL`: o convite fica, sem a origem, e continua valendo. Depois do descarte, o mesmo e-mail pode entrar de novo na lista.
- `xmin` como versão na entrada: convidar e descartar ao mesmo tempo → o segundo recebe `409 conflito-versao`. Sem `If-Match` (como os convites do F14n: a convenção do `If-Match` da área é para a situação da empresa).
- **Trilha (D14):** `nebrys.lista.convite` (alvo `lista-espera`, `dados`: `email`, `antes.situacao`, `depois` com `situacao`, `convidadaEm`, `conviteId`) e, junto, a `nebrys.convite.emissao` do convite com `entradaListaEspera`; `nebrys.lista.descarte` (`dados` só com o `email` e o `antes`: `situacao`, `criadaEm`, `convidadaEm`). O nome, a empresa e a mensagem não vão para a trilha (a entrada não fica guardada sem motivo, F14-77). A conferência de todas as ações é do F14p (F14-70).

## Arquitetura

- `EntradaListaEspera` (Domain), tabela global `entradas_lista_espera` aplicada pelo `Aplicar` estático (`Persistence.ListaEspera`). Teste `OperacaoTests.EntradaListaEspera_SoAListaDeEsperaEAAreaDaNebrysUsam` libera `Application.ListaEspera` (inscrição pública), `Persistence.ListaEspera`, `Application.Nebrys`, `Persistence.Nebrys`, as migrations e **só o tipo `ConviteEmpresaConfiguration`** (a chave estrangeira da origem). `EntradaListaEspera_SemReferenciaAEmpresa` fixa as propriedades; `ConviteEmpresa_SemReferenciaAEmpresa` ganhou `EntradaListaEsperaId`.
- Portas: `Application.ListaEspera.IInscricoesListaEspera` (grava a entrada nova) e `Application.Nebrys.IListaEspera` (obter e remover), as duas no `RegistroListaEspera`.
- `AreaNebrysTests`: rotas novas no `DocumentoNebrys_SoAsRotasDaArea` e no `RotasDaArea` (F14-63). `LimiteRenovacaoTests.Rota_UsaOLimiteCerto` com a política da lista. Fixture com `RateLimit:ListaEspera:PorHora` alto.

## Para o Daniel

- **Retenção da entrada convidada:** ela fica (aparece como convidada, F14-75) até a Nebrys descartar. A política de privacidade (F14f) diz "a entrada fica até a Nebrys convidar a pessoa ou descartar a entrada": pode ser lida como "some ao convidar". Sugestão: ajustar a frase na revisão dos textos (#194) para "até a Nebrys descartar; convidada, fica com o convite", ou incluir as entradas convidadas no expurgo dos convites (#213). Nada mudou no texto aqui (mudar o texto pede versão nova, convenções §4).
- E-mail repetido na lista não atualiza a entrada (premissa acima).

## Documentos

Contrato §2 (link para a lista), §11a, §14, §16 (rota pública) e §17 (lista na área e trilha); segurança §4, §5.5, §8 e §9; modelo de domínio §4.14 (`EntradaListaEspera`, origem do `ConviteEmpresa`); read side §8a; deploy-ci §7 (`RateLimit__ListaEspera__PorHora`, opcional); setup da infra (Turnstile serve à lista sem mudança no painel); convenções §3 (Turnstile por rota); estado.md. ERS sem mudança (RF-AUT-08 já descreve a lista). Sem variável obrigatória nova no `deploy.yml`.
