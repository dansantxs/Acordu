using System.Net;
using Acordu.Api.Seguranca;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Segurança §9: a renovação da sessão tem limite próprio, por refresh token, fora do limite por IP do login (decidido
/// em 15/10, depois da fumaça do F1c3 derrubar a sessão em uso normal).
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class LimiteRenovacaoTests(ApiFixture api)
{
    [Theory]
    [InlineData("/api/v1/auth/refresh", ComposicaoSeguranca.LimiteRenovacao)]
    [InlineData("/api/v1/auth/login", ComposicaoSeguranca.LimiteAuth)]
    [InlineData("/api/v1/auth/registro", ComposicaoSeguranca.LimiteAuth)]
    [InlineData("/api/v1/auth/senha/esqueci", ComposicaoSeguranca.LimiteAuth)]
    public void Rota_UsaOLimiteCerto(string rota, string politica)
    {
        var endpoints = api.App.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        var endpoint = endpoints.Single(e => "/" + e.RoutePattern.RawText?.TrimStart('/') == rota);

        endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName.Should().Be(politica);
    }

    [Fact]
    public void ChaveDaRenovacao_PeloCookie_SemGuardarOToken()
    {
        var primeira = ComCookie("token-a");
        var mesma = ComCookie("token-a");
        var outra = ComCookie("token-b");

        var chave = ComposicaoSeguranca.ChaveDaRenovacao(primeira);

        chave.Should().StartWith("refresh:").And.NotContain("token-a");
        ComposicaoSeguranca.ChaveDaRenovacao(mesma).Should().Be(chave, "o mesmo cookie cai na mesma partição, em qualquer IP");
        ComposicaoSeguranca.ChaveDaRenovacao(outra).Should().NotBe(chave, "outra sessão no mesmo IP tem limite próprio");
    }

    [Fact]
    public void ChaveDaRenovacao_SemCookie_UsaOIp()
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        ComposicaoSeguranca.ChaveDaRenovacao(http).Should().Be("ip:203.0.113.7");
    }

    private static DefaultHttpContext ComCookie(string token)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = $"acordu_refresh={token}";
        http.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.1");
        return http;
    }
}
