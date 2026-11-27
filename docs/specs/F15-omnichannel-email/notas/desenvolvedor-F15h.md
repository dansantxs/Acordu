# Notas do desenvolvedor — F15h (etapa 4)

Premissas técnicas do PR da chegada à triagem (F15-20, F15-24, F15-25, F15-26, F15-70, F15-103; design D6, D10, D11 e D14; ADR 0016, item 4; ADR 0002). Sessão sem SDK .NET: o backend foi lido com cuidado, mas só compila e roda na CI. Nada do front mudou.

## Modelo

- `ItemTriagemEmail` (Domain, raiz por tenant, sem cliente), criado só por `Receber`, sem método que o altere (D10: "nunca é editado"). Campos: `RemetenteEmail` (normalizado por `Common.Email`; nulo quando o `From` não tem exatamente um endereço válido), `RemetenteNome` (o nome do `From`, numa linha, até 200), `Assunto` (numa linha, até 998, como o `AssuntoOrigem` da conversa), `Texto` (a mesma limpeza da descrição da abertura, até 10.000), `RecebidoEm`, `Motivo`, `ClientesPossiveis` (`uuid[]`), `Anexos` (o `Anexo` da interação, filhos em `anexos_triagem_email`, validados pela mesma política) e `Recusados` (`AnexosRecusados` em `jsonb`, o conversor do registro de entrada). Não registra evento: "nada é respondido ao remetente", e o aviso no sino é só o dos limites (F15n).
- `ClientesPossiveis` só com `MAIS_DE_UM_CLIENTE` e com dois ou mais, sem repetir; nos outros motivos, vazio. A invariante fica no domínio.
- `MotivoTriagem` já com os cinco valores do D10 (texto, sem migration depois); `NAO_CONTATO_DA_SOLICITACAO` só é usado no F15j. A solicitação respondida (F15-39) **não** entrou como coluna agora: é do F15j, que a preenche (como o F15d deixou os ids do registro para os PRs que os usam).
- Índice `(tenant_id, recebido_em)` já na tabela nova, para a lista do mais novo e o expurgo de 30 dias do F15k (evita uma migration só de índice).
- `EmailRecebido.ItemTriagemId`: obrigatório com `TRIAGEM` e proibido nos outros, sem FK (o item sai pelas ações e pelo expurgo; o registro fica os seus 7 dias). Os recusados do não identificado ficam no item, não no registro (o registro só guarda os da abertura, que o aviso de recebida lê).

## Motivo (Application)

- `IdentificacaoDoRemetente.Classificar(autenticidadeOk, candidatos)`, função pura, nesta ordem: sem candidato → `DESCONHECIDO` (também com a autenticidade falhando: um forjado de quem não é contato é só desconhecido, e o motivo "não confirmado" fica para quando o `From` é de um contato, que é o que o F15-26 descreve); com candidato e sem `pass` → `REMETENTE_NAO_CONFIRMADO` (antes do dois-clientes e do inativo: sem a autenticidade, quem o remetente diz ser não vale, e a equipe precisa ver isso primeiro); ativos em dois ou mais clientes → `MAIS_DE_UM_CLIENTE`; nenhum ativo de cliente ativo → `CONTATO_INATIVO`. `From` com vários endereços, inválido ou ausente → `DESCONHECIDO` sem consultar os contatos.
- Dois contatos ativos do mesmo cliente com o mesmo e-mail não existem (RF-CLI-02); se aparecerem, `DESCONHECIDO`, para nada ser escolhido sozinho.
- Os contatos passaram a ser consultados também quando a autenticidade não é `pass` (antes a consulta era pulada), para o motivo saber se o `From` é de um contato. A consulta é a mesma, sob os filtros do tenant do destino.
- "Pessoa da equipe que não é contato" (termo da spec) cai em `DESCONHECIDO`: a identificação só olha contatos.

## Chegada (`ComandosEntradaEmail`)

- Ordem: repetição → automático, laço, filtro → destino com `+token` (F15i: só o registro, desfecho `Registrada`) → identificação → abertura **ou** triagem. O desfecho novo `Triagem` vai ao log (`Entrada por e-mail: Triagem.`).
- Anexos: `AnexosDaEntrada.Separar` (a mesma da abertura), os válidos sobem por `RecebimentoAnexos.EnviarParaTriagemAsync` na chave `{tenantId}/triagem/{itemId}/{anexoId}` (o Id do item sorteado antes), antes do commit; qualquer falha da gravação (inclusive a corrida de duas entregas iguais no índice do registro, F15-103) chama o `DescartarAsync`, como na abertura. Sem nota do sistema: não há conversa; os recusados ficam no item, para a tela do F15k/F15o.
- `RecebimentoAnexos`: o envio virou um núcleo com a função da chave; `EnviarAsync` (solicitação) e `EnviarParaTriagemAsync` (triagem) só escolhem a chave. A política e a conferência são as mesmas.
- `MensagemDeEntrada.NomeDoRemetente` (o `Name` do `MailboxAddress`, decodificado pelo MimeKit, só com um endereço no `From`). Nunca vai ao log.

## Infrastructure

- `ItemTriagemEmailConfiguration` (tabela `itens_triagem_email`), DbSet `ItensTriagemEmail`, filtro de tenant e "some no contexto de Cliente" (como o `EmailRecebido`). A guarda de isolamento já recusa gravá-lo no contexto do contato (não é `RaizComCliente`), e a de só leitura o trata como da empresa.
- Classificações: `ClassificacaoSomenteLeitura.DaEmpresa`, `ClassificacaoDoExpurgo.Dados` e `DadosDeExemplo.TiposConferidos`, com a consulta própria em `SituacaoAsync` (o item vive 30 dias, mais que o registro de 7: sem ela, uma empresa só com itens da triagem voltaria a oferecer os exemplos).
- `ApagadorDeEmpresa` devolve também as chaves dos anexos da triagem (design §1.2, "Diário"): o expurgo e a restauração da demonstração tiram os arquivos do storage depois do commit, como os das interações. Os anexos saem do banco em cascata com o item (owned).
- **Migration:** `TriagemDeEmail`, pedida ao bot por `.github/migration-request`. Deve gerar as tabelas `itens_triagem_email` (`remetente_email varchar(254)`, `remetente_nome varchar(200)`, `assunto varchar(998)`, `texto text`, `recebido_em timestamptz`, `motivo varchar`, `clientes_possiveis uuid[]` não nulo, `recusados jsonb`, `xmin`, índice `(tenant_id, recebido_em)`) e `anexos_triagem_email` (FK `item_triagem_id` em cascata), e a coluna `item_triagem_id uuid` nula em `emails_recebidos`. Sem preenchimento: nada a acrescentar no `Up`.

## Testes que mudaram por causa do comportamento novo

- Testes de PRs anteriores que esperavam o não identificado como `IGNORADO`/`Registrada` (o estado provisório "até o F15h" do design §3) passaram a esperar `TRIAGEM`/`Triagem`, com o nome ajustado quando dizia "SoRegistra", "NadaSobe" ou "RegistroIgnorado": `ComandosEntradaEmailTests` (seis casos), `EntradaEmailTests` da Api (o `Entregar_RecebimentoLigado_202SemCorpoERegistroDaTriagem` e as duas linhas de log de "segue como e-mail comum") e `AberturaPorEmailTests` (duas asserções do resultado). A coluna Teste da spec acompanha os nomes novos (F15-03, F15-10, F15-14).
- A lista da triagem pela API é do F15k: os funcionais leem o item do banco, no contexto da empresa (plano-2, menor 4).

## Documentos

- Contrato §18 (triagem, motivos, chave, registro `TRIAGEM`, desfecho `Triagem`); modelo de domínio §4.15 (`ItemTriagemEmail`, `ItemTriagemId`) e o diagrama; segurança §7 (chave da triagem e os recusados no item) e §9 (triagem do não identificado e o "não confirmado"); `estado.md`; coluna Teste da spec.
- Sem mudança: ERS (RF-SOL-15 já traz a triagem), OpenAPI e tipos do front (nenhuma rota nem DTO), read side (a lista é do F15k), convenções (nenhuma regra nova: o item segue as de anexos e de log que já existem), fluxo BPMN (a tarefa da triagem entra no F15k, design §2), deploy e setup (sem variável nova; `EmailEntrada__DiasTriagem` é do F15k).

## Conferência manual (primeiro deploy do F15h)

- F15-26 com e-mail real: um forjado (o `From` de um contato, mandado de outro servidor) e um legítimo do mesmo domínio. O forjado deve ficar na triagem com `REMETENTE_NAO_CONFIRMADO`; o legítimo abre a solicitação. O Daniel liga a própria empresa pelo `PUT /configuracoes/recebimento-email` e confere o item no banco (a tela é do F15k).

## Menores do `/plano` (`plano-2.md`)

- 4: aplicado (os testes leem o item e o registro pelo banco).
- 7: o teto de volume e de bytes da triagem de remetente não identificado fica com os limites do F15n (o tamanho total do e-mail, 25 MiB, e os 5 anexos de até 20 MB já limitam cada item; o limite por empresa por dia, do F15n, pode contar o `TRIAGEM`).
- 6: os casos da triagem (corpo inválido da conversão, anexo da triagem de outro tenant ou de item já tratado) são das rotas, F15k a F15m.
- 5, 8, 9, 10 e 11: não são do F15h.
