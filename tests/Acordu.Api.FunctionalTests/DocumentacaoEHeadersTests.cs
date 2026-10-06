using System.Net;

namespace Acordu.Api.FunctionalTests;

[Collection(ColecaoApi.Nome)]
public sealed class DocumentacaoEHeadersTests(ApiFixture api)
{
    private async Task<(HttpStatusCode Status, string Corpo)> GetAsync(string rota)
    {
        using var cliente = api.NovoHttp();
        using var resposta = await cliente.GetAsync(new Uri(rota, UriKind.Relative));
        return (resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DocumentoInterno_SemRotasDoPortalNemInternas()
    {
        var (status, corpo) = await GetAsync("/openapi/interno.json");

        status.Should().Be(HttpStatusCode.OK);
        corpo.Should().Contain("/api/v1/teste/eco");
        corpo.Should().NotContain("/api/v1/portal/teste");
        corpo.Should().NotContain("/internal/jobs");
        corpo.Should().NotContain("/api/health");
    }

    [Fact]
    public async Task DocumentoPortal_SoRotasDoPortal()
    {
        var (status, corpo) = await GetAsync("/openapi/portal.json");

        status.Should().Be(HttpStatusCode.OK);
        corpo.Should().Contain("/api/v1/portal/teste");
        corpo.Should().NotContain("/api/v1/teste/");
    }

    [Fact]
    public async Task Scalar_EmDevelopment_Responde()
    {
        (await GetAsync("/scalar")).Status.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.MovedPermanently, HttpStatusCode.Found);
    }

    [Fact]
    public async Task RespostaDaApi_ComHeadersDeSeguranca()
    {
        using var cliente = api.NovoHttp();

        using var resposta = await cliente.GetAsync(new Uri("/api/health", UriKind.Relative));

        resposta.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        resposta.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
        resposta.Headers.GetValues("Referrer-Policy").Should().Equal("same-origin");
        resposta.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("default-src 'none'");
    }
}
