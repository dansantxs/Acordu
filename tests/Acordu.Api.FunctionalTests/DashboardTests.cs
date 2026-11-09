using System.Globalization;
using System.Net;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Acesso;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §11 e read side §6: blocos do dashboard, período e blocos por permissão.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class DashboardTests(ApiFixture api)
{
    private DateOnly Hoje() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(api.Relogio.Agora, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);

    private static string Data(DateOnly data) => data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Blocos_FilaSlaHorasContratosETarefas()
    {
        var equipe = await Equipe.CriarAsync(api);
        var eu = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("usuarioId").GetGuid();
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contrato = await Cenarios.ContratoAsync(equipe, cliente);
        var comContrato = Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, contratoId = contrato, titulo = "Banner", prioridade = "MEDIA", responsavelId = eu,
        }));
        await Cenarios.SolicitacaoAsync(equipe, cliente);
        var tarefa = await Cenarios.TarefaAsync(equipe, comContrato);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = tarefa, data = Data(Hoje()), duracaoMinutos = 90, descricao = "Arte", faturavel = true,
        });
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = tarefa, data = Data(Hoje()), duracaoMinutos = 30, descricao = "Reunião", faturavel = false,
        });
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new
        {
            titulo = "Atrasada", prazo = Data(Hoje().AddDays(-1)),
        });
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", new
        {
            clienteId = cliente,
            modelo = new { titulo = "Fechamento mensal", visivelCliente = false },
            regra = new { periodicidade = "MENSAL", diaReferencia = 10, antecedenciaDias = 0, inicio = "2030-01-01" },
        });

        var painel = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/dashboard");

        painel.GetProperty("de").GetString().Should().Be(Data(new DateOnly(Hoje().Year, Hoje().Month, 1)));
        var fila = painel.GetProperty("fila");
        fila.GetProperty("abertas").GetInt32().Should().Be(1, "a solicitação com tarefa passou a EM_ANDAMENTO (F11-18)");
        fila.GetProperty("semResponsavel").GetInt32().Should().Be(1);
        var sla = painel.GetProperty("sla");
        sla.GetProperty("violadas").GetInt32().Should().Be(0);
        sla.GetProperty("cumprimentoPeriodo").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        var horas = painel.GetProperty("horas");
        horas.GetProperty("faturavelMinutos").GetInt32().Should().Be(90);
        horas.GetProperty("naoFaturavelMinutos").GetInt32().Should().Be(30);
        horas.GetProperty("porCliente")[0].GetProperty("nome").GetString().Should().Be("Padaria Bom Grão");
        horas.GetProperty("porCliente")[0].GetProperty("minutos").GetInt32().Should().Be(90);
        var contratos = painel.GetProperty("contratos");
        contratos.GetArrayLength().Should().Be(1);
        contratos[0].GetProperty("tipo").GetString().Should().Be("BANCO_HORAS");
        contratos[0].GetProperty("consumidoMinutos").GetInt32().Should().Be(90);
        contratos[0].GetProperty("percentual").GetInt32().Should().Be(7);
        var tarefas = painel.GetProperty("tarefas");
        tarefas.GetProperty("vencidas").GetInt32().Should().Be(1);
        tarefas.GetProperty("proximasRecorrentes")[0].GetProperty("titulo").GetString().Should().Be("Fechamento mensal");
        tarefas.GetProperty("proximasRecorrentes")[0].GetProperty("data").GetString().Should().Be("2030-01-10");
    }

    [Fact]
    public async Task Periodo_InvertidoOuLongoDemais_400()
    {
        var equipe = await Equipe.CriarAsync(api);

        var invertido = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Get, "/api/v1/dashboard?de=2026-10-10&ate=2026-10-01");
        var longo = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Get, "/api/v1/dashboard?de=2025-01-01&ate=2026-10-01");
        var ano = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/dashboard?de=2025-10-01&ate=2026-09-30");

        invertido.GetProperty("errors").TryGetProperty("ate", out _).Should().BeTrue();
        longo.GetProperty("errors").TryGetProperty("ate", out _).Should().BeTrue();
        ano.GetProperty("ate").GetString().Should().Be("2026-09-30");
    }

    [Fact]
    public async Task SemPermissaoDaArea_BlocoNulo()
    {
        var equipe = await Equipe.CriarAsync(api);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/papeis", new
        {
            nome = "Atendimento", permissoes = new[] { Permissoes.DashboardVer, Permissoes.SolicitacaoVer, Permissoes.TarefaVer },
        });
        var atendimento = await equipe.ComPapelAsync("Atendimento");

        var painel = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/dashboard", token: atendimento);

        painel.GetProperty("fila").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
        painel.GetProperty("horas").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        painel.GetProperty("contratos").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        painel.GetProperty("tarefas").GetProperty("proximasRecorrentes").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }
}
