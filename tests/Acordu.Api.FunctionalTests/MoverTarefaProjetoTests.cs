using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F11, F11d: mover a tarefa entre projetos do mesmo cliente (F11-23 a F11-26).</summary>
[Collection(ColecaoApi.Nome)]
public sealed class MoverTarefaProjetoTests(ApiFixture api)
{
    private static async Task<Guid> ProjetoAsync(Equipe equipe, Guid cliente, string nome, Guid? contrato = null) =>
        Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/projetos", new
        {
            clienteId = cliente, nome, contratoId = contrato,
        }));

    private static Task<JsonElement> MoverAsync(
        Equipe equipe, HttpStatusCode esperado, Guid tarefa, Guid? projetoId, string? ifMatch = null) =>
        equipe.EsperarAsync(esperado, HttpMethod.Put, $"/api/v1/tarefas/{tarefa}/projeto", new { projetoId }, ifMatch: ifMatch);

    private static async Task<int> ApontadoAsync(Equipe equipe, Guid projeto) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/projetos/{projeto}"))
            .GetProperty("progresso").GetProperty("apontadoMinutos").GetInt32();

    [Fact]
    public async Task Mover_DeSolicitacaoParaProjetoTrocarETirar() // F11-23
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var solicitacao = await SolicitacaoAsync(equipe, cliente);
        var tarefa = await TarefaAsync(equipe, solicitacao);
        var site = await ProjetoAsync(equipe, cliente, "Site novo");
        var campanha = await ProjetoAsync(equipe, cliente, "Campanha");

        var noSite = await MoverAsync(equipe, HttpStatusCode.OK, tarefa, site);
        var naCampanha = await MoverAsync(equipe, HttpStatusCode.OK, tarefa, campanha, ifMatch: $"\"{noSite.GetProperty("versao").GetString()}\"");
        var semProjeto = await MoverAsync(equipe, HttpStatusCode.OK, tarefa, null);

        noSite.GetProperty("projetoId").GetGuid().Should().Be(site);
        noSite.GetProperty("solicitacaoId").GetGuid().Should().Be(solicitacao, "a tarefa continua ligada à solicitação");
        naCampanha.GetProperty("projetoNome").GetString().Should().Be("Campanha");
        semProjeto.GetProperty("projetoId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Mover_VersaoVelha_409()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var tarefa = await TarefaAsync(equipe, await SolicitacaoAsync(equipe, cliente));
        var site = await ProjetoAsync(equipe, cliente, "Site novo");
        var antes = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/tarefas/{tarefa}")).GetProperty("versao").GetString();
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/tarefas/{tarefa}/inicio");

        await MoverAsync(equipe, HttpStatusCode.Conflict, tarefa, site, ifMatch: $"\"{antes}\"");
    }

    [Fact]
    public async Task Mover_ProjetoDeOutroCliente_422ENadaMuda() // F11-24
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var oficina = await ClienteAsync(equipe, "Oficina");
        var tarefa = await TarefaAsync(equipe, await SolicitacaoAsync(equipe, cliente));
        var daOficina = await ProjetoAsync(equipe, oficina, "Vitrine");

        var problema = await MoverAsync(equipe, HttpStatusCode.UnprocessableEntity, tarefa, daOficina);

        problema.GetProperty("type").GetString().Should().EndWith("/projeto-de-outro-cliente");
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/tarefas/{tarefa}"))
            .GetProperty("projetoId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Mover_ProjetoEncerrado_422() // F11-25
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var tarefa = await TarefaAsync(equipe, await SolicitacaoAsync(equipe, cliente));
        var concluido = await ProjetoAsync(equipe, cliente, "Antigo");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/projetos/{concluido}/conclusao");

        var problema = await MoverAsync(equipe, HttpStatusCode.UnprocessableEntity, tarefa, concluido);

        problema.GetProperty("type").GetString().Should().EndWith("/projeto-encerrado");
    }

    [Fact]
    public async Task Mover_ProjetoDeOutroTenant_404() // F11-29
    {
        var equipe = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var tarefa = await TarefaAsync(equipe, await SolicitacaoAsync(equipe, cliente));
        var deFora = await ProjetoAsync(outra, await ClienteAsync(outra), "Alheio");

        await MoverAsync(equipe, HttpStatusCode.NotFound, tarefa, deFora);
        await MoverAsync(outra, HttpStatusCode.NotFound, tarefa, deFora);
    }

    [Fact]
    public async Task Mover_SemTarefaEditar_403()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var tarefa = await TarefaAsync(equipe, await SolicitacaoAsync(equipe, cliente));
        var site = await ProjetoAsync(equipe, cliente, "Site novo");
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/papeis", new
        {
            nome = "Só vê", permissoes = new[] { "tarefa.ver", "projeto.ver" },
        });
        var token = await equipe.ComPapelAsync("Só vê");

        await equipe.EsperarAsync(
            HttpStatusCode.Forbidden, HttpMethod.Put, $"/api/v1/tarefas/{tarefa}/projeto", new { projetoId = site }, token: token);
    }

    [Fact]
    public async Task Mover_ApontamentosMantemOContrato_ProgressoSegueATarefa_EVaiParaAAtividade() // F11-26
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var contrato = await ContratoAsync(equipe, cliente);
        var site = await ProjetoAsync(equipe, cliente, "Site novo", contrato);
        var campanha = await ProjetoAsync(equipe, cliente, "Campanha");
        var tarefa = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new
        {
            titulo = "Layout", projetoId = site,
        }));
        var apontamento = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = tarefa, data = "2026-10-05", duracaoMinutos = 90, descricao = "Layout da home", descricaoExterna = "Layout",
            faturavel = true, visivelCliente = true,
        }));

        await MoverAsync(equipe, HttpStatusCode.OK, tarefa, campanha);

        var depois = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/apontamentos/{apontamento}");
        depois.GetProperty("contratoId").GetGuid().Should().Be(contrato, "o apontamento congela o contrato (ADR 0004)");
        (await ApontadoAsync(equipe, site)).Should().Be(0);
        (await ApontadoAsync(equipe, campanha)).Should().Be(90);

        var feed = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/atividades");
        var linha = feed.GetProperty("itens").EnumerateArray().First(i => i.GetProperty("tipo").GetString() == "TAREFA_PROJETO");
        var detalhe = linha.GetProperty("detalhe");
        detalhe.GetProperty("projetoDe").GetProperty("nome").GetString().Should().Be("Site novo");
        detalhe.GetProperty("projetoPara").GetProperty("nome").GetString().Should().Be("Campanha");
        detalhe.GetProperty("statusPara").ValueKind.Should().Be(JsonValueKind.Null);
        linha.GetProperty("alvo").GetProperty("id").GetGuid().Should().Be(tarefa);
    }
}
