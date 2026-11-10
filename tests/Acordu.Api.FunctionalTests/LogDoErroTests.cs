using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Operacao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F13, F13a: o erro inesperado no log, com o contexto do <c>ManipuladorExcecoes</c> (F13-02, F13-04).</summary>
[Collection(ColecaoApi.Nome)]
public sealed class LogDoErroTests(ApiFixture api)
{
    private async Task<(WebApplication App, LogsCapturados Logs)> ApiComLogsAsync()
    {
        var logs = new LogsCapturados();
        var app = await api.NovaApiAsync(
            new Dictionary<string, string?> { ["Logs:Formato"] = "json", ["App:Versao"] = "abc1234" },
            mapear: grupos =>
            {
                grupos.Interno.MapPost(
                    "/teste/log/{id:guid}", IResult (Guid id) => throw new InvalidOperationException($"falha de teste {id.Version}"));
                grupos.Interno.MapGet("/teste/log/regra", IResult () => throw new ArgumentException("Nome inválido.", "nome"));
            },
            servicos: s => s.AddSingleton<ILoggerProvider>(logs));
        logs.Formatador = app.Services.GetServices<ConsoleFormatter>().OfType<FormatadorLogJson>().Single();
        return (app, logs);
    }

    [Fact]
    public async Task Erro500_ComDadosNaRequisicao_UmaLinhaComRotaModeloEIdentificadoresSemOsDados() // F13-02, F13-04
    {
        var equipe = await Equipe.CriarAsync(api);
        var (app, logs) = await ApiComLogsAsync();
        await using var montada = app;
        var alvo = Guid.NewGuid();
        using var http = app.GetTestClient();
        using var pedido = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/v1/teste/log/{alvo}?email=ana%40exemplo.com", UriKind.Relative))
        {
            Content = JsonContent.Create(new { observacao = "corpo-secreto" }),
        };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", equipe.Dono);
        pedido.Headers.Add("X-Sensivel", "cabecalho-secreto");

        using var resposta = await http.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var erros = logs.Linhas.Where(l => l.GetProperty("severity").GetString() == "ERROR").ToList();
        erros.Should().ContainSingle("o diagnóstico do ExceptionHandlerMiddleware é suprimido: senão o mesmo 500 conta duas vezes");
        var linha = erros[0];
        linha.GetProperty("categoria").GetString().Should().Be("Acordu.Api.Erros.ManipuladorExcecoes");
        linha.GetProperty("serviceContext").GetProperty("version").GetString().Should().Be("abc1234");
        var contexto = linha.GetProperty("contexto");
        contexto.GetProperty("rota").GetString().Should().Be("/api/v1/teste/log/{id:guid}");
        contexto.GetProperty("metodo").GetString().Should().Be("POST");
        contexto.GetProperty("tenantId").GetGuid().Should().Be(equipe.TenantId);
        Guid.TryParse(contexto.GetProperty("usuarioId").GetString(), out _).Should().BeTrue();
        var bruto = linha.GetRawText();
        bruto.Should().NotContain("ana%40").And.NotContain("ana@exemplo").And.NotContain("corpo-secreto").And.NotContain("cabecalho-secreto");
        bruto.Should().NotContain(alvo.ToString(), "a rota vai como modelo, sem os valores");
    }

    [Fact]
    public async Task ErroDeRegra_4xx_NaoViraErroNoLog()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (app, logs) = await ApiComLogsAsync();
        await using var montada = app;
        using var http = app.GetTestClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/teste/log/regra", UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", equipe.Dono);

        using var resposta = await http.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        logs.Linhas.Should().NotContain(l => l.GetProperty("severity").GetString() == "ERROR", "4xx não é evento no Error Reporting");
    }
}
