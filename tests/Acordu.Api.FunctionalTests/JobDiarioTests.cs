using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F8, cenários F8-06 a F8-08: o disparo diário do Scheduler (ADR 0010 §5).</summary>
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

        var inicio = api.Relogio.Agora;
        api.Relogio.Agora = inicio.AddDays(2);
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
        foreach (var contagem in new[] { "auditoriaRemovida", "notificacoesRemovidas", "atividadesRemovidas" })
            resultado.GetProperty(contagem).GetInt32().Should().BeGreaterThanOrEqualTo(0);
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

    [Fact]
    public async Task Passos_UmFalha_OsSeguintesRodamEAFalhaConta()
    {
        var rodados = new List<string>();

        var resultado = await PassosDiario.ExecutarAsync(
            [
                new("primeiro", _ => { rodados.Add("primeiro"); return Task.FromResult(3); }),
                new("quebra", _ => throw new InvalidOperationException("banco fora")),
                new("ultimo", _ => { rodados.Add("ultimo"); return Task.FromResult(5); }),
            ],
            NullLogger.Instance,
            CancellationToken.None);

        rodados.Should().Equal("primeiro", "ultimo");
        resultado.Contagens.Should().Equal(3, 0, 5);
        resultado.Falhas.Should().Be(1);
    }
}
