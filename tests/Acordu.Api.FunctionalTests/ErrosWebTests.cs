using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Acordu.Api.Composicao;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Operacao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F13, F13d: <c>POST /api/v1/erros-web</c>, os erros do app no navegador (F13-15, F13-18 a F13-20).</summary>
[Collection(ColecaoApi.Nome)]
public sealed class ErrosWebTests(ApiFixture api)
{
    private const string Rota = "/api/v1/erros-web";

    /// <summary>Letras só (números viram "#"): cada teste com uma mensagem própria, para não colidir na deduplicação.</summary>
    private static string Unica() => new(Enumerable.Range(0, 12).Select(_ => (char)('a' + Random.Shared.Next(26))).ToArray());

    private async Task<(WebApplication App, DiscordFalso Discord, LogsCapturados Logs)> ApiAsync(string? limite = null)
    {
        var discord = new DiscordFalso();
        var logs = new LogsCapturados();
        var app = await api.NovaApiAsync(
            new Dictionary<string, string?>
            {
                ["Discord:Alertas"] = DiscordFalso.Alertas,
                ["Logs:Formato"] = "json",
                ["RateLimit:ErrosWeb:PorMinuto"] = limite ?? "100000",
            },
            servicos: s =>
            {
                s.AddHttpClient(ComposicaoApi.NomeClienteDiscord).ConfigurePrimaryHttpMessageHandler(() => discord);
                s.AddSingleton<ILoggerProvider>(logs);
            });
        logs.Formatador = app.Services.GetServices<ConsoleFormatter>().OfType<FormatadorLogJson>().Single();
        return (app, discord, logs);
    }

    private static async Task<HttpResponseMessage> EnviarAsync(WebApplication app, object corpo)
    {
        using var http = app.GetTestClient();
        return await http.PostAsJsonAsync(new Uri(Rota, UriKind.Relative), corpo);
    }

    private static object Erro(string mensagem, string? rota = "/tarefas") => new
    {
        mensagem,
        pilha = $"TypeError: {mensagem}\n    at Ab (https://acordu.app.br/assets/index-B3x9aZk1.js:12:345)\n    at https://acordu.app.br/assets/vendor-Cc81aa9q.js:1:2",
        rota,
        versao = "abc1234",
        navegador = "Mozilla/5.0 (X11; Linux x86_64) Chrome/130.0",
    };

    [Fact]
    public async Task Registrar_ErroDoApp_204LinhaDoAcorduWebEAvisoComAImpressao() // F13-15
    {
        var (app, discord, logs) = await ApiAsync();
        await using var montada = app;
        var mensagem = $"x.{Unica()} is not a function";

        using var resposta = await EnviarAsync(app, Erro(mensagem));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var linha = logs.Linhas.Single(l => l.GetProperty("severity").GetString() == "ERROR");
        linha.GetProperty("serviceContext").GetProperty("service").GetString().Should().Be("acordu-web");
        linha.GetProperty("stack_trace").GetString().Should().StartWith($"TypeError: {mensagem}\n    at Ab (");
        linha.GetProperty("@type").GetString().Should().EndWith("ReportedErrorEvent");
        linha.GetProperty("contexto").GetProperty("rota").GetString().Should().Be("/tarefas");
        linha.GetProperty("message").GetString().Should().Contain(mensagem).And.Contain("abc1234");
        var aviso = discord.Textos(DiscordFalso.Alertas).Should().ContainSingle().Subject;
        aviso.Should().StartWith("**Erro novo** · acordu-web `abc1234`");
        aviso.Should().Contain($"`{mensagem}` em `Ab (assets/index.js)`").And.Contain("`/tarefas`");
    }

    [Fact]
    public async Task Registrar_ComDadosNaMensagemENaRota_LimpaAntesDeRegistrar() // F13-15, F13-20
    {
        var (app, discord, logs) = await ApiAsync();
        await using var montada = app;
        var id = Guid.NewGuid();
        var unica = Unica();

        using var resposta = await EnviarAsync(app, new
        {
            mensagem = $"{unica}: ana@exemplo.com não achou {id} no pedido 123456",
            pilha = $"Error\n    at f (https://acordu.app.br/assets/index-B3x9aZk1.js?token=segredo:1:2)",
            rota = $"/portal/solicitacoes/{id}?aba=2#topo",
            versao = "abc1234",
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var bruto = string.Join("\n", logs.Linhas.Select(l => l.GetRawText()).Concat(discord.Textos(DiscordFalso.Alertas)));
        bruto.Should().NotContain("ana@exemplo.com").And.NotContain(id.ToString()).And.NotContain("123456").And.NotContain("segredo");
        bruto.Should().NotContain("aba=2");
        var linha = logs.Linhas.Single(l => l.GetProperty("severity").GetString() == "ERROR");
        linha.GetProperty("contexto").GetProperty("rota").GetString().Should().Be("/portal/solicitacoes/:id");
        linha.GetProperty("message").GetString().Should().Contain($"{unica}: [email] não achou :id no pedido #");
    }

    [Fact]
    public async Task Registrar_MesmoErroComOutroNumero_AvisaUmaVez() // F13-18
    {
        var (app, discord, _) = await ApiAsync();
        await using var montada = app;
        var unica = Unica();

        using var primeira = await EnviarAsync(app, Erro($"{unica} falhou na linha 12"));
        using var segunda = await EnviarAsync(app, Erro($"{unica} falhou na linha 87"));

        segunda.StatusCode.Should().Be(HttpStatusCode.NoContent);
        discord.Textos(DiscordFalso.Alertas).Should().ContainSingle().Which.Should().Contain($"`{unica} falhou na linha #`");
    }

    [Fact]
    public async Task Registrar_SemLogin_Aceita() // F13-19 (rota pública)
    {
        using var resposta = await EnviarAsync(api.App, Erro($"{Unica()} na tela de entrar", "/entrar"));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Registrar_AlemDoLimitePorIp_429() // F13-19
    {
        var (app, _, _) = await ApiAsync(limite: "3");
        await using var montada = app;
        var unica = Unica();

        var status = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            using var resposta = await EnviarAsync(app, Erro($"{unica} limite"));
            status.Add(resposta.StatusCode);
        }

        status.Should().Equal(HttpStatusCode.NoContent, HttpStatusCode.NoContent, HttpStatusCode.NoContent, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Registrar_CorpoAcimaDe16KB_413() // F13-19
    {
        using var http = api.NovoHttp();
        var grande = JsonSerializer.Serialize(new { mensagem = "x", pilha = new string('a', 17 * 1024) });
        using var conteudo = new StringContent(grande, Encoding.UTF8, "application/json");

        using var resposta = await http.PostAsync(new Uri(Rota, UriKind.Relative), conteudo);

        resposta.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString().Should().EndWith("/corpo-grande-demais");
    }

    [Fact]
    public async Task Registrar_CorpoSemTamanhoAcimaDe16KB_413() // F13-19
    {
        using var http = api.NovoHttp();
        var grande = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { mensagem = "x", pilha = new string('a', 17 * 1024) }));
        using var conteudo = new StreamContent(new MemoryStream(grande));
        conteudo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri(Rota, UriKind.Relative)) { Content = conteudo };
        pedido.Headers.TransferEncodingChunked = true;

        using var resposta = await http.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Registrar_VersaoDoRelato_NoServiceContext() // F13-15
    {
        var (app, _, logs) = await ApiAsync();
        await using var montada = app;

        using var resposta = await EnviarAsync(app, new { mensagem = Unica(), versao = "velha12" });

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        logs.Linhas.Single(l => l.GetProperty("severity").GetString() == "ERROR")
            .GetProperty("serviceContext").GetProperty("version").GetString().Should().Be("velha12", "uma aba com o SPA antigo mostra a versão dela");
    }

    [Fact]
    public async Task Registrar_CamposLongos_Cortados() // F13-19
    {
        var (app, _, logs) = await ApiAsync();
        await using var montada = app;

        using var resposta = await EnviarAsync(app, new { mensagem = Unica() + new string('m', 2000), rota = "/" + new string('r', 600) });

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var linha = logs.Linhas.Single(l => l.GetProperty("severity").GetString() == "ERROR");
        linha.GetProperty("contexto").GetProperty("rota").GetString()!.Length.Should().BeLessThanOrEqualTo(200);
        linha.GetProperty("message").GetString()!.Length.Should().BeLessThan(700, "a mensagem é cortada em 500");
    }

    [Fact]
    public async Task Registrar_SemMensagem_400NoCampo()
    {
        using var resposta = await EnviarAsync(api.App, new { pilha = "x", rota = "/" });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("mensagem", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("    at Ab (https://acordu.app.br/assets/index-B3x9aZk1.js:12:345)", "Ab (assets/index.js)")]
    [InlineData("    at https://acordu.app.br/assets/index-B3x9aZk1.js:1:2", "(anônima) (assets/index.js)")]
    [InlineData("Ab@https://acordu.app.br/assets/index-B3x9aZk1.js:12:345", "Ab (assets/index.js)")]
    [InlineData("@https://acordu.app.br/assets/chunk-Dd7_kq0P.js:3:4", "(anônima) (assets/chunk.js)")]
    [InlineData("    at Ab (https://acordu.app.br/assets/index-BbhaJrIF.js:1:144523)", "Ab (assets/index.js)")]
    [InlineData("    at async Carregar (https://acordu.app.br/assets/consultas--pNxrSBH.js:1:20345)", "Carregar (assets/consultas.js)")]
    [InlineData("    at new Painel (https://acordu.app.br/assets/_app-DUkjv1h-.js:2:3)", "Painel (assets/_app.js)")]
    [InlineData("    at Object.salvar [as onPress] (https://acordu.app.br/assets/Button-BE1-a82j.js:9:88)", "Object.salvar (assets/Button.js)")]
    public void PrimeiroQuadro_ChromeEFirefox_FuncaoEArquivoSemHash(string linha, string esperado) =>
        ErrosWebEndpoints.PrimeiroQuadro(ErrosWebEndpoints.Limpar($"TypeError: x\n{linha}", 4000)).Should().Be(esperado);

    [Theory]
    [InlineData("CPF 123.456.789-00 inválido", "CPF # inválido")]
    [InlineData("CNPJ 12.345.678/0001-90", "CNPJ #")]
    [InlineData("ligar (18) 99999-1234", "ligar #")]
    [InlineData("link /p/AbCdEfGhIjKlMnOpQrStUvWxYz0123456789_-abc expirou", "link /p/[token] expirou")]
    [InlineData("Unexpected token '?' em a?.b", "Unexpected token '?' em a?.b")]
    [InlineData("x `](https://mal.site) y", "x '](https://mal.site) y")]
    [InlineData("versão 1.2.3 na linha 12", "versão 1.2.3 na linha 12")]
    public void Limpar_DadosEMarkdown_Saem(string texto, string esperado) =>
        ErrosWebEndpoints.Limpar(texto, 500).Should().Be(esperado);

    [Theory]
    [InlineData("/clientes/7c1f0e2a-1b2c-4d5e-8f90-a1b2c3d4e5f6/contratos?aba=2", "/clientes/:id/contratos")]
    [InlineData("/tarefas/42", "/tarefas/:id")]
    [InlineData("//mal.site/x", "(sem rota)")]
    [InlineData("tarefas", "(sem rota)")]
    public void RotaModelo_IdsEQuery_Saem(string rota, string esperado) =>
        ErrosWebEndpoints.RotaModelo(rota).Should().Be(esperado);
}
