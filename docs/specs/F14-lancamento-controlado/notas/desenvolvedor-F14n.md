# Notas do desenvolvedor (F14n, etapa 4)

Premissas técnicas do F14n (F14-45, F14-47 a F14-49, F14-51, F14-72 a F14-74, F14-105; D11, D12, D14; ADR 0014 itens 6 e 7). Nenhuma muda cenário nem decisão do design.

## Ambiente e o que falta gerar

- Sessão sem SDK .NET: nada de C# foi compilado nem rodado aqui. Infrastructure, Api e todos os testes .NET só rodam na CI. Assinaturas conferidas por leitura do código.
- **Migration pelo bot:** `.github/migration-request` com `ConvitesDeEmpresa` (tabela `convites_empresa`: `id` uuid PK, `email` varchar(254), `token_hash` varchar(64) com índice único, `criado_em`, `enviado_em`, `expira_em`, `usado_em`, `revogado_em`, índice em `email`, `xmin` como versão). Aditiva, sem preenchimento. Até o bot gerar, os testes que sobem o banco falham (modelo com tabela que o banco não tem). Localmente: `dotnet tool restore && dotnet ef migrations add ConvitesDeEmpresa --project src/Acordu.Infrastructure --startup-project src/Acordu.Api --output-dir Persistence/Migrations`.
- **OpenAPI à mão**, no formato do gerado (mesma indentação, schemas em ordem alfabética, caminhos na ordem do registro): `interno.json` (`RegistroRequest.convite`, `GET /auth/cadastro`, `POST /auth/registro/convite/consulta`, `CadastroInfo`, `ConviteEmpresaInfo`, `ModoCadastro`) e `nebrys.json` (rotas de convites e os esquemas). Tipos do SPA regerados com `pnpm api:tipos` a partir desses arquivos. Se o `ContratoOpenApiTests` acusar diferença, regenerar depois da migration: `ACORDU_ATUALIZAR_OPENAPI=1 dotnet test --filter ContratoOpenApiTests` e `cd apps/web && pnpm api:tipos` (ou `.github/openapi-request`).

## Modo do cadastro (D11)

- `OpcoesCadastro` (Application/Cadastro): `Cadastro__Modo` (`convite`/`aberto`, aparado, sem diferenciar maiúsculas; ausente → `convite`; outro valor impede a subida) e `Cadastro__DiasConvite` (padrão 7; inválido impede a subida). Composta na `ComposicaoApi`, como as `OpcoesTextosLegais`.
- `GET /auth/cadastro` → `{ modo }` com o enum `ModoCadastro` (`CONVITE`/`ABERTO` no JSON, pelo conversor da Api). Pública, sem banco e sem limite.
- `appsettings.Development.json` (dev, E2E e bot do manual sobem em Development) e a `ApiFixture` definem `aberto`; a fumaça da imagem na CI sobe sem a chave (vale `convite`, e a API sobe).
- `deploy.yml`: `CADASTRO_MODO` (variável, não secret) obrigatória, `convite` ou `aberto`, conferida no passo "Variáveis da API" antes de publicar e passada como `Cadastro__Modo`. Teste em `scripts/conferir-fabrica.test.mjs`. **O Daniel cria a variável antes do próximo deploy** (estado.md).

## Registro com convite

- Corpo ganha `convite` (opcional no OpenAPI, como os campos do aceite). Ordem no `ServicoAutenticacao.RegistrarAsync`: Turnstile e limite na rota (antes de tudo, F14-51); o que não consulta o banco (agora inclui "modo convite sem convite" → `400 errors.convite`); **o convite** (inexistente/vencido/usado/revogado → `400 errors.convite`, sem distinguir; outro e-mail → `400 errors.email`, sem revelar o e-mail do convite); a conta (e-mail em uso, senha); o documento; e por fim o convite marcado como usado, gravado no mesmo `SaveChanges` da empresa.
- **Convite antes da conta:** no modo convite, quem não tem convite que vale não descobre se o e-mail tem conta. O convite válido para e-mail com conta cai no `400` do e-mail em uso (F14-105); como a transação volta, o convite continua valendo.
- **Convite presente vale em qualquer modo:** no modo aberto, um `convite` no corpo é conferido e usado do mesmo jeito (a pessoa veio do link). Premissa: um link velho num cadastro aberto recebe o `400` do convite; a tela do F14q decide se manda o campo.
- **Conta pelo convite nasce confirmada** (premissa, **para o Daniel confirmar**): o token chegou por aquele e-mail, como nos convites da equipe e do portal (segurança §4, "conta criada por convite já nasce confirmada"). A empresa sai liberada para enviar (o mesmo `ExecuteUpdate` da confirmação, na transação do cadastro) e não há e-mail de confirmação. Consequência: o expurgo de cadastro abandonado (RF-AUT-07, sem confirmação em 7 dias) não alcança quem entrou por convite. Se o Daniel preferir manter a confirmação, é tirar o `EmailConfirmed` e a liberação do ramo do convite.
- **Uso único com concorrência:** `convites_empresa.xmin` como versão. Dois cadastros com o mesmo convite trazem o mesmo e-mail, e o segundo esbarra antes no índice único da conta: o `CreateAsync` do Identity lança `DbUpdateException` (23505), que agora vira o `400` do e-mail em uso (antes era `500`; vale também para dois cadastros abertos com o mesmo e-mail). Se o convite mudar no meio (reenvio, revogação), a versão falha e a resposta é o `400` do convite.
- O cadastro usa o convite pelo `RegistroConvitesEmpresa` (`Persistence.ConvitesEmpresa`), que devolve só um resultado (`ConferenciaConvite`), o e-mail ou um booleano: o `ServicoAutenticacao` não conhece o tipo (teste `OperacaoTests.ConviteEmpresa_SoOCadastroEAAreaDaNebrysUsam`).
- `POST /auth/registro/convite/consulta` `{ token }` → `{ email, temConta }` (para o F14-46 do F14q); token que não vale → `401` sem distinguir, limite das rotas de `/auth`.
- Link do e-mail: `{App__UrlPublica}/cadastro/convite/{token}` (`EnderecosPublicos.ConviteEmpresa`). A rota do SPA é do F14q, que segue a regra de tirar o token da URL na primeira carga (convenções §4).

## Área da Nebrys (F14-72 a F14-74)

- `ConvitesEmpresaNebrys` (Application.Nebrys) com a porta `IConvitesEmpresa`; leitura pela `IConsultasNebrys` (`ListarConvitesAsync`, `ObterConviteAsync`), que ganhou o `TimeProvider` para calcular a situação (`PENDENTE`, `VENCIDO`, `USADO`, `REVOGADO`, enum `SituacaoConviteEmpresa`, não gravada).
- Rotas: `GET /nebrys/convites` (paginada, emitidos por último primeiro), `GET /nebrys/convites/{id}`, `POST /nebrys/convites` (`201`, `{ convite, emailEnviado }`), `POST …/{id}/reenvio` (`200`, mesmo corpo), `POST …/{id}/revogacao` (`200`, o convite). A lista e o detalhe não estão nos cenários; entram porque a área precisa achar o convite para reenviar e revogar (e o F14q tem as telas).
- **Um convite em aberto por e-mail** (premissa): emitir para um e-mail que já tem convite nem usado nem revogado (vencido ou não) → `422 transicao-invalida` ("reenvie ou revogue"). Evita dois links valendo para o mesmo e-mail. Sem índice único parcial: a corrida entre dois cliques da mesma operadora é desprezível no nível 0.
- **Reenviar vencido vale** (é para quem demorou); reenviar ou revogar usado ou revogado → `422`; inexistente → `404`. Sem `If-Match` (a convenção do `If-Match` da área é para as ações que mudam a situação da empresa; aqui a ação sobre um convite que mudou cai na regra).
- E-mail na requisição, depois do commit (D12), remetente o Acordu, categoria `convite-empresa`; a falha volta como `emailEnviado: false`, sem log (a Application não tem logger; como o link por solicitação). O token só existe em memória até o e-mail; a resposta nunca o traz.
- E-mail emitido para uma conta que já existe não é recusado na área: o cadastro recusa (F14-105, decisão 18).
- Trilha sem tenant (D14): `nebrys.convite.emissao`, `.reenvio`, `.revogacao`, alvo `convite` com o id, `dados` com o `email` e `situacao`/`expiraEm` antes e depois (a emissão só o depois). Nunca o token nem o hash. A conferência de todas as ações é do F14p (F14-70).

## Para os próximos PRs

- **F14o:** a "entrada de origem" do convite (D12) fica para ele, que cria a `EntradaListaEspera`: coluna nova anulável em `convites_empresa` e o "convidar a partir da entrada" usando o `ConvitesEmpresaNebrys.EmitirAsync`.
- **F14p:** mexe no mesmo `RegistrarAsync`; o e-mail descartável entra no bloco do que não consulta o banco, antes do convite. O F14-50 (aberto sem convite) já passa.
- **Retenção (para o Daniel decidir):** os convites guardam o e-mail convidado sem prazo, inclusive os usados, revogados e vencidos. Sugestão: expurgo no diário (ex.: 90 dias depois de usado, revogado ou vencido), como melhoria ou no F14p.

## Documentos

Contrato §2, §11a e §17; segurança §4, §5.5, §8 e §9; modelo de domínio §4.14 (`ConviteEmpresa`); read side §8a; deploy-ci §7; runbook ("Segredos do GitHub", `CADASTRO_MODO`); estado.md. ERS sem mudança (RF-AUT-08 já descreve os modos e o convite).
