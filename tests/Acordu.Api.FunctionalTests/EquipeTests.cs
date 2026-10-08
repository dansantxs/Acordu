using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §3 e segurança §3.3–4: convites, aceite, troca de papel e remoção, com a guarda de ≥ 1 Dono.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class EquipeTests(ApiFixture api)
{
    private const string Senha = "senha-da-equipe-123";

    private static string NovoEmail() => $"pessoa-{Guid.NewGuid():N}@estudio.com";

    [Fact]
    public async Task Convidar_EAceitarComContaNova()
    {
        var equipe = await Equipe.CriarAsync(api);
        var email = NovoEmail();
        var membro = await PapelAsync(equipe, Papel.NomeMembro);

        var convite = await equipe.EsperarAsync(HttpStatusCode.Accepted, HttpMethod.Post, "/api/v1/convites", new { email, papelId = membro });

        convite.GetProperty("papel").GetProperty("nome").GetString().Should().Be(Papel.NomeMembro);
        var pendentes = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/convites");
        pendentes.GetArrayLength().Should().Be(1);
        pendentes[0].GetProperty("expiraEm").GetDateTimeOffset().Should().Be(api.Relogio.Agora.AddDays(7));
        var token = api.Emails.TokenDoConviteEquipe(email);
        using (var consulta = await PostAsync("/api/v1/auth/convite/consulta", new { token }))
        {
            consulta.StatusCode.Should().Be(HttpStatusCode.OK);
            var info = await Equipe.JsonAsync(consulta);
            info.GetProperty("nomeTenant").GetString().Should().Be("Estúdio Traço");
            info.GetProperty("papel").GetString().Should().Be(Papel.NomeMembro);
            info.GetProperty("temConta").GetBoolean().Should().BeFalse();
        }

        using (var semNome = await PostAsync("/api/v1/auth/convite", new { token, senha = Senha }))
        {
            semNome.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Equipe.JsonAsync(semNome)).GetProperty("errors").TryGetProperty("nome", out _).Should().BeTrue();
        }

        var sessao = await AceitarAsync(token, nome: "Bruno Lima");

        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: sessao.Token);
        eu.GetProperty("papel").GetString().Should().Be(Papel.NomeMembro);
        eu.GetProperty("tenant").GetProperty("id").GetGuid().Should().Be(equipe.TenantId);
        var usuarios = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/usuarios");
        usuarios.GetArrayLength().Should().Be(2);
        var bruno = usuarios.EnumerateArray().Single(u => u.GetProperty("nome").GetString() == "Bruno Lima");
        bruno.GetProperty("email").GetString().Should().Be(email);
        bruno.GetProperty("dono").GetBoolean().Should().BeFalse();
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/convites")).GetArrayLength().Should().Be(0);
        using var deNovo = await PostAsync("/api/v1/auth/convite", new { token, nome = "Bruno", senha = Senha });
        deNovo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Aceitar_ContaExistente_ExigeASenhaDela()
    {
        var email = NovoEmail();
        using (var registro = await PostAsync("/api/v1/auth/registro", new
        {
            nome = "Bruno", email, senha = Equipe.Senha, nomeEmpresa = "Empresa do Bruno",
        }))
        {
            registro.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var equipe = await Equipe.CriarAsync(api);
        await Convidar(equipe, email, await PapelAsync(equipe, Papel.NomeLeitor));
        var token = api.Emails.TokenDoConviteEquipe(email);

        using (var errada = await PostAsync("/api/v1/auth/convite", new { token, senha = "outra-senha-qualquer" }))
            errada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var sessao = await AceitarAsync(token, Equipe.Senha);

        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: sessao.Token);
        eu.GetProperty("papel").GetString().Should().Be(Papel.NomeLeitor);
        eu.GetProperty("tenants").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Convidar_Recusas_EReconvidarInvalidaOAnterior()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await PapelAsync(equipe, Papel.NomeMembro);
        var cliente = await PapelAsync(equipe, Papel.NomeCliente);
        var emailDono = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("email").GetString();

        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, "/api/v1/convites", new { email = NovoEmail(), papelId = cliente });
        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, "/api/v1/convites", new { email = emailDono, papelId = membro });
        var semPapel = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/convites", new { email = NovoEmail() });
        semPapel.GetProperty("errors").TryGetProperty("papelId", out _).Should().BeTrue();
        var tokenMembro = await equipe.ComPapelAsync(Papel.NomeMembro);
        await equipe.EsperarAsync(
            HttpStatusCode.Forbidden, HttpMethod.Post, "/api/v1/convites", new { email = NovoEmail(), papelId = membro }, tokenMembro);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, "/api/v1/usuarios", token: tokenMembro);

        var email = NovoEmail();
        await Convidar(equipe, email, membro);
        var primeiro = api.Emails.TokenDoConviteEquipe(email);
        var segundoConvite = await Convidar(equipe, email, membro);
        var segundo = api.Emails.TokenDoConviteEquipe(email);

        (await StatusConsultaAsync(primeiro)).Should().Be(HttpStatusCode.Unauthorized);
        (await StatusConsultaAsync(segundo)).Should().Be(HttpStatusCode.OK);
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/convites/{Cenarios.Id(segundoConvite)}");
        (await StatusConsultaAsync(segundo)).Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TrocarPapel_ComAGuardaDoUltimoDono()
    {
        var equipe = await Equipe.CriarAsync(api);
        var dono = await PapelAsync(equipe, Papel.NomeDono);
        var membro = await PapelAsync(equipe, Papel.NomeMembro);
        var cliente = await PapelAsync(equipe, Papel.NomeCliente);
        var eu = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("usuarioId").GetGuid();
        var email = NovoEmail();
        await Convidar(equipe, email, membro);
        var bruno = await AceitarAsync(api.Emails.TokenDoConviteEquipe(email), nome: "Bruno Lima");
        var idBruno = await UsuarioIdAsync(equipe, bruno.Token);

        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Patch, $"/api/v1/usuarios/{eu}/papel", new { papelId = membro });
        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Patch, $"/api/v1/usuarios/{idBruno}/papel", new { papelId = cliente });
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Patch, $"/api/v1/usuarios/{eu}/papel", new { papelId = membro }, bruno.Token);
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Patch, $"/api/v1/usuarios/{Guid.NewGuid()}/papel", new { papelId = membro });

        var promovido = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/usuarios/{idBruno}/papel", new { papelId = dono });
        promovido.GetProperty("dono").GetBoolean().Should().BeTrue();
        var rebaixado = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/usuarios/{eu}/papel", new { papelId = membro });
        rebaixado.GetProperty("papel").GetProperty("nome").GetString().Should().Be(Papel.NomeMembro);

        // O novo papel vale no próximo refresh de quem mudou.
        using var refresh = await PostAsync("/api/v1/auth/refresh", null, refresh: bruno.Refresh);
        var tokenBruno = (await Equipe.JsonAsync(refresh)).GetProperty("accessToken").GetString()!;
        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Patch, $"/api/v1/usuarios/{idBruno}/papel", new { papelId = membro }, tokenBruno);
    }

    [Fact]
    public async Task Remover_DerrubaASessao_EUltimoDonoFica()
    {
        var equipe = await Equipe.CriarAsync(api);
        var eu = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("usuarioId").GetGuid();
        var email = NovoEmail();
        await Convidar(equipe, email, await PapelAsync(equipe, Papel.NomeMembro));
        var bruno = await AceitarAsync(api.Emails.TokenDoConviteEquipe(email), nome: "Bruno Lima");
        var idBruno = await UsuarioIdAsync(equipe, bruno.Token);

        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Delete, $"/api/v1/usuarios/{eu}", token: bruno.Token);
        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Delete, $"/api/v1/usuarios/{eu}");
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/usuarios/{idBruno}");

        using (var refresh = await PostAsync("/api/v1/auth/refresh", null, refresh: bruno.Refresh))
            refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var usuarios = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/usuarios");
        usuarios.EnumerateArray().Select(u => u.GetProperty("usuarioId").GetGuid()).Should().Equal(eu);
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Delete, $"/api/v1/usuarios/{idBruno}");
    }

    private async Task<Guid> PapelAsync(Equipe equipe, string nome)
    {
        await using var db = api.DaEquipe(equipe.TenantId);
        return (await db.Papeis.ToListAsync()).Single(p => p.Nome == nome).Id;
    }

    private static Task<JsonElement> Convidar(Equipe equipe, string email, Guid papel) =>
        equipe.EsperarAsync(HttpStatusCode.Accepted, HttpMethod.Post, "/api/v1/convites", new { email, papelId = papel });

    private static async Task<Guid> UsuarioIdAsync(Equipe equipe, string token) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: token)).GetProperty("usuarioId").GetGuid();

    private async Task<HttpStatusCode> StatusConsultaAsync(string token)
    {
        using var resposta = await PostAsync("/api/v1/auth/convite/consulta", new { token });
        return resposta.StatusCode;
    }

    private async Task<Sessao> AceitarAsync(string token, string senha = Senha, string? nome = null)
    {
        using var resposta = await PostAsync("/api/v1/auth/convite", new { token, nome, senha });
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, "corpo: {0}", texto);
        var cookie = resposta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("acordu_refresh=", StringComparison.Ordinal));
        return new Sessao(
            JsonDocument.Parse(texto).RootElement.GetProperty("accessToken").GetString()!,
            cookie.Split(';')[0]["acordu_refresh=".Length..]);
    }

    private async Task<HttpResponseMessage> PostAsync(string rota, object? corpo, string? refresh = null)
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri(rota, UriKind.Relative));
        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);
        if (refresh is not null)
            requisicao.Headers.Add("Cookie", $"acordu_refresh={refresh}");
        return await http.SendAsync(requisicao);
    }

    private sealed record Sessao(string Token, string Refresh);
}
