# Notas do desenvolvedor (F14s, etapa 4)

Premissas técnicas do F14s (F14-87 a F14-90, F14-110; RF-NEB-02; D1 a1, D13, D14; ADR 0014 itens 5 a 7). Nenhuma muda cenário nem decisão do design.

## Ambiente

- Sessão sem SDK .NET: nada de C# foi compilado nem rodado aqui. O C# foi escrito e relido para compilar (assinaturas conferidas no código); Domain, Application, Infrastructure, Api e todos os testes .NET só rodam na CI.
- **Sem migration:** a marca `tenants.demonstracao` veio no F14l (`DemonstracaoDaEmpresa`), e nada mais muda no modelo.
- **OpenAPI à mão**, no formato do gerado (mesma indentação, esquemas em ordem alfabética, caminhos na ordem do registro): `nebrys.json` (cinco rotas da demonstração e `DemonstracaoNebrysDto`, `IndicacaoDemonstracaoRequest`, `PessoaIndicadaNebrysDto`) e `interno.json` (`TenantAtivo.demonstracao`). Tipos do SPA regerados com `pnpm api:tipos`. Se o `ContratoOpenApiTests` acusar diferença, regenerar com `ACORDU_ATUALIZAR_OPENAPI=1 dotnet test --filter ContratoOpenApiTests && cd apps/web && pnpm api:tipos` (ou `.github/openapi-request`).
- **Sem pedido ao bot do manual:** a área da Nebrys fica fora do manual, e a faixa da demonstração só aparece na empresa de demonstração (o bot cria uma empresa comum). Nenhuma captura muda. Sem nota de Novidades pelo mesmo motivo: só a Nebrys e quem ela indicar veem a demonstração.

## Domínio

- `Tenant.CriarDemonstracao(id, nome, agora, criadoPor)`: ATIVA, marca de demonstração, sem fim de teste nem documento; o criador é a pessoa fictícia que é Dona no arquivo (a Marina). **Premissa:** `EnvioLiberadoEm` = criação. Sem isso, as telas da equipe desabilitam convites e links com "confirme o e-mail de quem criou a empresa", o que não faz sentido numa conta fictícia; com isso, o botão chama a API e recebe o `422 demonstracao-sem-envio` com a explicação. O bloqueio olha a marca **antes** da liberação, então nada sai.
- `Tenant.RestaurarDemonstracao(nome)`: volta nome, portal, endereço de resposta, logotipo, SLA sugerido e fechamento automático ao estado da criação; só vale na demonstração. Situação, criador e data de criação ficam.
- `Vencer` já recusava empresa fora de TESTE; a demonstração é ATIVA. Os passos do diário (`vencimento-testes`, `avisos-situacao`, `expurgo-cadastros`, `expurgo-empresas`) passaram a excluir a marca também, explicitamente, para "nunca vence nem é expurgada" não depender só de ela ser ATIVA (o design do F14i deixava a escolha para cá).

## Envio (F14-89)

- `EnvioATerceiros.GarantirLiberadoAsync(comEmail, ...)` (sobrecarga; a sem `comEmail` vale `true`): na demonstração, com e-mail → `DemonstracaoSemEnvioException` (`422 demonstracao-sem-envio`, tipo novo no contrato §13 e no `ManipuladorExcecoes`); sem e-mail, passa. Convite da equipe e do portal sempre mandam e-mail; o link de acesso manda quando `enviarEmail` não é `false`.
- `PodeAvisarAsync` falso na demonstração, para qualquer destinatário (inclusive o criador fictício e as pessoas indicadas), como no só leitura: os avisos do sino continuam (não são e-mail), os e-mails são descartados e registrados como bloqueados.
- Continuam saindo os e-mails da própria conta de quem foi indicado (confirmação, redefinição de senha): são da pessoa, não da empresa. As contas fictícias não têm senha e o e-mail é `.invalid`: um pedido de redefinição para elas não entrega nada.

## Área (contrato §17)

- **Uma demonstração só.** `GET /nebrys/demonstracao` responde `200` também sem ela (`empresaId` nulo): na área o `404` de uma leitura é a barreira, e o SPA encerra a sessão com ele. `POST` cria (`201`); já existe → `422 transicao-invalida`. A criação é serializável: duas ao mesmo tempo não deixam duas (a segunda falha no commit, um `500` raro; risco aceito, é uma ação de uma pessoa só).
- **Onde fica o código:** o caso de uso `DemonstracaoNebrys` (Application.Nebrys) não conhece tipo de negócio (teste `NebrysTests`); a porta `IEmpresaDeDemonstracao` é implementada em `Infrastructure.Persistence.Demonstracao.EmpresaDeDemonstracao`, fora de `Persistence.Nebrys` (que o teste de arquitetura proíbe de tocar tipo de negócio). Cada ação que grava abre um escopo próprio com o tenant da demonstração fixado (ADR 0014, item 5), grava pelo `SaveChanges` (sem a unidade de trabalho: nada de outbox nem de sinais do tempo real) e põe a linha da trilha sem tenant, com o operador, na mesma transação, como o `SituacaoDasEmpresas`.
- **Dados:** `SemeaduraDaDemonstracao` (Application.Exemplos) monta tudo do `demonstracao.json`: papéis e calendário padrão, a equipe (papel e capacidade do arquivo), as pessoas indicadas como Dono, categorias, o que é ligado a cliente (`CargaDosDadosFicticios.MontarDemonstracaoAsync`, com as categorias das solicitações e sem marcar exemplo), recorrências (estimativa de 1h, visíveis no portal, como no bot), o cronômetro rodando e o contato da padaria com acesso ao portal (vínculo de cliente e a conta no contato, como o aceite do convite faria, sem convite). Eventos descartados em tudo.
- **Contas fictícias:** as pessoas e o contato do portal, com os e-mails `.invalid` do arquivo, criadas **sem senha** pelo `UserManager` (ninguém entra com elas) e reaproveitadas na restauração. **Premissa de segurança:** uma conta com senha num desses e-mails (alguém se cadastrou com ele) nunca é reaproveitada: a criação recusa com `422`, senão essa pessoa entraria como da equipe.
- **Pessoa indicada** = vínculo de equipe na demonstração cuja conta não é uma das fictícias (comparação pelo e-mail normalizado do Identity). Não guardei a lista à parte: o vínculo é a indicação, e restaurar lê os vínculos antes de apagar. Consequência aceita: quem foi indicado e se removeu da equipe da demonstração deixa de ser indicado.
- **Indicar (F14-90, F14-110):** só conta que já existe **e tem senha**. E-mail sem conta, ou de conta fictícia → `400` em `errors.email` com a explicação (erro de campo, como o cadastro, e não `422`: é o valor do campo que não serve); nada é criado e nenhum e-mail sai. Já na demonstração → `422 transicao-invalida`. A pessoa entra como Dono (todas as permissões ali) e vê a demonstração na troca de empresa; não recebe convite.
- **Tirar a indicação** (`DELETE …/indicacoes/{usuarioId}`, `204`): não está nos cenários; entrou porque indicar sem poder desfazer deixaria um Dono a mais para sempre. Apaga o vínculo e revoga as sessões dela na demonstração, como a remoção da equipe. Conta fictícia → `404`.
- **Restaurar (F14-88):** `ApagadorDeEmpresa.ApagarParaRestaurarAsync` apaga, no escopo da demonstração, toda entidade com `TenantId` dela (dados, papéis, vínculos, calendário, convites, notificações, trilha dela, contador do protocolo, outbox), **menos os refresh tokens**: quem estava na demonstração continua com a sessão (o papel é relido na renovação, pelo vínculo novo). A linha do `Tenant`, as contas e as sugestões ficam. Semeia de novo na mesma transação; anexos e logotipo saem do storage depois do commit, em melhor esforço. Só o tenant da demonstração é tocado (teste funcional e de integração).
- **Trilha (D14):** `nebrys.demonstracao.criacao` (`antes` nulo; `depois` com nome, situação e clientes), `.restauracao` (nome e clientes antes e depois), `.indicacao` e `.retirada` (com o `email` e o papel antes e depois). Alvo `empresa` com o id da demonstração. `TrilhaNebrysTests` ganhou os quatro casos.
- O detalhe da empresa na área (`EmpresaNebrysDto`) **não** ganhou a marca: o teste de aceite do F14-62 fixa os campos. A demonstração aparece na lista como ATIVA, e a tela da demonstração tem o link para o detalhe.

## Sessão e front

- `/auth/eu` traz `tenant.demonstracao` (design §1.2); a casca mostra a faixa da demonstração no lugar da do teste ("os dados são fictícios e nada sai daqui por e-mail…").
- Tela `/nebrys/demonstracao` (`src/nebrys/Demonstracao.tsx`, link "Demonstração" na casca): criar; o nome, a data e o link para o detalhe; restaurar com confirmação; a lista de quem foi indicado com "Tirar" (confirmação); o formulário de indicar por e-mail (`400` no campo, `422` no alerta, `404` relê a tela e avisa que a demonstração não existe mais, sem encerrar a sessão). A leitura com `404` encerra a sessão, como as listas da área.

## Testes de outros PRs que acompanham

- `AreaNebrysTests.DocumentoNebrys_SoAsRotasDaArea` e `RotasDaArea` (rotas novas, todas com o 404 da barreira); `TrilhaNebrysTests` (`Acoes` e `RotaDaAcao`, preparo e conferência das quatro ações). O banco dos funcionais é um só para a coleção: `Apoio/Demonstracoes` apaga a demonstração que houver (pelo `ApagadorDeEmpresa`, com as contas fictícias) antes de criar, e `DemonstracaoTests` começa e termina sem nenhuma.

## Devolução, rodada 1

- Ressalvas do revisor: as quatro escolhas fora do design no `docs/estado.md` ("Para o Daniel confirmar"); teste do front renomeado para `Indicar_ContaQueExiste_IndicaEMostraNaLista` (nenhum e-mail sai) e a spec do F14-90 atualizada; `cancelada_em` com tipo explícito (`(DateTimeOffset?)null`) no `ExecuteSqlAsync`; `Interacao_ExternaNaDemonstracao_NaoAvisaOContato` e `AlertasSla_NaDemonstracao_…` ganharam o controle numa `Equipe` comum (a mesma interação externa manda `aviso-mensagem` ao contato; o mesmo tick manda `aviso-sla-violado` ao Dono), para a ausência provar que vem da demonstração.

## Devolução, rodada 2 (CI vermelha)

- `DemonstracaoTests.Criar_ContasFicticias_SemSenhaNinguemEntra` falhou em `helena.prado@advocacia.exemplo.invalid`. **Causa: defeito do teste, não de produção.** O teste percorria `EmailsFicticios()`, que junta os e-mails da equipe e de **todos** os contatos dos clientes (certo para "nenhum e-mail sai"), e esperava uma conta para cada um. Pelo design e pela criação (`EmpresaDeDemonstracao.ContasFicticiasAsync`), só ganham conta as pessoas da equipe e o contato com acesso ao portal (o da padaria, Sérgio, que passou); Helena é contato da advocacia, sem portal, e não tem conta, como deve. Não era isolamento: a coleção `ColecaoApi` não roda em paralelo e o teste começa sem demonstração; a busca usa `UserManager.FindByEmailAsync`, que normaliza o e-mail.
- Correção: o teste confere conta sem senha só em `ContasFicticias()` (equipe e contato do portal, tirado de `Dados.Portal`) e passou a conferir que os outros contatos **não** ganham conta. Nada mudou na produção.
