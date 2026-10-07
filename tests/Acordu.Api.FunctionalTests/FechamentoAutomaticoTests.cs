using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;

namespace Acordu.Api.FunctionalTests;

/// <summary>RF-SOL-09: job diário que fecha as RESOLVIDAS sem resposta do cliente depois de N dias corridos.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class FechamentoAutomaticoTests(ApiFixture api)
{
    [Fact]
    public async Task FechaSoAsResolvidasHaMaisDeNDias_PorTenant_EIdempotente()
    {
        var curto = await Equipe.CriarAsync(api);
        await curto.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/configuracoes/portal", new { diasFechamentoAutomatico = 2 });
        var vencida = await ResolvidaAsync(curto);
        var emAndamento = await Cenarios.SolicitacaoAsync(curto, await Cenarios.ClienteAsync(curto, "Oficina do Zé"));
        var padrao = await Equipe.CriarAsync(api);
        var dentroDoPrazo = await ResolvidaAsync(padrao);

        var inicio = api.Relogio.Agora;
        api.Relogio.Agora = inicio.AddDays(2);
        try
        {
            var primeira = await RodarAsync();
            var segunda = await RodarAsync();

            primeira.GetProperty("fechadas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
            primeira.GetProperty("falhas").GetInt32().Should().Be(0);
            segunda.GetProperty("fechadas").GetInt32().Should().Be(0, "o job é idempotente");
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }

        (await StatusAsync(curto, vencida)).Should().Be("FECHADA");
        (await StatusAsync(curto, emAndamento)).Should().Be("ABERTA");
        (await StatusAsync(padrao, dentroDoPrazo)).Should().Be("RESOLVIDA", "o padrão é 5 dias e só passaram 2");
    }

    [Fact]
    public async Task SemToken_404()
    {
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsync(new Uri("/internal/jobs/fechamento-automatico", UriKind.Relative), null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<Guid> ResolvidaAsync(Equipe equipe)
    {
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, await Cenarios.ClienteAsync(equipe));
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/resolucao",
            new { solucao = new { texto = "Feito" } });
        return solicitacao;
    }

    private static async Task<string?> StatusAsync(Equipe equipe, Guid solicitacao) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}"))
            .GetProperty("status").GetString();

    private async Task<JsonElement> RodarAsync()
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/fechamento-automatico", UriKind.Relative));
        requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
        using var resposta = await http.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }
}
