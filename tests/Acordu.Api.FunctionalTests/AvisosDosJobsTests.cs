using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.Composicao;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F13, F13c: o diário sem falhas manda o resumo em #deploy, uma vez por dia (F13-14).</summary>
[Collection(ColecaoApi.Nome)]
public sealed class AvisosDosJobsTests(ApiFixture api)
{
    private static async Task<JsonElement> DiarioAsync(WebApplication app)
    {
        using var http = app.GetTestClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/diario", UriKind.Relative));
        requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
        using var resposta = await http.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Diario_SemFalhas_ResumoEmDeployUmaVezPorDia() // F13-14
    {
        var discord = new DiscordFalso();
        await using var app = await api.NovaApiAsync(
            new Dictionary<string, string?> { ["Discord:Deploy"] = DiscordFalso.Deploy, ["Discord:Alertas"] = DiscordFalso.Alertas },
            servicos: s => s.AddHttpClient(ComposicaoApi.NomeClienteDiscord).ConfigurePrimaryHttpMessageHandler(() => discord));

        var primeiro = await DiarioAsync(app);
        await DiarioAsync(app);

        primeiro.GetProperty("falhas").GetInt32().Should().Be(0);
        primeiro.TryGetProperty("marcasOperacaoRemovidas", out _).Should().BeTrue();
        var resumo = discord.Textos(DiscordFalso.Deploy).Should().ContainSingle("o segundo disparo no mesmo dia não repete").Subject;
        resumo.Should().StartWith("**Diário ok** · ").And.Contain("fechadas ").And.Contain("ocorrências de erro ");
        discord.Textos(DiscordFalso.Alertas).Should().BeEmpty();
    }
}
