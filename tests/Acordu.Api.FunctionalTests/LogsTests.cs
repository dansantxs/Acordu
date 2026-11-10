using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Operacao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F13, F13a: logs estruturados para o Cloud Logging e o Error Reporting (F13-01 a F13-04).</summary>
[Collection(ColecaoApi.Nome)]
public sealed class LogsTests(ApiFixture api)
{
    private static readonly Dictionary<string, string?> ComJson = new()
    {
        ["Logs:Formato"] = "json",
        ["App:Versao"] = "abc1234",
    };

    private static readonly string[] CamposEsperados = ["rota", "metodo", "tenantId"];

    private static JsonElement Formatar(
        LogLevel nivel, string mensagem, Exception? excecao = null, IExternalScopeProvider? escopos = null)
    {
        using var saida = new StringWriter();
        new FormatadorLogJson("abc1234").Write(
            new LogEntry<string>(nivel, "Acordu.Teste", new EventId(42), mensagem, excecao, static (m, _) => m), escopos, saida);
        var texto = saida.ToString();
        texto.TrimEnd('\n', '\r').Should().NotContain("\n", "uma entrada é uma linha");
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    [Fact]
    public void Formatador_Informacao_UmaLinhaComOsCamposDoCloudLogging() // F13-01
    {
        var linha = Formatar(LogLevel.Information, "Tick terminou.");

        linha.GetProperty("severity").GetString().Should().Be("INFO");
        linha.GetProperty("message").GetString().Should().Be("Tick terminou.");
        linha.GetProperty("categoria").GetString().Should().Be("Acordu.Teste");
        linha.GetProperty("eventId").GetInt32().Should().Be(42);
        DateTimeOffset.Parse(linha.GetProperty("time").GetString()!, System.Globalization.CultureInfo.InvariantCulture)
            .Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        linha.TryGetProperty("serviceContext", out _).Should().BeFalse("só erro leva o contexto do serviço");
    }

    [Theory]
    [InlineData(LogLevel.Debug, "DEBUG")]
    [InlineData(LogLevel.Information, "INFO")]
    [InlineData(LogLevel.Warning, "WARNING")]
    [InlineData(LogLevel.Error, "ERROR")]
    [InlineData(LogLevel.Critical, "CRITICAL")]
    public void Formatador_Severidade(LogLevel nivel, string esperado) =>
        Formatar(nivel, "x").GetProperty("severity").GetString().Should().Be(esperado);

    [Fact]
    public void Formatador_Erro_PilhaNaMensagemEContextoDoServico() // F13-02
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
        var mensagem = linha.GetProperty("message").GetString()!;
        mensagem.Should().StartWith("Erro inesperado ao processar a requisição.\nSystem.InvalidOperationException");
        mensagem.Should().Contain(" at ", "o Error Reporting agrupa pela pilha");
        linha.GetProperty("@type").GetString().Should().EndWith("ReportedErrorEvent");
        linha.GetProperty("serviceContext").GetProperty("service").GetString().Should().Be("acordu-api");
        linha.GetProperty("serviceContext").GetProperty("version").GetString().Should().Be("abc1234");
    }

    [Fact]
    public void Formatador_DosEscopos_SoOsCamposDoContexto() // F13-04
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
    public async Task Erro500_LinhaJsonComRotaModeloEIdentificadores_SemDadosDaRequisicao() // F13-02, F13-04
    {
        var equipe = await Equipe.CriarAsync(api);
        var capturados = new LogsCapturados();
        await using var app = await api.NovaApiAsync(
            ComJson,
            mapear: grupos => grupos.Interno.MapPost(
                "/teste/log/{id:guid}", IResult (Guid id) => throw new InvalidOperationException($"falha de teste {id.Version}")),
            servicos: s => s.AddSingleton<ILoggerProvider>(capturados));
        capturados.Formatador = app.Services.GetServices<ConsoleFormatter>().OfType<FormatadorLogJson>().Single();
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
        var erros = capturados.Linhas.Where(l => l.GetProperty("severity").GetString() == "ERROR").ToList();
        erros.Should().ContainSingle("o log do ExceptionHandlerMiddleware sai: senão o mesmo 500 conta duas vezes");
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
    public async Task SemFormato_ConsoleDeTexto() // F13-03
    {
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?>());

        app.Services.GetServices<ConsoleFormatter>().OfType<FormatadorLogJson>().Should().BeEmpty();
    }

    [Fact]
    public async Task FormatoDesconhecido_NaoSobe()
    {
        var subir = () => api.NovaApiAsync(new Dictionary<string, string?> { ["Logs:Formato"] = "xml" });

        (await subir.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*Logs__Formato*");
    }

    /// <summary>Provedor que formata cada entrada com o formatador da Api, como o console faria.</summary>
    private sealed class LogsCapturados : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider? _escopos;

        public FormatadorLogJson? Formatador { get; set; }

        public ConcurrentQueue<JsonElement> Linhas { get; } = new();

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _escopos = scopeProvider;

        public ILogger CreateLogger(string categoryName) => new Registro(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class Registro(LogsCapturados dono, string categoria) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => dono._escopos?.Push(state);

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (dono.Formatador is null || !IsEnabled(logLevel))
                    return;
                using var saida = new StringWriter();
                dono.Formatador.Write(new LogEntry<TState>(logLevel, categoria, eventId, state, exception, formatter), dono._escopos, saida);
                if (saida.ToString() is { Length: > 0 } texto)
                    dono.Linhas.Enqueue(JsonDocument.Parse(texto).RootElement.Clone());
            }
        }
    }
}
