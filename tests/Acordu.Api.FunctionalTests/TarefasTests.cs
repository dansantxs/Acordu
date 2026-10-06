using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §8.1: tarefas, herança do cliente, transições e checklist.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class TarefasTests(ApiFixture api)
{
    private const string Base = "https://acordu.app.br/erros/";

    [Fact]
    public async Task Criar_DeSolicitacao_HerdaOCliente()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var solicitacao = await SolicitacaoAsync(equipe, cliente);

        var tarefa = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new
        {
            titulo = "Refazer arte", solicitacaoId = solicitacao, prazo = "2026-10-09", estimativaMinutos = 120, visivelCliente = true,
        });
        var naSolicitacao = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}");

        tarefa.GetProperty("clienteId").GetGuid().Should().Be(cliente);
        tarefa.GetProperty("status").GetString().Should().Be("A_FAZER");
        tarefa.GetProperty("estimativaMinutos").GetInt32().Should().Be(120);
        tarefa.GetProperty("apontadoMinutos").GetInt32().Should().Be(0);
        naSolicitacao.GetProperty("tarefas")[0].GetProperty("titulo").GetString().Should().Be("Refazer arte");
    }

    [Fact]
    public async Task Criar_AvulsaVisivelSemCliente400_ClienteDivergente400()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var outro = await ClienteAsync(equipe, "Oficina");
        var solicitacao = await SolicitacaoAsync(equipe, cliente);

        var visivel = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/tarefas", new { titulo = "Interna", visivelCliente = true });
        var divergente = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/tarefas", new { titulo = "X", solicitacaoId = solicitacao, clienteId = outro });
        var avulsa = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new { titulo = "Revisar backup" });

        visivel.GetProperty("errors").TryGetProperty("visivelCliente", out _).Should().BeTrue();
        divergente.GetProperty("errors").TryGetProperty("clienteId", out _).Should().BeTrue();
        avulsa.GetProperty("clienteId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Transicoes_EConcluirSemIniciar422()
    {
        var equipe = await Equipe.CriarAsync(api);
        var id = await TarefaAsync(equipe);
        var rota = $"/api/v1/tarefas/{id}";

        var semIniciar = await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, $"{rota}/conclusao");
        var fazendo = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{rota}/inicio");
        var concluida = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{rota}/conclusao");
        var reaberta = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{rota}/reabertura");
        var cancelada = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{rota}/cancelamento");
        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Patch, rota, new { titulo = "Depois de cancelada" });

        semIniciar.GetProperty("type").GetString().Should().Be(Base + "transicao-invalida");
        fazendo.GetProperty("status").GetString().Should().Be("FAZENDO");
        concluida.GetProperty("status").GetString().Should().Be("CONCLUIDA");
        reaberta.GetProperty("status").GetString().Should().Be("FAZENDO");
        cancelada.GetProperty("status").GetString().Should().Be("CANCELADA");
    }

    [Fact]
    public async Task Checklist_AdicionarMarcarRemover()
    {
        var equipe = await Equipe.CriarAsync(api);
        var id = await TarefaAsync(equipe);
        var rota = $"/api/v1/tarefas/{id}/itens";

        var primeiro = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, rota, new { texto = "Exportar PNG" });
        var segundo = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, rota, new { texto = "Publicar" });
        var marcado = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Patch, $"{rota}/{Id(primeiro)}", new { texto = "Exportar PNG e JPG", feito = true });
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"{rota}/{Id(segundo)}");
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Delete, $"{rota}/{Guid.NewGuid()}");
        var tarefa = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/tarefas/{id}");

        primeiro.GetProperty("ordem").GetInt32().Should().Be(1);
        segundo.GetProperty("ordem").GetInt32().Should().Be(2);
        marcado.GetProperty("feito").GetBoolean().Should().BeTrue();
        marcado.GetProperty("texto").GetString().Should().Be("Exportar PNG e JPG");
        tarefa.GetProperty("checklist").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Listar_MinhasStatusESolicitacao()
    {
        var equipe = await Equipe.CriarAsync(api);
        var eu = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("usuarioId").GetGuid();
        var cliente = await ClienteAsync(equipe);
        var solicitacao = await SolicitacaoAsync(equipe, cliente);
        var minha = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new
        {
            titulo = "Minha", responsavelId = eu, solicitacaoId = solicitacao,
        }));
        var outra = await TarefaAsync(equipe, titulo: "Sem responsável");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/tarefas/{outra}/inicio");

        var minhas = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/tarefas?minhas=true");
        var fazendo = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/tarefas?status=FAZENDO");
        var daSolicitacao = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/tarefas?solicitacaoId={solicitacao}");

        minhas.GetProperty("itens").EnumerateArray().Select(Id).Should().Equal(minha);
        minhas.GetProperty("itens")[0].GetProperty("responsavel").GetProperty("nome").GetString().Should().Be("Pessoa da equipe");
        fazendo.GetProperty("itens").EnumerateArray().Select(Id).Should().Equal(outra);
        daSolicitacao.GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task OutroTenant404()
    {
        var equipe = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        var id = await TarefaAsync(equipe);

        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/tarefas/{id}");
        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Post, $"/api/v1/tarefas/{id}/inicio");
    }
}
