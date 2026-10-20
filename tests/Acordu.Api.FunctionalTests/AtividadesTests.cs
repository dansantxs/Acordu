using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §11f (RF-COL-05): feed de atividade alimentado pelos eventos, filtros e títulos atuais.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class AtividadesTests(ApiFixture api)
{
    private static string[] Tipos(JsonElement pagina) =>
        [.. pagina.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("tipo").GetString()!)];

    [Fact]
    public async Task Feed_DoMaisRecente_ComDetalheEFiltroPorCliente()
    {
        var equipe = await Equipe.CriarAsync(api);
        var eu = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("usuarioId").GetGuid();
        var cliente = await ClienteAsync(equipe);
        var solicitacao = await SolicitacaoAsync(equipe, cliente);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/interacoes", new
        {
            texto = "Ligar amanhã.", visibilidade = "INTERNA",
        });
        var tarefa = await TarefaAsync(equipe, solicitacao, "Arte do banner");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/tarefas/{tarefa}/inicio");
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/tarefas/{tarefa}/comentarios", new { texto = "Versão 1." });
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new { titulo = "Interna, sem cliente" });

        var feed = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/atividades");
        var doCliente = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/atividades?clienteId={cliente}");
        var minhas = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/atividades?pessoaId={eu}");
        var daSolicitacao = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/atividades?solicitacaoId={solicitacao}");

        Tipos(feed).Should().Equal(
            "TAREFA_CRIADA", "COMENTARIO_TAREFA", "TAREFA_STATUS", "TAREFA_CRIADA", "NOTA_INTERNA", "SOLICITACAO_ABERTA");
        Tipos(doCliente).Should().Equal("COMENTARIO_TAREFA", "TAREFA_STATUS", "TAREFA_CRIADA", "NOTA_INTERNA", "SOLICITACAO_ABERTA");
        Tipos(minhas).Should().HaveCount(6);
        Tipos(daSolicitacao).Should().Equal("NOTA_INTERNA", "SOLICITACAO_ABERTA");
        feed.GetProperty("proximo").ValueKind.Should().Be(JsonValueKind.Null);
        var status = feed.GetProperty("itens")[2];
        status.GetProperty("autor").GetProperty("id").GetGuid().Should().Be(eu);
        status.GetProperty("alvo").GetProperty("titulo").GetString().Should().Be("Arte do banner");
        status.GetProperty("cliente").GetProperty("nome").GetString().Should().Be("Padaria Bom Grão");
        status.GetProperty("detalhe").GetProperty("statusDe").GetString().Should().Be("A_FAZER");
        status.GetProperty("detalhe").GetProperty("statusPara").GetString().Should().Be("FAZENDO");
        var aberta = feed.GetProperty("itens")[5];
        aberta.GetProperty("alvo").GetProperty("tipo").GetString().Should().Be("SOLICITACAO");
        aberta.GetProperty("alvo").GetProperty("numero").GetInt32().Should().BePositive();
    }

    [Fact]
    public async Task OutroTenantNaoVe_EContato403()
    {
        var equipe = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        await SolicitacaoAsync(equipe, cliente);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);

        var deOutra = await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/atividades");
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, "/api/v1/atividades", token: contato.Token);

        deOutra.GetProperty("itens").GetArrayLength().Should().Be(0);
    }
}
