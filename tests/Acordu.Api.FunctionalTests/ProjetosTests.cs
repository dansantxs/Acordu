using System.Net;
using Acordu.Api.FunctionalTests.Apoio;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §8.2: projetos, contrato do mesmo cliente, progresso e transições.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class ProjetosTests(ApiFixture api)
{
    [Fact]
    public async Task Criar_ComProgresso_EConcluidoNaoEdita()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var contrato = await ContratoAsync(equipe, cliente);

        var projeto = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/projetos", new
        {
            clienteId = cliente, nome = "Site novo", contratoId = contrato, estimativaMinutos = 2400,
        });
        var id = Id(projeto);
        var tarefa = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new { titulo = "Layout", projetoId = id }));
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/tarefas/{tarefa}/inicio");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/tarefas/{tarefa}/conclusao");
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new { titulo = "Conteúdo", projetoId = id });

        var detalhe = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/projetos/{id}");
        var concluido = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/projetos/{id}/conclusao");
        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Patch, $"/api/v1/projetos/{id}", new { nome = "Outro" });
        var novaTarefa = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/tarefas", new { titulo = "Atrasada", projetoId = id });

        projeto.GetProperty("cliente").GetProperty("nome").GetString().Should().Be("Padaria Bom Grão");
        var progresso = detalhe.GetProperty("progresso");
        progresso.GetProperty("tarefasConcluidas").GetInt32().Should().Be(1);
        progresso.GetProperty("tarefasTotal").GetInt32().Should().Be(2);
        progresso.GetProperty("estimativaMinutos").GetInt32().Should().Be(2400);
        concluido.GetProperty("status").GetString().Should().Be("CONCLUIDO");
        novaTarefa.GetProperty("errors").TryGetProperty("projetoId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Criar_ContratoDeOutroCliente422_Listar()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var outro = await ClienteAsync(equipe, "Oficina");
        var contratoDoOutro = await ContratoAsync(equipe, outro);

        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, "/api/v1/projetos", new
        {
            clienteId = cliente, nome = "Site", contratoId = contratoDoOutro,
        });
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/projetos", new { clienteId = cliente, nome = "Site" });
        var lista = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/projetos?clienteId={cliente}&status=EM_ANDAMENTO");

        lista.GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Inicio_CriarEditarEDepoisDoPrazo400()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);

        var projeto = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/projetos", new
        {
            clienteId = cliente, nome = "Campanha", inicio = "2026-11-02", prazo = "2026-11-20",
        });
        var id = Id(projeto);
        var invalido = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Patch, $"/api/v1/projetos/{id}", new
        {
            nome = "Campanha", inicio = "2026-11-21", prazo = "2026-11-20",
        });
        var editado = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/projetos/{id}", new
        {
            nome = "Campanha", inicio = "2026-11-05", prazo = "2026-11-20",
        });
        var lista = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/projetos?clienteId={cliente}");

        projeto.GetProperty("inicio").GetString().Should().Be("2026-11-02");
        invalido.GetProperty("errors").TryGetProperty("inicio", out _).Should().BeTrue();
        editado.GetProperty("inicio").GetString().Should().Be("2026-11-05");
        lista.GetProperty("itens")[0].GetProperty("inicio").GetString().Should().Be("2026-11-05");
    }
}
