using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Acordu.Api.Operacao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F13, F13a: a linha JSON do log (F13-01, F13-02 e F13-04 nos escopos).</summary>
public sealed class FormatadorLogJsonTests
{
    private static readonly string[] CamposEsperados = ["rota", "metodo", "tenantId"];

    private static JsonElement Formatar(
        LogLevel nivel, string mensagem, Exception? excecao = null, IExternalScopeProvider? escopos = null)
    {
        using var saida = new StringWriter(CultureInfo.InvariantCulture);
        new FormatadorLogJson("abc1234").Write(
            new LogEntry<string>(nivel, "Acordu.Teste", new EventId(42), mensagem, excecao, static (m, _) => m), escopos, saida);
        var texto = saida.ToString();
        texto.TrimEnd('\n', '\r').Should().NotContain("\n", "uma entrada é uma linha");
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    [Fact]
    public void Write_Informacao_UmaLinhaComOsCamposDoCloudLogging() // F13-01
    {
        var linha = Formatar(LogLevel.Information, "Tick terminou.");

        linha.GetProperty("severity").GetString().Should().Be("INFO");
        linha.GetProperty("message").GetString().Should().Be("Tick terminou.");
        linha.GetProperty("categoria").GetString().Should().Be("Acordu.Teste");
        linha.GetProperty("eventId").GetInt32().Should().Be(42);
        DateTimeOffset.Parse(linha.GetProperty("time").GetString()!, CultureInfo.InvariantCulture)
            .Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        linha.TryGetProperty("traceId", out _).Should().BeFalse("fora de uma requisição não há trace");
        linha.TryGetProperty("serviceContext", out _).Should().BeFalse("só erro leva o contexto do serviço");
    }

    [Fact]
    public void Write_DentroDeUmaAtividade_TraceIdDela() // F13-01
    {
        using var atividade = new Activity("teste").SetIdFormat(ActivityIdFormat.W3C).Start();

        var linha = Formatar(LogLevel.Warning, "x");

        linha.GetProperty("traceId").GetString().Should().Be(atividade.TraceId.ToHexString());
    }

    [Theory]
    [InlineData(LogLevel.Debug, "DEBUG")]
    [InlineData(LogLevel.Information, "INFO")]
    [InlineData(LogLevel.Warning, "WARNING")]
    [InlineData(LogLevel.Error, "ERROR")]
    [InlineData(LogLevel.Critical, "CRITICAL")]
    public void Write_CadaNivel_SeveridadeDoCloudLogging(LogLevel nivel, string esperado) =>
        Formatar(nivel, "x").GetProperty("severity").GetString().Should().Be(esperado);

    [Fact]
    public void Write_Erro_PilhaEContextoDoServico() // F13-02
    {
        Exception excecao;
        try
        {
            throw new InvalidOperationException("falhou\nem duas linhas");
        }
        catch (InvalidOperationException e)
        {
            excecao = e;
        }

        var linha = Formatar(LogLevel.Error, "Erro inesperado ao processar a requisição.", excecao);

        linha.GetProperty("severity").GetString().Should().Be("ERROR");
        linha.GetProperty("message").GetString().Should().Be("Erro inesperado ao processar a requisição.");
        var pilha = linha.GetProperty("stack_trace").GetString()!;
        pilha.Should().StartWith("System.InvalidOperationException");
        pilha.Should().Contain(" at ", "o Error Reporting agrupa pela pilha");
        linha.GetProperty("@type").GetString().Should().EndWith("ReportedErrorEvent");
        linha.GetProperty("serviceContext").GetProperty("service").GetString().Should().Be("acordu-api");
        linha.GetProperty("serviceContext").GetProperty("version").GetString().Should().Be("abc1234");
    }

    [Fact]
    public void Write_DosEscopos_SoOsCamposDoContexto() // F13-04
    {
        var escopos = new LoggerExternalScopeProvider();
        using var hospedagem = escopos.Push(new Dictionary<string, object?>
        {
            ["RequestPath"] = "/api/v1/clientes/7c1f",
            ["RequestId"] = "0HN1",
        });
        using var nosso = escopos.Push(new Dictionary<string, object?>
        {
            ["rota"] = "/api/v1/clientes/{id}",
            ["metodo"] = "PATCH",
            ["tenantId"] = "t-1",
            ["email"] = "ana@exemplo.com",
        });
        using var texto = escopos.Push("um escopo de texto");

        var contexto = Formatar(LogLevel.Error, "x", escopos: escopos).GetProperty("contexto");

        contexto.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(CamposEsperados);
        contexto.GetProperty("rota").GetString().Should().Be("/api/v1/clientes/{id}");
        contexto.GetRawText().Should().NotContain("ana@exemplo.com").And.NotContain("7c1f");
    }

    [Fact]
    public void Write_ErroDeOutroServicoComPilhaEmTexto_ServicoEPilhaDoEscopo() // F13-15
    {
        var escopos = new LoggerExternalScopeProvider();
        using var escopo = escopos.Push(new Dictionary<string, object?>
        {
            [FormatadorLogJson.EscopoServico] = "acordu-web",
            [FormatadorLogJson.EscopoPilha] = "TypeError: x\n    at f (index.js:1:2)",
            ["rota"] = "/tarefas",
        });

        var linha = Formatar(LogLevel.Error, "Erro no app.", escopos: escopos);

        linha.GetProperty("serviceContext").GetProperty("service").GetString().Should().Be("acordu-web");
        linha.GetProperty("stack_trace").GetString().Should().StartWith("TypeError: x");
        linha.GetProperty("@type").GetString().Should().EndWith("ReportedErrorEvent");
        linha.GetProperty("contexto").EnumerateObject().Select(p => p.Name).Should().ContainSingle().Which.Should().Be("rota");
    }
}
