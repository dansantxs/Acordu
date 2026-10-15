using System.Globalization;
using System.Net;
using Acordu.Api.FunctionalTests.Apoio;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §11d e segurança §5.5 (RF-COL-04): link secreto da agenda, feed ICS, modo discreto e revogação.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class AgendaIcsTests(ApiFixture api)
{
    private DateOnly Hoje() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(api.Relogio.Agora, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);

    private async Task<(HttpStatusCode Status, string Corpo, string? Tipo)> BaixarAsync(string url)
    {
        using var http = api.NovoHttp();
        using var resposta = await http.GetAsync(new Uri(new Uri(url).PathAndQuery, UriKind.Relative));
        return (resposta.StatusCode, await resposta.Content.ReadAsStringAsync(), resposta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Link_FeedModoDiscretoRenovacaoERevogacao()
    {
        var equipe = await Equipe.CriarAsync(api);
        var eu = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("usuarioId").GetGuid();
        var cliente = await ClienteAsync(equipe);
        var contrato = await ContratoAsync(equipe, cliente);
        var amanha = Hoje().AddDays(1);
        var tarefa = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new
        {
            titulo = "Arte do cardápio, versão final", responsavelId = eu, clienteId = cliente,
            planejadaPara = amanha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        }));
        var solicitacao = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, contratoId = contrato, titulo = "Banner", prioridade = "MEDIA", responsavelId = eu,
        }));
        await TarefaAsync(equipe, titulo: "De ninguém");

        var antes = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/agenda/assinatura");
        var link = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/agenda/assinatura", new { discreta = false });
        var url = link.GetProperty("url").GetString()!;
        var (status, ics, tipo) = await BaixarAsync(url);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, "/api/v1/agenda/assinatura", new { discreta = true });
        var (_, discreto, _) = await BaixarAsync(url);
        var novo = (await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/agenda/assinatura", new { discreta = false }))
            .GetProperty("url").GetString()!;
        var (antigoStatus, _, _) = await BaixarAsync(url);
        var (novoStatus, _, _) = await BaixarAsync(novo);
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, "/api/v1/agenda/assinatura");
        var (revogadoStatus, _, _) = await BaixarAsync(novo);
        var depois = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/agenda/assinatura");
        var trilha = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auditoria?acao=agenda.link.emissao"))
            .GetProperty("itens");

        antes.GetProperty("ativa").GetBoolean().Should().BeFalse();
        url.Should().MatchRegex(@"/api/v1/agenda/[A-Za-z0-9_-]{43}\.ics$");
        status.Should().Be(HttpStatusCode.OK);
        tipo.Should().Be("text/calendar");
        ics.Should().StartWith("BEGIN:VCALENDAR\r\n").And.EndWith("END:VCALENDAR\r\n");
        ics.Should().Contain($"UID:tarefa-{tarefa}@acordu");
        ics.Should().Contain("DTSTART;VALUE=DATE:" + amanha.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        ics.Should().Contain("SUMMARY:Arte do cardápio\\, versão final · Padaria Bom Grão");
        ics.Should().Contain($"UID:solicitacao-{solicitacao}@acordu");
        ics.Should().NotContain("De ninguém");
        ics.Split("\r\n").Should().OnlyContain(l => System.Text.Encoding.UTF8.GetByteCount(l) <= 75);
        discreto.Should().Contain("SUMMARY:Tarefa\r\n").And.Contain("SUMMARY:Prazo de solicitação\r\n");
        discreto.Should().NotContain("Padaria").And.NotContain("cardápio");
        antigoStatus.Should().Be(HttpStatusCode.NotFound);
        novoStatus.Should().Be(HttpStatusCode.OK);
        revogadoStatus.Should().Be(HttpStatusCode.NotFound);
        depois.GetProperty("ativa").GetBoolean().Should().BeFalse();
        trilha.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task TokenDesconhecido404_ESemLinkPatch404()
    {
        var equipe = await Equipe.CriarAsync(api);

        var (status, _, _) = await BaixarAsync("http://localhost/api/v1/agenda/nao-existe.ics");
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Patch, "/api/v1/agenda/assinatura", new { discreta = true });

        status.Should().Be(HttpStatusCode.NotFound);
    }
}
