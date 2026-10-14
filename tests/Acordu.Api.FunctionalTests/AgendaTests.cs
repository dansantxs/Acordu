using System.Globalization;
using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §11b e read side §6a: a agenda junta solicitações, tarefas, projetos, recorrências e contratos.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class AgendaTests(ApiFixture api)
{
    private DateOnly Hoje() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(api.Relogio.Agora, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);

    private static string Data(DateOnly data) => data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static JsonElement[] Itens(JsonElement agenda, string tipo) =>
        [.. agenda.GetProperty("itens").EnumerateArray().Where(i => i.GetProperty("tipo").GetString() == tipo)];

    [Fact]
    public async Task Agenda_TodosOsTiposEFiltroPorPessoa()
    {
        var equipe = await Equipe.CriarAsync(api);
        var eu = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("usuarioId").GetGuid();
        var hoje = Hoje();
        var cliente = await ClienteAsync(equipe);
        var contrato = await ContratoAsync(equipe, cliente);
        var solicitacao = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, contratoId = contrato, titulo = "Banner", prioridade = "MEDIA", responsavelId = eu,
        }));
        var deApontar = await TarefaAsync(equipe, solicitacao);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = deApontar, data = Data(hoje), duracaoMinutos = 1020, descricao = "Campanha", faturavel = true,
        });
        var planejada = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new
        {
            titulo = "Arte do cardápio", responsavelId = eu, clienteId = cliente, prazo = Data(hoje.AddDays(3)), planejadaPara = Data(hoje.AddDays(1)),
        }));
        var atrasada = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new
        {
            titulo = "Atrasada", prazo = Data(hoje.AddDays(-20)),
        }));
        var projeto = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/projetos", new
        {
            clienteId = cliente, nome = "Campanha", inicio = Data(hoje), prazo = Data(hoje.AddDays(5)),
        }));
        var recorrencia = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", new
        {
            clienteId = cliente,
            modelo = new { titulo = "Posts da semana", responsavelId = eu, estimativaMinutos = 300, visivelCliente = false },
            regra = new { periodicidade = "SEMANAL", diaReferencia = 5, antecedenciaDias = 2, inicio = "2020-01-01" },
        }));

        var agenda = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/agenda");
        var minha = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/agenda?responsavelId={eu}");
        var longa = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Get, $"/api/v1/agenda?de={Data(hoje)}&ate={Data(hoje.AddDays(62))}");

        agenda.GetProperty("dias").GetArrayLength().Should().Be(14);
        agenda.GetProperty("hoje").GetString().Should().Be(Data(hoje));
        agenda.GetProperty("equipe")[0].GetProperty("id").GetGuid().Should().Be(eu);
        var sol = Itens(agenda, "SOLICITACAO").Single(i => Id(i) == solicitacao);
        sol.GetProperty("numero").GetInt32().Should().BePositive();
        sol.GetProperty("responsavel").GetProperty("id").GetGuid().Should().Be(eu);
        sol.GetProperty("cliente").GetProperty("nome").GetString().Should().Be("Padaria Bom Grão");
        var tarefa = Itens(agenda, "TAREFA").Single(i => Id(i) == planejada);
        tarefa.GetProperty("inicio").GetString().Should().Be(Data(hoje.AddDays(1)));
        tarefa.GetProperty("prazo").GetString().Should().Be(Data(hoje.AddDays(3)));
        tarefa.GetProperty("situacao").GetString().Should().Be("NO_PRAZO");
        var vencida = Itens(agenda, "TAREFA").Single(i => Id(i) == atrasada);
        vencida.GetProperty("situacao").GetString().Should().Be("VENCIDO");
        vencida.GetProperty("cliente").ValueKind.Should().Be(JsonValueKind.Null);
        Itens(agenda, "TAREFA").Any(i => Id(i) == deApontar).Should().BeFalse("tarefa sem prazo nem dia planejado fica fora");
        var barra = Itens(agenda, "PROJETO").Single(i => Id(i) == projeto);
        barra.GetProperty("fim").GetString().Should().Be(Data(hoje.AddDays(5)));
        var ocorrencias = Itens(agenda, "RECORRENCIA").Where(i => Id(i) == recorrencia).ToList();
        ocorrencias.Should().NotBeEmpty();
        ocorrencias.Should().OnlyContain(i => i.GetProperty("situacao").GetString() == "PREVISTO"
            && DateOnly.Parse(i.GetProperty("inicio").GetString()!, CultureInfo.InvariantCulture).DayOfWeek == DayOfWeek.Friday);
        var consumo = Itens(agenda, "CONTRATO").Single(i => Id(i) == contrato);
        consumo.GetProperty("marco").GetString().Should().Be("CONSUMO_ALTO");
        consumo.GetProperty("percentual").GetInt32().Should().Be(85);
        consumo.GetProperty("inicio").GetString().Should().Be(Data(hoje));

        Itens(minha, "PROJETO").Should().BeEmpty();
        Itens(minha, "CONTRATO").Should().BeEmpty();
        Itens(minha, "TAREFA").Select(Id).Should().Equal(planejada);
        Itens(minha, "RECORRENCIA").Should().HaveCount(ocorrencias.Count);
        longa.GetProperty("errors").TryGetProperty("ate", out _).Should().BeTrue();
    }

    [Fact]
    public async Task OutroTenant_NaoVeNada()
    {
        var equipe = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new { titulo = "Minha", prazo = Data(Hoje()) });

        var agenda = await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/agenda");

        agenda.GetProperty("itens").GetArrayLength().Should().Be(0);
    }
}
