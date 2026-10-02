using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Acordu.Api.FunctionalTests;

public sealed class HealthTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_ApiNoAr_Responde200()
    {
        using var cliente = fabrica.CreateClient();

        using var resposta = await cliente.GetAsync(new Uri("/api/health", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
