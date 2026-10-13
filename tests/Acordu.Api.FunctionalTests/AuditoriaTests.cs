using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;

namespace Acordu.Api.FunctionalTests;

/// <summary>RNF-AUD-01 e segurança §8: login na trilha, leitura por <c>auditoria.ver</c> e isolamento.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class AuditoriaTests(ApiFixture api)
{
    private async Task<HttpStatusCode> LoginAsync(string email, string senha)
    {
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/login", UriKind.Relative), new { email, senha });
        return resposta.StatusCode;
    }

    private static JsonElement[] Itens(JsonElement pagina) => [.. pagina.GetProperty("itens").EnumerateArray()];

    [Fact]
    public async Task Login_SucessoNoTenantDaSessao_FalhaFicaFora()
    {
        var equipe = await Equipe.CriarAsync(api);
        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu");
        var email = eu.GetProperty("email").GetString()!;

        (await LoginAsync(email, "senha-errada-de-proposito")).Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAsync(email, Equipe.Senha)).Should().Be(HttpStatusCode.OK);

        var trilha = Itens(await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auditoria"));
        var login = trilha.Should().ContainSingle(r => r.GetProperty("acao").GetString() == "login.sucesso").Subject;
        login.GetProperty("usuario").GetProperty("id").GetGuid().Should().Be(eu.GetProperty("usuarioId").GetGuid());
        login.GetProperty("usuario").GetProperty("nome").GetString().Should().Be("Pessoa da equipe");
        trilha.Should().NotContain(r => r.GetProperty("acao").GetString() == "login.falha");
    }

    [Fact]
    public async Task Filtros_PorAcaoUsuarioEPeriodo()
    {
        var equipe = await Equipe.CriarAsync(api);
        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu");
        var usuario = eu.GetProperty("usuarioId").GetGuid();
        (await LoginAsync(eu.GetProperty("email").GetString()!, Equipe.Senha)).Should().Be(HttpStatusCode.OK);

        var porAcao = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auditoria?acao=login.sucesso");
        var porUsuario = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/auditoria?usuarioId={Guid.NewGuid()}");
        var futuro = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auditoria?de=2099-01-01");
        var ate = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/auditoria?usuarioId={usuario}&ate=2099-12-31");

        porAcao.GetProperty("total").GetInt32().Should().Be(1);
        porUsuario.GetProperty("total").GetInt32().Should().Be(0);
        futuro.GetProperty("total").GetInt32().Should().Be(0);
        ate.GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task SoQuemTemAuditoriaVer_EOutroTenantNaoVe()
    {
        var equipe = await Equipe.CriarAsync(api);
        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu");
        (await LoginAsync(eu.GetProperty("email").GetString()!, Equipe.Senha)).Should().Be(HttpStatusCode.OK);
        var leitor = await equipe.ComPapelAsync(Papel.NomeLeitor);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var outra = await Equipe.CriarAsync(api);

        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, "/api/v1/auditoria", token: leitor);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, "/api/v1/auditoria", token: membro);
        var daOutra = await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/auditoria?usuarioId={eu.GetProperty("usuarioId").GetGuid()}");

        daOutra.GetProperty("total").GetInt32().Should().Be(0);
    }
}
