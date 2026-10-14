using System.Globalization;
using System.Net;
using Acordu.Api.FunctionalTests.Apoio;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §11c e read side §7a: horas planejadas da semana × capacidade, por pessoa.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class CapacidadeTests(ApiFixture api)
{
    private DateOnly Hoje() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(api.Relogio.Agora, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);

    private static string Data(DateOnly data) => data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Semana_PlanejadoPorPessoaESemResponsavel()
    {
        var equipe = await Equipe.CriarAsync(api);
        var eu = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("usuarioId").GetGuid();
        var hoje = Hoje();
        var segunda = hoje.AddDays(-(((int)hoje.DayOfWeek + 6) % 7));
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, $"/api/v1/usuarios/{eu}/capacidade", new { capacidadeSemanalMinutos = 1200 });
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new
        {
            titulo = "Planejada", responsavelId = eu, planejadaPara = Data(segunda.AddDays(1)), prazo = Data(segunda.AddDays(20)), estimativaMinutos = 300,
        });
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new
        {
            titulo = "Pelo prazo", responsavelId = eu, prazo = Data(segunda.AddDays(2)),
        });
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new
        {
            titulo = "Outra semana", responsavelId = eu, prazo = Data(segunda.AddDays(9)), estimativaMinutos = 600,
        });
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new
        {
            titulo = "De ninguém", prazo = Data(segunda.AddDays(3)), estimativaMinutos = 60,
        });

        var semana = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/capacidade?semana={Data(segunda.AddDays(4))}");

        semana.GetProperty("semana").GetString().Should().Be(Data(segunda));
        var pessoa = semana.GetProperty("pessoas")[0];
        pessoa.GetProperty("pessoa").GetProperty("id").GetGuid().Should().Be(eu);
        pessoa.GetProperty("capacidadeMinutos").GetInt32().Should().Be(1200);
        pessoa.GetProperty("percentual").GetInt32().Should().Be(25);
        var carga = pessoa.GetProperty("carga");
        carga.GetProperty("planejadoMinutos").GetInt32().Should().Be(300);
        carga.GetProperty("tarefas").GetInt32().Should().Be(2);
        carga.GetProperty("tarefasSemEstimativa").GetInt32().Should().Be(1);
        carga.GetProperty("porDia")[1].GetProperty("planejadoMinutos").GetInt32().Should().Be(300);
        semana.GetProperty("semResponsavel").GetProperty("planejadoMinutos").GetInt32().Should().Be(60);
    }
}
