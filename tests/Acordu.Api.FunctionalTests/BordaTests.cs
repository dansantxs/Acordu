using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Api.Seguranca;
using Acordu.Application.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F8, cenários F8-09 a F8-15: a API atrás do Worker de <c>api.acordu.app.br</c> (ADR 0010 §3 e §4), cada
/// cenário numa Api montada com a configuração dele, sobre o mesmo banco.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class BordaTests(ApiFixture api)
{
    private const string Segredo = "segredo-da-borda-nos-testes";
    private const string Cliente = "203.0.113.7";
    private const string Worker = "198.51.100.2";

    [Fact]
    public async Task TokenDeOrigem_SemCabecalho_404EmQualquerRota()
    {
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?> { ["Proxy:TokenOrigem"] = Segredo });
        using var http = app.GetTestClient();

        using var health = await http.GetAsync(new Uri("/api/health", UriKind.Relative));
        using var errado = await EnviarAsync(http, HttpMethod.Get, "/api/health", origem: "outro-valor");
        using var job = await EnviarAsync(http, HttpMethod.Post, "/internal/jobs/tick", job: true);
        using var login = await http.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative), new { email = "a@exemplo.com", senha = "x" });

        health.StatusCode.Should().Be(HttpStatusCode.NotFound);
        errado.StatusCode.Should().Be(HttpStatusCode.NotFound);
        job.StatusCode.Should().Be(HttpStatusCode.NotFound, "o token do job não substitui o da borda");
        login.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TokenDeOrigem_ComCabecalho_SegueSemLevarOCabecalhoAdiante()
    {
        await using var app = await api.NovaApiAsync(
            new Dictionary<string, string?> { ["Proxy:TokenOrigem"] = Segredo },
            g => g.Base.MapGet("/teste-borda/cabecalho", (HttpContext http) =>
                Results.Ok(new { presente = http.Request.Headers.ContainsKey(TokenDeOrigem.Cabecalho) })));
        using var http = app.GetTestClient();

        using var health = await EnviarAsync(http, HttpMethod.Get, "/api/health", origem: Segredo);
        using var eco = await EnviarAsync(http, HttpMethod.Get, "/api/v1/teste-borda/cabecalho", origem: Segredo);

        health.StatusCode.Should().Be(HttpStatusCode.OK);
        eco.StatusCode.Should().Be(HttpStatusCode.OK);
        (await eco.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("presente").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task DoisSaltos_ComTokenDeOrigem_UsaOIpDoCliente()
    {
        var ip = await IpVistoAsync(new Dictionary<string, string?>
        {
            ["Proxy:TokenOrigem"] = Segredo,
            ["Proxy:ConfiarEncaminhamento"] = "true",
            ["Proxy:Saltos"] = "2",
        });

        ip.Should().Be(Cliente);
    }

    [Fact]
    public async Task DoisSaltos_SemTokenDeOrigem_ValeUmSalto()
    {
        var ip = await IpVistoAsync(new Dictionary<string, string?>
        {
            ["Proxy:ConfiarEncaminhamento"] = "true",
            ["Proxy:Saltos"] = "2",
        });

        ip.Should().Be(Worker, "sem a borda obrigatória, quem chama direto escolheria o próprio IP");
    }

    [Fact]
    public async Task UrlApi_Configurada_LinkDaAgendaUsaOEnderecoDaApi()
    {
        // Mesma chave JWT e mesmo banco: o token emitido pela Api do fixture vale na Api com App:UrlApi.
        var equipe = await Equipe.CriarAsync(api);
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?>
        {
            ["App:UrlPublica"] = "https://acordu.app.br/",
            ["App:UrlApi"] = "https://api.acordu.app.br",
        });
        using var http = app.GetTestClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/agenda/assinatura", UriKind.Relative))
        {
            Content = JsonContent.Create(new { discreta = false }),
        };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", equipe.Dono);

        using var resposta = await http.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var link = new Uri((await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!);
        link.Host.Should().Be("api.acordu.app.br");
        link.AbsolutePath.Should().StartWith("/api/v1/agenda/");
        app.Services.GetRequiredService<EnderecosPublicos>().SolicitacaoInterna(Guid.Empty).Host
            .Should().Be("acordu.app.br", "os links do SPA continuam no endereço público");
    }

    [Fact]
    public async Task Sse_Desligado_RotaDeEventosResponde404()
    {
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?> { ["TempoReal:Sse"] = "false" });
        using var http = app.GetTestClient();
        using var padrao = api.NovoHttp();

        using var desligado = await http.GetAsync(new Uri("/api/v1/eventos", UriKind.Relative));
        using var ligado = await padrao.GetAsync(new Uri("/api/v1/eventos", UriKind.Relative));

        desligado.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ligado.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "sem a chave o SSE continua existindo");
    }

    [Fact]
    public async Task Cors_SoAOrigemDoSpa_ComCredenciais()
    {
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?> { ["Cors:Origem"] = "https://acordu.app.br" });
        using var http = app.GetTestClient();

        using var spa = await PreflightAsync(http, "https://acordu.app.br");
        using var outra = await PreflightAsync(http, "https://exemplo.com");

        spa.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("https://acordu.app.br");
        spa.Headers.GetValues("Access-Control-Allow-Credentials").Should().Equal("true");
        outra.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    private async Task<string?> IpVistoAsync(Dictionary<string, string?> configuracao)
    {
        await using var app = await api.NovaApiAsync(
            configuracao,
            g => g.Base.MapGet("/teste-borda/ip", (IOrigemRequisicao origem) => Results.Ok(new { ip = origem.Ip })),
            // A conexão chega do front do Google, como no Cloud Run.
            a => a.Use((http, proximo) =>
            {
                http.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
                return proximo(http);
            }));
        using var http = app.GetTestClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/teste-borda/ip", UriKind.Relative));
        requisicao.Headers.Add("X-Forwarded-For", $"{Cliente}, {Worker}");
        if (configuracao.ContainsKey("Proxy:TokenOrigem"))
            requisicao.Headers.Add(TokenDeOrigem.Cabecalho, Segredo);
        using var resposta = await http.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ip").GetString();
    }

    private static async Task<HttpResponseMessage> PreflightAsync(HttpClient http, string origem)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Options, new Uri("/api/v1/auth/refresh", UriKind.Relative));
        requisicao.Headers.Add("Origin", origem);
        requisicao.Headers.Add("Access-Control-Request-Method", "POST");
        return await http.SendAsync(requisicao);
    }

    private static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient http, HttpMethod metodo, string rota, string? origem = null, bool job = false)
    {
        using var requisicao = new HttpRequestMessage(metodo, new Uri(rota, UriKind.Relative));
        if (origem is not null)
            requisicao.Headers.Add(TokenDeOrigem.Cabecalho, origem);
        if (job)
            requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
        return await http.SendAsync(requisicao);
    }
}
