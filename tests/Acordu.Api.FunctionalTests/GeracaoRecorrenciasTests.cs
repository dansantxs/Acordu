using System.Globalization;
using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Domain.Entities;

namespace Acordu.Api.FunctionalTests;

/// <summary>RF-REC-03..05 pelo job <c>tick</c>: geração com antecedência, idempotência, pausa, cliente inativo e responsável fora.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class GeracaoRecorrenciasTests(ApiFixture api)
{
    /// <summary>Hoje no fuso padrão do tenant (calendário semeado em America/Sao_Paulo), pelo relógio da fixture.</summary>
    private DateOnly Hoje() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(api.Relogio.Agora, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);

    /// <summary>Semanal no dia da semana de hoje, a partir de hoje: com 14 dias de antecedência, três ocorrências devidas.</summary>
    private object Semanal(Guid clienteId, Guid? responsavelId = null, int antecedencia = 14)
    {
        var hoje = Hoje();
        var dia = hoje.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)hoje.DayOfWeek;
        return new
        {
            clienteId,
            modelo = new
            {
                titulo = "Conferir caixa",
                responsavelId,
                estimativaMinutos = 30,
                visivelCliente = true,
                checklist = new[] { "Somar entradas", "Somar saídas" },
            },
            regra = new { periodicidade = "SEMANAL", diaReferencia = dia, antecedenciaDias = antecedencia, inicio = Data(hoje) },
        };
    }

    private static string Data(DateOnly data) => data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<JsonElement[]> TarefasAsync(Equipe equipe, Guid cliente) =>
        [.. (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/tarefas?clienteId={cliente}"))
            .GetProperty("itens").EnumerateArray()];

    [Fact]
    public async Task Tick_GeraAsDevidasComAntecedencia_EIdempotente()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var eu = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("usuarioId").GetGuid();
        var recorrencia = Cenarios.Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", Semanal(cliente, eu)));

        var tick = await TickAsync();
        await TickAsync();

        var hoje = Hoje();
        var tarefas = await TarefasAsync(equipe, cliente);
        tick.GetProperty("tarefasRecorrentes").GetInt32().Should().BeGreaterThanOrEqualTo(3);
        tarefas.Select(t => t.GetProperty("prazo").GetString()).Should()
            .BeEquivalentTo([Data(hoje), Data(hoje.AddDays(7)), Data(hoje.AddDays(14))]);
        var detalhe = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/tarefas/{Cenarios.Id(tarefas[0])}");
        detalhe.GetProperty("titulo").GetString().Should().Be("Conferir caixa");
        detalhe.GetProperty("recorrenciaId").GetGuid().Should().Be(recorrencia);
        detalhe.GetProperty("responsavel").GetProperty("id").GetGuid().Should().Be(eu);
        detalhe.GetProperty("estimativaMinutos").GetInt32().Should().Be(30);
        detalhe.GetProperty("visivelCliente").GetBoolean().Should().BeTrue();
        detalhe.GetProperty("checklist").GetArrayLength().Should().Be(2);
        var depois = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/recorrencias/{recorrencia}");
        depois.GetProperty("proximaData").GetString().Should().Be(Data(hoje.AddDays(21)));
    }

    [Fact]
    public async Task Pausada_NaoGera()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var recorrencia = Cenarios.Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", Semanal(cliente)));
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/recorrencias/{recorrencia}/pausa");

        await TickAsync();

        (await TarefasAsync(equipe, cliente)).Should().BeEmpty();
    }

    [Fact]
    public async Task ClienteInativo_PulaAsDatasSemGerarNemAcumular()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var recorrencia = Cenarios.Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", Semanal(cliente)));
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/clientes/{cliente}");

        await TickAsync();

        (await TarefasAsync(equipe, cliente)).Should().BeEmpty();
        var depois = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/recorrencias/{recorrencia}");
        depois.GetProperty("proximaData").GetString().Should().Be(Data(Hoje().AddDays(21)));
    }

    [Fact]
    public async Task ResponsavelForaDaEquipe_TarefaNasceSemResponsavel()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var idMembro = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: membro))
            .GetProperty("usuarioId").GetGuid();
        await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", Semanal(cliente, idMembro, antecedencia: 0));
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/usuarios/{idMembro}");

        await TickAsync();

        var tarefas = await TarefasAsync(equipe, cliente);
        tarefas.Should().HaveCount(1);
        var tarefa = tarefas[0];
        tarefa.GetProperty("prazo").GetString().Should().Be(Data(Hoje()));
        tarefa.GetProperty("responsavel").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private async Task<JsonElement> TickAsync()
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/tick", UriKind.Relative));
        requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
        using var resposta = await http.SendAsync(requisicao);
        var corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(corpo).RootElement.Clone();
        json.GetProperty("falhas").GetInt32().Should().Be(0, "tick: {0}", corpo);
        return json;
    }
}
