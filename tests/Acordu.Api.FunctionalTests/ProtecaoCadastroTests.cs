using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Acordu.Api.Composicao;
using Acordu.Api.Seguranca;
using Acordu.Application.Autenticacao;
using Acordu.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// RNF-SEG-10 (spec do F9, F9c): Turnstile no cadastro, conferido no servidor (siteverify falso, por handler), e limite
/// próprio de cadastros por IP, encadeado com o de <c>/auth</c>. Cada cenário numa Api montada com a configuração dele.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class ProtecaoCadastroTests(ApiFixture api)
{
    private const string CabecalhoIp = "X-Teste-Ip";

    [Fact]
    public async Task Registro_TokenAprovado_CriaEEnviaSegredoEIp() // F9-01
    {
        using var siteverify = new SiteverifyFalso(Aprovado());
        await using var app = await ApiComTurnstileAsync(siteverify);

        using var resposta = await RegistrarAsync(app, "token-bom", ip: "203.0.113.10");

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var enviado = siteverify.Corpos.Should().ContainSingle().Subject;
        enviado.Should().Contain("secret=segredo-dos-testes").And.Contain("response=token-bom").And.Contain("remoteip=203.0.113.10");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Registro_SemToken_400SemChamarOCloudflare(string? token) // F9-02
    {
        using var siteverify = new SiteverifyFalso(Aprovado());
        await using var app = await ApiComTurnstileAsync(siteverify);

        using var resposta = await RegistrarAsync(app, token);

        await ConferirRecusaAsync(resposta, "robô");
        siteverify.Corpos.Should().BeEmpty();
    }

    [Fact]
    public async Task Registro_TokenRecusado_400ENadaCriado() // F9-02
    {
        using var siteverify = new SiteverifyFalso("""{"success":false,"error-codes":["invalid-input-response"]}""");
        await using var app = await ApiComTurnstileAsync(siteverify);
        var email = NovoEmail();

        using var resposta = await RegistrarAsync(app, "token-ruim", email: email);

        await ConferirRecusaAsync(resposta, "robô");
        api.Emails.Para(email).Should().BeEmpty("nada foi criado nem enviado");
        using var http = app.GetTestClient();
        using var login = await http.PostAsJsonAsync(new Uri("/api/v1/auth/login", UriKind.Relative), new { email, senha = "senha-bem-comprida" });
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Registro_CloudflareForaDoAr_RecusaPedindoNovaTentativa() // F9-03
    {
        using var siteverify = new SiteverifyFalso(_ => throw new HttpRequestException("fora do ar"));
        await using var app = await ApiComTurnstileAsync(siteverify);

        using var resposta = await RegistrarAsync(app, "token-bom");

        await ConferirRecusaAsync(resposta, "Tente de novo");
    }

    [Fact]
    public async Task Registro_CloudflareEstouraOTempo_RecusaPedindoNovaTentativa() // F9-03
    {
        // O que o HttpClient lança quando passam os 5 s.
        using var siteverify = new SiteverifyFalso(_ => throw new TaskCanceledException("tempo esgotado", new TimeoutException()));
        await using var app = await ApiComTurnstileAsync(siteverify);

        using var resposta = await RegistrarAsync(app, "token-bom");

        await ConferirRecusaAsync(resposta, "Tente de novo");
    }

    [Fact]
    public async Task Registro_SegredoDoServidorRecusado_TrataComoIndisponivel() // F9-03
    {
        using var siteverify = new SiteverifyFalso("""{"success":false,"error-codes":["invalid-input-secret"]}""");
        await using var app = await ApiComTurnstileAsync(siteverify);

        using var resposta = await RegistrarAsync(app, "token-bom");

        await ConferirRecusaAsync(resposta, "Tente de novo");
    }

    [Fact]
    public async Task Registro_CloudflareRespondeErro_Recusa() // F9-03
    {
        using var siteverify = new SiteverifyFalso(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        await using var app = await ApiComTurnstileAsync(siteverify);

        using var resposta = await RegistrarAsync(app, "token-bom");

        await ConferirRecusaAsync(resposta, "Tente de novo");
    }

    [Theory]
    [InlineData("""{"success":true,"hostname":"outro-site.com","action":"registro"}""")]
    [InlineData("""{"success":true,"hostname":"acordu.app.br","action":"login"}""")]
    [InlineData("""{"success":true,"hostname":"acordu.app.br"}""")]
    public async Task Registro_TokenDeOutroSiteOuOutraAcao_Recusa(string resposta) // F9-04
    {
        using var siteverify = new SiteverifyFalso(resposta);
        await using var app = await ApiComTurnstileAsync(siteverify);

        using var registro = await RegistrarAsync(app, "token-de-fora");

        await ConferirRecusaAsync(registro, "robô");
    }

    [Fact]
    public async Task Registro_TurnstileDesligado_CriaSemToken() // F9-05
    {
        using var http = api.NovoHttp();

        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), Corpo(NovoEmail(), null));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        using var escopo = api.App.Services.CreateScope();
        escopo.ServiceProvider.GetRequiredService<IVerificacaoHumana>().Should().BeOfType<VerificacaoHumanaDesligada>(
            "sem segredo, ninguém chama o Cloudflare");
    }

    [Fact]
    public async Task Registro_QuartoDoMesmoIpNaHora_429_OutroIpSegue() // F9-06
    {
        await using var app = await ApiComLimiteAsync();

        for (var i = 0; i < 3; i++)
            (await StatusAsync(app, "198.51.100.1")).Should().Be(HttpStatusCode.Created);

        (await StatusAsync(app, "198.51.100.1")).Should().Be(HttpStatusCode.TooManyRequests);
        (await StatusAsync(app, "198.51.100.2")).Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task OutrasRotasDeAuth_LimiteDoCadastroEsgotado_Seguem() // F9-07
    {
        await using var app = await ApiComLimiteAsync();
        const string Ip = "198.51.100.3";
        for (var i = 0; i < 4; i++)
            await StatusAsync(app, Ip);

        using var http = app.GetTestClient();
        (await EnviarAsync(http, Ip, "/api/v1/auth/login", new { email = NovoEmail(), senha = "senha-errada-qualquer" }))
            .Should().Be(HttpStatusCode.Unauthorized, "o limite do cadastro não vale para o login");
        (await EnviarAsync(http, Ip, "/api/v1/auth/senha/esqueci", new { email = NovoEmail() }))
            .Should().Be(HttpStatusCode.Accepted);
        (await EnviarAsync(http, Ip, "/api/v1/auth/refresh", new { }))
            .Should().Be(HttpStatusCode.Unauthorized, "a renovação tem limite próprio");
    }

    [Fact]
    public async Task Registro_TentativasRecusadas_ContamNoLimite() // F9-08
    {
        await using var app = await ApiComLimiteAsync();
        const string Ip = "198.51.100.4";
        using var http = app.GetTestClient();

        for (var i = 0; i < 3; i++)
        {
            (await EnviarAsync(http, Ip, "/api/v1/auth/registro", new { nome = "Ana", email = NovoEmail(), senha = "curta", nomeEmpresa = "X" }))
                .Should().Be(HttpStatusCode.BadRequest);
        }

        (await StatusAsync(app, Ip)).Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Registro_RecusasDoTurnstile_ContamNoLimite() // F9-08
    {
        using var siteverify = new SiteverifyFalso(Aprovado());
        await using var app = await ApiComTurnstileAsync(siteverify, limitePorHora: "3");
        const string Ip = "198.51.100.5";

        for (var i = 0; i < 3; i++)
        {
            using var recusada = await RegistrarAsync(app, token: null, ip: Ip);
            recusada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        using var quarta = await RegistrarAsync(app, "token-bom", ip: Ip);
        quarta.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [InlineData("203.0.113.9", "203.0.113.9")]
    [InlineData("::ffff:203.0.113.9", "203.0.113.9")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:2::/64")]
    public void ChaveDoIp_Ipv4OuIpv6_AgrupaIpv6Por64(string ip, string chave) =>
        ComposicaoSeguranca.ChaveDoIp(IPAddress.Parse(ip)).Should().Be(chave);

    private Task<WebApplication> ApiComTurnstileAsync(SiteverifyFalso siteverify, string? limitePorHora = null) => api.NovaApiAsync(
        new Dictionary<string, string?>
        {
            ["Turnstile:Segredo"] = "segredo-dos-testes",
            ["App:UrlPublica"] = "https://acordu.app.br/",
            ["RateLimit:Registro:PorHora"] = limitePorHora ?? "100000",
        },
        antes: IpDoCabecalho,
        servicos: s => s.AddHttpClient(ComposicaoApi.NomeClienteTurnstile).ConfigurePrimaryHttpMessageHandler(() => siteverify));

    private Task<WebApplication> ApiComLimiteAsync() => api.NovaApiAsync(
        new Dictionary<string, string?> { ["RateLimit:Registro:PorHora"] = "3" }, antes: IpDoCabecalho);

    /// <summary>O IP da conexão vem de um cabeçalho de teste (o TestServer não tem conexão de verdade).</summary>
    private static void IpDoCabecalho(WebApplication app) => app.Use((http, proximo) =>
    {
        if (http.Request.Headers.TryGetValue(CabecalhoIp, out var ip))
            http.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
        return proximo(http);
    });

    private static string NovoEmail() => $"cadastro-{Guid.NewGuid():N}@exemplo.com";

    private static object Corpo(string email, string? token) =>
        new { nome = "Ana Souza", email, senha = "senha-bem-comprida", nomeEmpresa = "Estúdio Traço", tokenTurnstile = token };

    private static async Task<HttpResponseMessage> RegistrarAsync(WebApplication app, string? token, string? email = null, string? ip = null)
    {
        using var http = app.GetTestClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/auth/registro", UriKind.Relative))
        {
            Content = JsonContent.Create(Corpo(email ?? NovoEmail(), token)),
        };
        if (ip is not null)
            requisicao.Headers.Add(CabecalhoIp, ip);
        return await http.SendAsync(requisicao);
    }

    private static async Task<HttpStatusCode> StatusAsync(WebApplication app, string ip)
    {
        using var http = app.GetTestClient();
        return await EnviarAsync(http, ip, "/api/v1/auth/registro", Corpo(NovoEmail(), null));
    }

    private static async Task<HttpStatusCode> EnviarAsync(HttpClient http, string ip, string rota, object corpo)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri(rota, UriKind.Relative)) { Content = JsonContent.Create(corpo) };
        requisicao.Headers.Add(CabecalhoIp, ip);
        using var resposta = await http.SendAsync(requisicao);
        return resposta.StatusCode;
    }

    private static async Task ConferirRecusaAsync(HttpResponseMessage resposta, string trecho)
    {
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest, texto);
        var erros = JsonDocument.Parse(texto).RootElement.GetProperty("errors").GetProperty("tokenTurnstile");
        erros[0].GetString().Should().Contain(trecho);
    }

    private static string Aprovado() => """{"success":true,"hostname":"acordu.app.br","action":"registro"}""";

    /// <summary>O siteverify do Cloudflare, falso: guarda os corpos recebidos e responde o que o teste mandar.</summary>
    private sealed class SiteverifyFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public SiteverifyFalso(string json)
            : this(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") })
        {
        }

        public List<string> Corpos { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri.Should().Be(new Uri("https://challenges.cloudflare.com/turnstile/v0/siteverify"));
            Corpos.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return responder(request);
        }
    }
}
