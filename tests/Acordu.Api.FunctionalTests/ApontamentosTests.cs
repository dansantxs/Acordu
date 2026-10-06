using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §9: apontamentos (contrato congelado, período fechado, autoria) e cronômetro.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class ApontamentosTests(ApiFixture api)
{
    private const string Base = "https://acordu.app.br/erros/";

    private static object Apontamento(Guid tarefa, string data = "2026-10-05", int minutos = 90) => new
    {
        tarefaId = tarefa, data, duracaoMinutos = minutos, descricao = "Ajuste de layout e exportação",
        descricaoExterna = "Arte refeita", faturavel = true, visivelCliente = true,
    };

    [Fact]
    public async Task CaminhoCompleto_SolicitacaoTarefaApontamentoConsumo()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var contrato = await ContratoAsync(equipe, cliente);
        var solicitacao = await SolicitacaoAsync(equipe, cliente);
        var tarefa = await TarefaAsync(equipe, solicitacao);

        var apontamento = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", Apontamento(tarefa));
        var naoFaturavel = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = tarefa, data = "2026-10-05", duracaoMinutos = 30, descricao = "Reunião interna", faturavel = false,
        });
        var consumo = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/contratos/{contrato}/consumo?periodo=2026-10");
        var detalheSolicitacao = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}");
        var detalheTarefa = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/tarefas/{tarefa}");

        apontamento.GetProperty("contratoId").GetGuid().Should().Be(contrato, "o contrato vem da solicitação e fica congelado");
        apontamento.GetProperty("clienteId").GetGuid().Should().Be(cliente);
        apontamento.GetProperty("usuario").GetProperty("nome").GetString().Should().Be("Pessoa da equipe");
        apontamento.GetProperty("tarefa").GetProperty("nome").GetString().Should().Be("Refazer arte");
        naoFaturavel.GetProperty("visivelCliente").GetBoolean().Should().BeFalse("o padrão segue o faturável");
        consumo.GetProperty("consumidoMinutos").GetInt32().Should().Be(90);
        consumo.GetProperty("naoFaturavelMinutos").GetInt32().Should().Be(30);
        consumo.GetProperty("saldoMinutos").GetInt32().Should().Be(1200 - 90);
        detalheSolicitacao.GetProperty("tempoApontadoMinutos").GetInt32().Should().Be(120);
        detalheTarefa.GetProperty("apontadoMinutos").GetInt32().Should().Be(120);
    }

    [Fact]
    public async Task PeriodoFechado_RecusaRegistrarEditarEExcluir()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var contrato = await ContratoAsync(equipe, cliente);
        var tarefa = await TarefaAsync(equipe, await SolicitacaoAsync(equipe, cliente));
        var setembro = Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", Apontamento(tarefa, "2026-09-15")));
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Post, $"/api/v1/contratos/{contrato}/periodos/2026-09/fechamento");

        var registrar = await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, "/api/v1/apontamentos", Apontamento(tarefa, "2026-09-20"));
        var editar = await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Patch, $"/api/v1/apontamentos/{setembro}", new
            {
                data = "2026-10-01", duracaoMinutos = 60, descricao = "Movendo para outubro", faturavel = true, visivelCliente = false,
            });
        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Delete, $"/api/v1/apontamentos/{setembro}");

        registrar.GetProperty("type").GetString().Should().Be(Base + "periodo-fechado");
        editar.GetProperty("type").GetString().Should().Be(Base + "periodo-fechado");
    }

    [Fact]
    public async Task Autoria_OutroMembro403_DonoEditaTodos()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var outroMembro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var tarefa = await TarefaAsync(equipe);
        var doMembro = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = tarefa, data = "2026-10-05", duracaoMinutos = 45, descricao = "Backup", faturavel = false,
        }, token: membro);
        var edicao = new { data = "2026-10-05", duracaoMinutos = 50, descricao = "Backup e teste", faturavel = false, visivelCliente = false };

        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Patch, $"/api/v1/apontamentos/{Id(doMembro)}", edicao, token: outroMembro);
        var peloDono = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/apontamentos/{Id(doMembro)}", edicao);
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/apontamentos/{Id(doMembro)}", token: membro);
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/apontamentos/{Id(doMembro)}");

        doMembro.GetProperty("contratoId").ValueKind.Should().Be(JsonValueKind.Null, "tarefa avulsa não tem contrato");
        peloDono.GetProperty("duracaoMinutos").GetInt32().Should().Be(50);
    }

    [Fact]
    public async Task Validacao_DuracaoEVisibilidadeSemCliente400()
    {
        var equipe = await Equipe.CriarAsync(api);
        var tarefa = await TarefaAsync(equipe);

        var longa = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = tarefa, data = "2026-10-05", duracaoMinutos = 1441, descricao = "Demais", faturavel = false,
        });
        var visivel = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = tarefa, data = "2026-10-05", duracaoMinutos = 30, descricao = "X", faturavel = true, visivelCliente = true,
        });

        longa.GetProperty("errors").TryGetProperty("duracaoMinutos", out _).Should().BeTrue();
        visivel.GetProperty("errors").TryGetProperty("visivelCliente", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Listar_PorTarefaEFaturavel()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        await ContratoAsync(equipe, cliente);
        var tarefa = await TarefaAsync(equipe, await SolicitacaoAsync(equipe, cliente));
        var outra = await TarefaAsync(equipe, titulo: "Outra");
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", Apontamento(tarefa));
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = outra, data = "2026-10-05", duracaoMinutos = 15, descricao = "X", faturavel = false,
        });

        var daTarefa = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/apontamentos?tarefaId={tarefa}");
        var naoFaturaveis = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/apontamentos?faturavel=false&de=2026-10-01&ate=2026-10-31");

        daTarefa.GetProperty("total").GetInt32().Should().Be(1);
        naoFaturaveis.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("duracaoMinutos").GetInt32()).Should().Equal(15);
    }

    [Fact]
    public async Task Cronometro_IniciarSegundo409_PararCriaApontamento_Descartar()
    {
        var equipe = await Equipe.CriarAsync(api);
        var tarefa = await TarefaAsync(equipe, titulo: "Manutenção do servidor");

        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Get, "/api/v1/cronometro");
        var iniciado = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/cronometro/inicio", new { tarefaId = tarefa });
        var segundo = await equipe.EsperarAsync(HttpStatusCode.Conflict, HttpMethod.Post, "/api/v1/cronometro/inicio", new { tarefaId = tarefa });
        api.Relogio.Agora = api.Relogio.Agora.AddMinutes(30);
        var ativo = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/cronometro");
        var apontamento = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/cronometro/parada");
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Get, "/api/v1/cronometro");
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/cronometro/inicio", new { tarefaId = tarefa });
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, "/api/v1/cronometro");
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Post, "/api/v1/cronometro/parada");

        iniciado.GetProperty("tarefa").GetProperty("nome").GetString().Should().Be("Manutenção do servidor");
        segundo.GetProperty("type").GetString().Should().Be(Base + "cronometro-ativo");
        ativo.GetProperty("decorridoMinutos").GetInt32().Should().Be(30);
        apontamento.GetProperty("duracaoMinutos").GetInt32().Should().Be(30);
        apontamento.GetProperty("descricao").GetString().Should().Be("Manutenção do servidor");
        apontamento.GetProperty("faturavel").GetBoolean().Should().BeTrue();
    }
}
