using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Acordu.Api.FunctionalTests.Apoio;

/// <summary>Contato com acesso ao portal: cadastrado, convidado pela equipe e com o convite aceito (sessão própria).</summary>
internal static class ContatosDoPortal
{
    public const string Senha = "senha-do-contato-123";

    public static async Task<ContatoComAcesso> CriarAsync(
        ApiFixture api, Equipe equipe, Guid cliente, string escopo = "TODAS_DO_CLIENTE", string nome = "Marina Souza")
    {
        var email = $"contato-{Guid.NewGuid():N}@bomgrao.com.br";
        var contato = Cenarios.Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos", new { nome, email }));
        await equipe.EsperarAsync(
            HttpStatusCode.Accepted, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos/{contato}/acesso-portal",
            new { escopoPortal = escopo });

        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(
            new Uri("/api/v1/auth/portal/convite", UriKind.Relative), new { token = api.Emails.TokenDoConvite(email), senha = Senha });
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, "corpo: {0}", texto);
        return new ContatoComAcesso(contato, email, JsonDocument.Parse(texto).RootElement.GetProperty("accessToken").GetString()!);
    }
}

internal sealed record ContatoComAcesso(Guid Id, string Email, string Token);
