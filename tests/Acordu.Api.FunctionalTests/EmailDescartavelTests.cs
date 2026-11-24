using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Application.Cadastro;
using Acordu.Domain.Entities;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14-60 (F14p; RF-AUT-10; design D9): e-mail de domínio descartável, pela lista versionada, é recusado no
/// cadastro e na lista de espera com <c>400</c> no campo do e-mail, e nada é gravado.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class EmailDescartavelTests(ApiFixture api)
{
    private const string Senha = "senha-bem-comprida";

    public static TheoryData<string> Descartaveis => new()
    {
        "mailinator.com",
        "YOPMAIL.com",
        "caixa.guerrillamail.com", // subdomínio de um da lista
    };

    [Theory] // F14-60
    [MemberData(nameof(Descartaveis))]
    public async Task Registro_EmailDescartavel_400NoEmailENadaCriado(string dominio)
    {
        var email = $"ana-{Guid.NewGuid():N}@{dominio}";
        var documento = DocumentosDeTeste.Novo();
        var antes = await ContagensAsync();

        var (status, corpo) = await RegistrarAsync(email, documento);

        status.Should().Be(HttpStatusCode.BadRequest, "corpo: {0}", corpo);
        corpo.GetProperty("type").GetString().Should().EndWith("/validacao");
        Erro(corpo, "email").Should().Be(MensagensDeCadastro.EmailDescartavel);
        (await ContagensAsync()).Should().Be(antes, "nada criado: conta, empresa, documento nem sessão");
        api.Emails.Para(email).Should().BeEmpty("nenhuma confirmação sai");
        var (depois, _) = await RegistrarAsync($"ana-{Guid.NewGuid():N}@exemplo.com", documento);
        depois.Should().Be(HttpStatusCode.Created, "o documento não ficou reservado pela recusa");
    }

    [Fact] // F14-60: a recusa vem com o que não consulta o banco, antes do convite (com ou sem convite, em qualquer modo)
    public async Task Registro_EmailDescartavelComConviteQualquer_400NoEmailSemConferirOConvite()
    {
        var (status, corpo) = await RegistrarAsync($"ana-{Guid.NewGuid():N}@mailinator.com", DocumentosDeTeste.Novo(), convite: "token-qualquer");

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo, "email").Should().Be(MensagensDeCadastro.EmailDescartavel);
        corpo.GetProperty("errors").TryGetProperty("convite", out _).Should().BeFalse();
    }

    [Theory] // F14-60
    [MemberData(nameof(Descartaveis))]
    public async Task ListaEspera_EmailDescartavel_400NoEmailENadaGravado(string dominio)
    {
        var email = $"espera-{Guid.NewGuid():N}@{dominio}";
        var antes = await TotalDeEntradasAsync();
        using var http = api.NovoHttp();

        using var resposta = await http.PostAsJsonAsync(
            new Uri("/api/v1/lista-espera", UriKind.Relative), new { nome = "Ana Souza", email, nomeEmpresa = "Estúdio Traço" });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("type").GetString().Should().EndWith("/validacao");
        Erro(corpo, "email").Should().Be(MensagensDeCadastro.EmailDescartavel);
        (await TotalDeEntradasAsync()).Should().Be(antes, "nada gravado");
    }

    [Fact] // F14-60: o domínio comum segue passando nos dois
    public async Task RegistroEListaEspera_EmailComum_Passam()
    {
        var (registro, corpo) = await RegistrarAsync($"ana-{Guid.NewGuid():N}@gmail.com", DocumentosDeTeste.Novo());
        using var http = api.NovoHttp();
        using var lista = await http.PostAsJsonAsync(
            new Uri("/api/v1/lista-espera", UriKind.Relative),
            new { nome = "Ana Souza", email = $"espera-{Guid.NewGuid():N}@outlook.com", nomeEmpresa = "Estúdio Traço" });

        registro.Should().Be(HttpStatusCode.Created, "corpo: {0}", corpo);
        lista.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Theory] // F14-60: com convite de verdade, em qualquer modo, a recusa vem antes de gastar o convite
    [InlineData("convite")]
    [InlineData("aberto")]
    public async Task Registro_ConviteValidoComEmailDescartavel_400NoEmailEOConviteContinuaPendente(string modo)
    {
        var email = $"convite-{Guid.NewGuid():N}@mailinator.com";
        await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);
        var operador = await OperadorNebrys.TokenAsync(api);
        using var emissao = await OperadorNebrys.EnviarAsync(
            api.NovoHttp(), HttpMethod.Post, "/api/v1/nebrys/convites", operador, JsonContent.Create(new { email }));
        emissao.StatusCode.Should().Be(HttpStatusCode.Created, "a área não confere a lista; o cadastro é que recusa");
        var token = api.Emails.TokenDe(email, "convite-empresa");
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?> { ["Cadastro:Modo"] = modo });
        var antes = await ContagensAsync();

        using var http = app.GetTestClient();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), new
        {
            nome = "Ana Souza", email, senha = Senha, nomeEmpresa = "Estúdio Traço", documento = DocumentosDeTeste.Novo(),
            versaoTermos = TextosAceitos.Termos, versaoPrivacidade = TextosAceitos.Privacidade, convite = token,
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Erro(corpo, "email").Should().Be(MensagensDeCadastro.EmailDescartavel);
        corpo.GetProperty("errors").TryGetProperty("convite", out _).Should().BeFalse();
        (await ContagensAsync()).Should().Be(antes);
        await using var db = api.DaEquipe(Guid.NewGuid());
        var convite = await db.Set<ConviteEmpresa>().SingleAsync(c => c.Email == email);
        convite.UsadoEm.Should().BeNull("o convite não foi consumido");
        convite.Pendente.Should().BeTrue();
    }

    [Fact] // F14-60: no modo convite, sem convite, o descartável recebe a recusa do e-mail (não a do convite)
    public async Task Registro_ModoConviteSemConviteComEmailDescartavel_400NoEmail()
    {
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?> { ["Cadastro:Modo"] = "convite" });
        using var http = app.GetTestClient();

        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), new
        {
            nome = "Ana Souza", email = $"ana-{Guid.NewGuid():N}@mailinator.com", senha = Senha, nomeEmpresa = "Estúdio Traço",
            documento = DocumentosDeTeste.Novo(), versaoTermos = TextosAceitos.Termos, versaoPrivacidade = TextosAceitos.Privacidade,
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Erro(corpo, "email").Should().Be(MensagensDeCadastro.EmailDescartavel);
        corpo.GetProperty("errors").TryGetProperty("convite", out _).Should().BeFalse();
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> RegistrarAsync(string email, string documento, string? convite = null)
    {
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), new
        {
            nome = "Ana Souza", email, senha = Senha, nomeEmpresa = "Estúdio Traço", documento,
            versaoTermos = TextosAceitos.Termos, versaoPrivacidade = TextosAceitos.Privacidade, convite,
        });
        return (resposta.StatusCode, await resposta.Content.ReadFromJsonAsync<JsonElement>());
    }

    private async Task<(int Usuarios, int Empresas, int Documentos, int Sessoes)> ContagensAsync()
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        return (
            await db.Users.CountAsync(),
            await db.Tenants.IgnoreQueryFilters().CountAsync(),
            await db.Set<DocumentoTeste>().CountAsync(),
            await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM refresh_tokens").SingleAsync());
    }

    private async Task<int> TotalDeEntradasAsync()
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        return await db.Set<EntradaListaEspera>().CountAsync();
    }

    private static string Erro(JsonElement corpo, string campo) => corpo.GetProperty("errors").GetProperty(campo)[0].GetString()!;
}
