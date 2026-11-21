# Notas do desenvolvedor (F14l, etapa 4)

Premissas técnicas do F14l (F14-65 a F14-69, F14-71, F14-106 a F14-108; D1, D5, D8, D14; ADR 0014 itens 5 e 6; ADR 0015 item 2). Nenhuma muda cenário nem decisão do design.

## Ambiente

- Sessão sem SDK .NET: nada de C# foi compilado nem rodado aqui. Infrastructure, Api e todos os testes .NET só rodam na CI. Assinaturas conferidas por grep no repositório.
- **Migration pedida ao bot:** `.github/migration-request` com `DemonstracaoDaEmpresa` (coluna `tenants.demonstracao boolean not null`, padrão `false`, sem preenchimento). Até o bot gerar, os testes que sobem o banco falham (o modelo tem a coluna e o banco não; o `MigrationsTests` acusa mudança pendente).
- **OpenAPI à mão:** `openapi/nebrys.json` editado no formato do gerado (mesma indentação, schemas em ordem alfabética, caminhos na ordem do registro). Se o `ContratoOpenApiTests` acusar diferença, regenerar com `ACORDU_ATUALIZAR_OPENAPI=1` (ou `.github/openapi-request`) depois da migration. O `interno.json` e o `portal.json` não mudam; o web não gera tipos da área ainda (F14m), então nada de `pnpm api:tipos`.

## Domínio

- **Transições no `Tenant`** (modelo §4.11.3): `Ativar(agora)`, `Prorrogar(ultimoDia, fuso, agora)`, `Cancelar(agora)`, `Reativar(agora)`, cada uma com `TransicaoInvalidaException` e a explicação fora da tabela.
- **`GravavelDesde`** (pendência do F14d): gravado com `agora` sempre que a empresa sai do só leitura (VENCIDA ativada ou prorrogada, CANCELADA reativada). Ativar ou prorrogar uma empresa em TESTE não mexe nele: ela não estava só leitura, e a regra do F14d é "datas do só leitura não geram". Testes no Domain, na integração (guarda) e nos funcionais.
- **Prorrogar:** o fim é o início de D+1 no fuso do calendário da empresa (padrão `America/Sao_Paulo`). Meia-noite inexistente por horário de verão: o primeiro instante válido do dia (avança de 30 em 30 min). "Data passada" = novo fim ≤ agora, ou seja D anterior a hoje no fuso; D = hoje vale até o fim do dia. **Premissa:** prorrogar uma empresa em TESTE para antes do fim atual (desde que no futuro) é aceito: a spec só recusa data passada, e a ação é "definir o novo fim".
- **Cancelar:** TESTE e ATIVA começam o só leitura agora; VENCIDA mantém `SomenteLeituraDesde` (design §1.2). `FimDoTeste` fica como estava.
- **Reativar e Ativar:** limpam `FimDoTeste` e `SomenteLeituraDesde`; reativar limpa também `CanceladaEm`. **Premissa:** as datas descrevem o ciclo atual (as telas mostram "data do vencimento ou cancelamento" pela situação); o histórico fica na trilha.
- **Demonstração (F14-69):** a marca `Tenant.Demonstracao` nasce aqui (D1 a1), só com a recusa das quatro transições, porque o F14-69 é deste PR. Quem cria e marca a empresa é o F14s. Os testes gravam a marca direto no banco, como os PRs anteriores fizeram com as situações. O detalhe da área não expõe a marca: fica para o F14m/F14s decidir se a tela precisa.
- **`DocumentoTeste.Liberar(agora)`** (pendência do F14g): `LiberadoEm = agora` e `LiberacaoUsadaEm = null`. Com liberação disponível, `TransicaoInvalidaException`. A liberação usada só volta a valer por uma liberação nova da Nebrys (decisão de 18/11 fala do expurgo, que continua sem devolvê-la).

## Escopo marcado e guarda de só leitura (ADR 0015, item 2; pendência do F14b)

- `AcorduDbContext.PermitirMudancaDeSituacao()` (público, porque a Infrastructure não expõe internos aos testes de integração) marca o contexto. Com a marca, a guarda deixa passar **só** o `Tenant` em `Modified` cujas propriedades alteradas estão todas em `Situacao`, `FimDoTeste`, `SomenteLeituraDesde`, `CanceladaEm` e `GravavelDesde`. Outro campo do `Tenant`, ou qualquer dado da empresa na mesma gravação, é recusado como sempre (testes `GuardaSomenteLeituraTests`).
- Quem marca é só `Persistence.Nebrys.SituacaoDasEmpresas`, num **escopo próprio** (`IServiceScopeFactory`) com `ContextoTenant.DefinirEquipe(empresa)`, como o `ExecutorPorTenant`. O escopo da requisição da operação continua sem tenant (ADR 0014, item 3). A guarda de isolamento do `Tenant` (contexto = a própria empresa) vale sem exceção.

## Ações (`ISituacaoDasEmpresas`, `AcoesEmpresasNebrys`)

- A Application (`AcoesEmpresasNebrys`) lê o relógio, monta a transição e o que vai para a trilha (`antes` e `depois` com `situacao`, `fimDoTeste`, `somenteLeituraDesde`, `canceladaEm`); a Infrastructure carrega a empresa no escopo marcado, confere a versão, lê o fuso do calendário, aplica, grava a linha da trilha e salva num `SaveChanges` só (mesma transação).
- **`If-Match` antes da regra (F14-107):** a versão lida é comparada com o `xmin` carregado antes da transição. Assim a divergência dá `409` mesmo quando a ação também não valeria no estado novo (ex.: cancelar com a versão de antes de outro cancelamento), como a spec pede ("409; nada muda e a área recarrega"). A gravação concorrente entre a leitura e o `UPDATE` cai no token de concorrência do próprio `UPDATE` (o `xmin` é `IsRowVersion`). Sem `If-Match`, sem conferência (convenções §5, D5). `If-Match` que não é número: `400` (o `Versoes.IfMatch` de sempre).
- Resposta: `200` com o detalhe (`IConsultasNebrys.ObterEmpresaAsync`, lido depois do commit, com a versão nova), como as ações de status das outras áreas.
- Empresa inexistente ou expurgada: `404 nao-encontrado` (F14-106). Uma corrida com o expurgo (apagada entre a leitura e o `UPDATE`) sai como `409`: a área recarrega e recebe `404`.
- Prorrogação: corpo `{ "ate": "AAAA-MM-DD" }` (`DateOnly?`); sem o dia, `400` em `errors.ate`.

## Liberação (`LiberacaoDocumentos`, `IDocumentosTeste`)

- `POST /nebrys/documentos/liberacao` com `{ "documento" }`, resposta `204`. Normaliza como o cadastro (`Documento.Normalizar`); vazio ou inválido → `400` em `errors.documento`.
- `IDocumentosTeste` é implementada pelo `RegistroDocumentos` (`Persistence.Documentos`), que calcula o HMAC e devolve o registro rastreado; o tipo `DocumentoTeste` continua só nos namespaces liberados pelo `OperacaoTests` (`Persistence.Documentos` e `Application.Nebrys`).
- **Premissas:** documento que nunca teve teste → `422 transicao-invalida` sem criar registro (não há o que liberar, o cadastro já passa; criar linha guardaria algo de um documento sem teste). Liberação ainda não usada → `422` (não duplica nem renova a data).
- Trilha: `nebrys.documento.liberacao`, alvo `documento` sem id, `dados` só com `liberadoEm` e `liberacaoUsadaEm`, antes e depois: nem o documento nem o HMAC.

## Trilha e operador (D14, ADR 0014 item 6; sugestão do F14k)

- `IOperadorAtual` (Application.Nebrys), implementado na Api por `OperadorDaRequisicao` e preenchido pelo `AreaNebrys.ConferirOperadorAsync` depois de conferir a conta no banco. Fora de uma rota da área, ler o operador lança.
- `ITrilhaDaOperacao` (`Persistence.Auditoria.TrilhaDaOperacao`): linha sem tenant, com o operador e o IP, na unidade de trabalho da requisição (liberação). As ações de situação gravam a linha no escopo próprio delas, pelo mesmo construtor (`TrilhaDaOperacao.Registro`), com o operador e o IP da requisição.
- Ações novas em `AcoesAuditoria` e no contrato §11a e §17: `nebrys.empresa.ativacao`, `.prorrogacao`, `.cancelamento`, `.reativacao`, `nebrys.documento.liberacao`. A conferência de todas as ações da área é do F14p (F14-70).

## Documentos

Contrato §11a e §17; segurança §5.5 e §8 (e o item da liberação em §4); modelo de domínio §4.11.3 e `DocumentoTeste`; read side §8a (uma nota: as ações respondem com a consulta do detalhe); convenções (área da Nebrys e a exceção de fuso do `Tenant.Prorrogar`). ERS sem mudança (RF-NEB-01 e RF-EMP-01 já descrevem as ações).
