using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F8, cenários F8-06 e F8-07: o disparo diário do Scheduler (ADR 0010 §5).</summary>
[Collection(ColecaoApi.Nome)]
public sealed class JobDiarioTests(ApiFixture api)
{
    [Fact]
    public async Task Diario_ComToken_FechaExpurgaESomaAsContagens()
    {
        var equipe = await Equipe.CriarAsync(api);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/configuracoes/portal", new { diasFechamentoAutomatico = 2 });
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, await Cenarios.ClienteAsync(equipe));
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/resolucao",
            new { solucao = new { texto = "Feito" } });

        // 91 dias depois: a solicitação passou do fechamento (2 dias) e a atividade da abertura passou da retenção do
        // feed (90 dias), então o fechamento e o expurgo precisam ter rodado.
        var inicio = api.Relogio.Agora;
        api.Relogio.Agora = inicio.AddDays(91);
        JsonElement resultado;
        try
        {
            using var http = api.NovoHttp();
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/diario", UriKind.Relative));
            requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
            using var resposta = await http.SendAsync(requisicao);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            resultado = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }

        resultado.GetProperty("fechadas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        resultado.GetProperty("falhas").GetInt32().Should().Be(0);
        resultado.GetProperty("atividadesRemovidas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        resultado.GetProperty("auditoriaRemovida").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        resultado.GetProperty("notificacoesRemovidas").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}"))
            .GetProperty("status").GetString().Should().Be("FECHADA");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("token-errado")]
    public async Task Diario_SemTokenOuComTokenErrado_404(string? token)
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/diario", UriKind.Relative));
        if (token is not null)
            requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, token);

        using var resposta = await http.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
