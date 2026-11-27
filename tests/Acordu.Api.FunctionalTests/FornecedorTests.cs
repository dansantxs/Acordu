using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// ADR 0017: quem fornece o Acordu aparece nos textos da API pelo nome e pelo artigo configurados
/// (<c>Fornecedor__Nome</c>, <c>Fornecedor__Artigo</c>), nunca fixo; artigo inválido impede a subida.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class FornecedorTests(ApiFixture api)
{
    [Fact]
    public async Task ConsultaDoConvite_TokenQueNaoVale_CitaOFornecedorConfigurado()
    {
        await using var outra = await api.NovaApiAsync(new Dictionary<string, string?>
        {
            ["Fornecedor:Nome"] = "Grupo Exemplo",
            ["Fornecedor:Artigo"] = "o",
        });
        using var http = outra.GetTestClient();

        using var resposta = await http.PostAsJsonAsync(
            new Uri("/api/v1/auth/registro/convite/consulta", UriKind.Relative), new { token = "nao-vale" });

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("detail").GetString().Should().EndWith("Peça um novo convite ao Grupo Exemplo.");
    }

    [Fact]
    public async Task ConsultaDoConvite_SemConfiguracao_CitaANebrys()
    {
        using var http = api.NovoHttp();

        using var resposta = await http.PostAsJsonAsync(
            new Uri("/api/v1/auth/registro/convite/consulta", UriKind.Relative), new { token = "nao-vale" });

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("detail").GetString().Should().EndWith("Peça um novo convite à Nebrys.");
    }

    [Fact]
    public async Task Composicao_ArtigoInvalido_NaoSobe()
    {
        var subir = async () =>
        {
            await using var outra = await api.NovaApiAsync(new Dictionary<string, string?> { ["Fornecedor:Artigo"] = "os" });
        };

        await subir.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Fornecedor__Artigo*");
    }
}
