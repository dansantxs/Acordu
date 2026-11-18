using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// RF-AUT-06 (spec do F9, F9a): empresa cujo criador não confirmou o e-mail não convida, não emite link e não manda
/// e-mail a terceiros; a confirmação (ou a redefinição de senha) libera, e a liberação é por empresa.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class EnvioATerceirosTests(ApiFixture api)
{
    private const string TipoBloqueio = "https://acordu.app.br/erros/email-nao-confirmado";

    [Fact]
    public async Task Eu_EmpresaRecemCadastrada_NaoLiberada() // F9-09
    {
        var equipe = await Equipe.CriarAsync(api, confirmar: false);

        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu");

        eu.GetProperty("emailConfirmado").GetBoolean().Should().BeFalse();
        eu.GetProperty("tenant").GetProperty("envioLiberado").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Convites_SemPermissaoNaEmpresaNaoLiberada_Retorna403() // F9-10
    {
        var equipe = await Equipe.CriarAsync(api, confirmar: false);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatoAsync(equipe, cliente);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);
        var leitor = await equipe.ComPapelAsync(Papel.NomeLeitor);

        await equipe.EsperarAsync(
            HttpStatusCode.Forbidden, HttpMethod.Post, "/api/v1/convites",
            new { email = NovoEmail(), papelId = await PapelAsync(equipe, Papel.NomeMembro) }, token: leitor);
        await equipe.EsperarAsync(
            HttpStatusCode.Forbidden, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos/{contato.Id}/acesso-portal",
            new { escopoPortal = "PROPRIAS" }, token: leitor);
        await equipe.EsperarAsync(
            HttpStatusCode.Forbidden, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal",
            new { contatoId = contato.Id }, token: leitor);
    }

    [Fact]
    public async Task ConvidarEquipe_EmpresaNaoLiberada_Retorna422SemGravarNemEnviar() // F9-11
    {
        var equipe = await Equipe.CriarAsync(api, confirmar: false);
        var email = NovoEmail();

        var erro = await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, "/api/v1/convites",
            new { email, papelId = await PapelAsync(equipe, Papel.NomeMembro) });

        erro.GetProperty("type").GetString().Should().Be(TipoBloqueio);
        erro.GetProperty("detail").GetString().Should().Contain("Confirme o e-mail");
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/convites")).GetArrayLength().Should().Be(0);
        api.Emails.Para(email).Should().BeEmpty();
    }

    [Fact]
    public async Task ConvidarPortal_EmpresaNaoLiberada_Retorna422SemAcessoNemEmail() // F9-12
    {
        var equipe = await Equipe.CriarAsync(api, confirmar: false);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatoAsync(equipe, cliente);

        var erro = await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post,
            $"/api/v1/clientes/{cliente}/contatos/{contato.Id}/acesso-portal", new { escopoPortal = "PROPRIAS" });

        erro.GetProperty("type").GetString().Should().Be(TipoBloqueio);
        api.Emails.Para(contato.Email).Should().NotContain(m => m.Categoria == "convite-portal");
        var depois = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/clientes/{cliente}");
        var noCliente = depois.GetProperty("contatos").EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == contato.Id);
        noCliente.GetProperty("acessoPortal").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EmitirLink_EmpresaNaoLiberada_Retorna422ComOuSemEmail(bool enviarEmail) // F9-13
    {
        var equipe = await Equipe.CriarAsync(api, confirmar: false);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatoAsync(equipe, cliente);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);

        var erro = await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal",
            new { contatoId = contato.Id, enviarEmail });

        erro.GetProperty("type").GetString().Should().Be(TipoBloqueio);
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal"))
            .GetArrayLength().Should().Be(0);
        api.Emails.Para(contato.Email).Should().NotContain(m => m.Categoria == "link-portal");
    }

    [Fact]
    public async Task AlterarEscopo_EmpresaNaoLiberada_SegueFuncionando() // F9-14
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        await DesliberarAsync(equipe);

        var alterado = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/clientes/{cliente}/contatos/{contato.Id}/acesso-portal",
            new { escopoPortal = "PROPRIAS" });

        alterado.GetProperty("acessoPortal").GetProperty("escopoPortal").GetString().Should().Be("PROPRIAS");
    }

    [Fact]
    public async Task EmailsDaConta_EmpresaNaoLiberada_Saem() // F9-18
    {
        var (email, token) = await RegistrarAsync();
        var antes = api.Emails.Para(email).Count;

        using (var reenvio = await PostAsync("/api/v1/auth/confirmacao-email/reenvio", null, token))
            reenvio.StatusCode.Should().Be(HttpStatusCode.Accepted);
        using (var esqueci = await PostAsync("/api/v1/auth/senha/esqueci", new { email }))
            esqueci.StatusCode.Should().Be(HttpStatusCode.Accepted);

        api.Emails.Para(email).Count.Should().Be(antes + 2);
        api.Emails.Para(email).Should().Contain(m => m.Categoria == "confirmacao-email")
            .And.Contain(m => m.Categoria == "redefinicao-senha");
    }

    [Fact]
    public async Task ConfirmarEmail_EmpresaNaoLiberada_LiberaConvitesELinks() // F9-19
    {
        var equipe = await Equipe.CriarAsync(api, confirmar: false);
        var emailDono = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("email").GetString()!;

        using (var confirmacao = await PostAsync("/api/v1/auth/confirmacao-email", new { token = api.Emails.TokenDe(emailDono, "confirmacao-email") }))
            confirmacao.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu");
        eu.GetProperty("tenant").GetProperty("envioLiberado").GetBoolean().Should().BeTrue();
        var convidado = NovoEmail();
        await equipe.EsperarAsync(
            HttpStatusCode.Accepted, HttpMethod.Post, "/api/v1/convites",
            new { email = convidado, papelId = await PapelAsync(equipe, Papel.NomeMembro) });
        api.Emails.TokenDoConviteEquipe(convidado).Should().NotBeNullOrEmpty();
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatoAsync(equipe, cliente);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);
        await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal", new { contatoId = contato.Id });
        await equipe.EsperarAsync(
            HttpStatusCode.Accepted, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos/{contato.Id}/acesso-portal",
            new { escopoPortal = "PROPRIAS" });
        api.Emails.TokenDoConvite(contato.Email).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RedefinirSenha_EmpresaNaoLiberada_Libera() // F9-20
    {
        var (email, _) = await RegistrarAsync();
        using (var esqueci = await PostAsync("/api/v1/auth/senha/esqueci", new { email }))
            esqueci.StatusCode.Should().Be(HttpStatusCode.Accepted);
        const string NovaSenha = "outra-senha-bem-comprida";

        using (var redefinir = await PostAsync(
            "/api/v1/auth/senha/redefinir", new { token = api.Emails.TokenDe(email, "redefinicao-senha"), novaSenha = NovaSenha }))
        {
            redefinir.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        using var login = await PostAsync("/api/v1/auth/login", new { email, senha = NovaSenha });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        using var eu = await GetAsync("/api/v1/auth/eu", token);
        var perfil = await eu.Content.ReadFromJsonAsync<JsonElement>();
        perfil.GetProperty("emailConfirmado").GetBoolean().Should().BeTrue();
        perfil.GetProperty("tenant").GetProperty("envioLiberado").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task TrocarPapel_CriadorRebaixado_EmpresaContinuaLiberada() // F9-22
    {
        var equipe = await Equipe.CriarAsync(api);
        var outroDono = await equipe.ComPapelAsync(Papel.NomeDono);
        var criador = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("usuarioId").GetGuid();

        await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/usuarios/{criador}/papel",
            new { papelId = await PapelAsync(equipe, Papel.NomeLeitor) }, token: outroDono);

        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: outroDono))
            .GetProperty("tenant").GetProperty("envioLiberado").GetBoolean().Should().BeTrue();
        await equipe.EsperarAsync(
            HttpStatusCode.Accepted, HttpMethod.Post, "/api/v1/convites",
            new { email = NovoEmail(), papelId = await PapelAsync(equipe, Papel.NomeMembro) }, token: outroDono);
    }

    [Fact]
    public async Task AceitarConvite_ContaNova_NasceConfirmada() // F9-23
    {
        var equipe = await Equipe.CriarAsync(api);
        var email = NovoEmail();
        await equipe.EsperarAsync(
            HttpStatusCode.Accepted, HttpMethod.Post, "/api/v1/convites",
            new { email, papelId = await PapelAsync(equipe, Papel.NomeMembro) });

        using var aceite = await PostAsync(
            "/api/v1/auth/convite", new { token = api.Emails.TokenDoConviteEquipe(email), nome = "Bruno", senha = Equipe.Senha, versaoTermos = Apoio.TextosAceitos.Termos, versaoPrivacidade = Apoio.TextosAceitos.Privacidade });
        aceite.StatusCode.Should().Be(HttpStatusCode.Created);
        var token = (await aceite.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: token))
            .GetProperty("emailConfirmado").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Envios_EmpresaLiberadaAoLadoDeBloqueada_SaemSoNaLiberada() // F9-24
    {
        var bloqueada = await Equipe.CriarAsync(api, confirmar: false);
        var liberada = await Equipe.CriarAsync(api);

        await bloqueada.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, "/api/v1/convites",
            new { email = NovoEmail(), papelId = await PapelAsync(bloqueada, Papel.NomeMembro) });
        await liberada.EsperarAsync(
            HttpStatusCode.Accepted, HttpMethod.Post, "/api/v1/convites",
            new { email = NovoEmail(), papelId = await PapelAsync(liberada, Papel.NomeMembro) });
        // Convite do portal e link: recusados na bloqueada, aceitos na liberada.
        foreach (var (equipe, convite, link) in new[]
        {
            (bloqueada, HttpStatusCode.UnprocessableEntity, HttpStatusCode.UnprocessableEntity),
            (liberada, HttpStatusCode.Accepted, HttpStatusCode.Created),
        })
        {
            var cliente = await Cenarios.ClienteAsync(equipe);
            var contato = await ContatoAsync(equipe, cliente);
            var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);
            await equipe.EsperarAsync(
                convite, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos/{contato.Id}/acesso-portal",
                new { escopoPortal = "PROPRIAS" });
            await equipe.EsperarAsync(
                link, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal", new { contatoId = contato.Id });
        }
    }

    [Fact]
    public async Task Convidar_UsuarioNaoConfirmadoEmEmpresaLiberada_Envia() // F9-25
    {
        var liberada = await Equipe.CriarAsync(api);
        // A pessoa registrou a própria empresa (sem confirmar) e tem vínculo de Dono na liberada.
        var pessoa = await liberada.ComPapelAsync(Papel.NomeDono);

        var eu = await liberada.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: pessoa);
        eu.GetProperty("emailConfirmado").GetBoolean().Should().BeFalse();
        eu.GetProperty("tenant").GetProperty("envioLiberado").GetBoolean().Should().BeTrue();
        await liberada.EsperarAsync(
            HttpStatusCode.Accepted, HttpMethod.Post, "/api/v1/convites",
            new { email = NovoEmail(), papelId = await PapelAsync(liberada, Papel.NomeMembro) }, token: pessoa);
    }

    private static string NovoEmail() => $"terceiro-{Guid.NewGuid():N}@exemplo.com";

    private async Task<Guid> PapelAsync(Equipe equipe, string nome)
    {
        await using var db = api.DaEquipe(equipe.TenantId);
        return (await db.Papeis.ToListAsync()).Single(p => p.Nome == nome).Id;
    }

    /// <summary>Volta a empresa ao estado "não liberada", para testar o que não depende do caminho até ele.</summary>
    private async Task DesliberarAsync(Equipe equipe)
    {
        await using var db = api.DaEquipe(equipe.TenantId);
        await db.Tenants.ExecuteUpdateAsync(t => t.SetProperty(x => x.EnvioLiberadoEm, (DateTimeOffset?)null));
    }

    private static async Task<(Guid Id, string Email)> ContatoAsync(Equipe equipe, Guid cliente)
    {
        var email = $"contato-{Guid.NewGuid():N}@bomgrao.com.br";
        var contato = Cenarios.Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos", new { nome = "Carlos Prado", email }));
        return (contato, email);
    }

    private async Task<(string Email, string Token)> RegistrarAsync()
    {
        var email = $"criador-{Guid.NewGuid():N}@exemplo.com";
        using var resposta = await PostAsync(
            "/api/v1/auth/registro", new { nome = "Ana Souza", email, senha = Equipe.Senha, nomeEmpresa = "Estúdio Traço", documento = Apoio.DocumentosDeTeste.Novo(), versaoTermos = Apoio.TextosAceitos.Termos, versaoPrivacidade = Apoio.TextosAceitos.Privacidade });
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        return (email, (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!);
    }

    private async Task<HttpResponseMessage> PostAsync(string rota, object? corpo, string? token = null)
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri(rota, UriKind.Relative));
        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);
        if (token is not null)
            requisicao.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(requisicao);
    }

    private async Task<HttpResponseMessage> GetAsync(string rota, string token)
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        requisicao.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(requisicao);
    }
}
