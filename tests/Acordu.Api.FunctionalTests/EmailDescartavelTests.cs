using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Application.Cadastro;
using Acordu.Application.Nebrys;
using Acordu.Domain.Entities;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14-60 (F14p; RF-AUT-10; design D9): e-mail de domínio descartável, pela lista versionada, é recusado no
/// cadastro e na lista de espera com <c>400</c> no campo do e-mail, e nada é gravado. Melhoria #224: a área da Nebrys também
/// não emite convite para ele (<c>400</c> no e-mail na emissão; <c>422</c> no convite a partir da lista de espera).
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
        // Desde o #224 a área não emite convite para descartável; o convite vem do banco, como um emitido antes disso (ou
        // antes de o domínio entrar na lista).
        var email = $"convite-{Guid.NewGuid():N}@mailinator.com";
        var token = await ConviteNoBancoAsync(email);
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

    [Theory] // #224: o cadastro recusaria o e-mail (F14-60), então a área não emite o convite
    [MemberData(nameof(Descartaveis))]
    public async Task EmitirConvite_EmailDescartavel_400NoEmailSemConviteEmailNemTrilha(string dominio)
    {
        var email = $"convite-{Guid.NewGuid():N}@{dominio}";
        await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);
        var operador = await OperadorNebrys.TokenAsync(api);

        using var resposta = await OperadorNebrys.EnviarAsync(
            api.NovoHttp(), HttpMethod.Post, "/api/v1/nebrys/convites", operador, JsonContent.Create(new { email }));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("type").GetString().Should().EndWith("/validacao");
        Erro(corpo, "email").Should().Be(MensagensDeCadastro.EmailDescartavel);
        var normalizado = email.ToLowerInvariant(); // o domínio da lista pode vir em caixa mista; o convite guardaria minúsculo
        (await ConvitesDoEmailAsync(normalizado)).Should().Be(0, "nenhum convite gravado");
        (await TrilhaComOEmailAsync(normalizado)).Should().Be(0, "sem trilha");
        api.Emails.Para(normalizado).Should().BeEmpty("nenhum e-mail sai");
        api.Emails.Para(email).Should().BeEmpty();
    }

    [Fact] // #224: o domínio comum segue recebendo o convite da área
    public async Task EmitirConvite_EmailComum_201()
    {
        await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);
        var operador = await OperadorNebrys.TokenAsync(api);
        var email = $"convite-{Guid.NewGuid():N}@gmail.com";

        using var resposta = await OperadorNebrys.EnviarAsync(
            api.NovoHttp(), HttpMethod.Post, "/api/v1/nebrys/convites", operador, JsonContent.Create(new { email }));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        api.Emails.Para(email).Should().ContainSingle();
    }

    [Fact] // #224: a entrada passou na inscrição, mas a lista de domínios ganhou o dela depois
    public async Task ConvidarDaListaDeEspera_EmailDescartavel_422SemConviteEmailNemTrilhaEAEntradaAguarda()
    {
        var email = $"espera-{Guid.NewGuid():N}@mailinator.com";
        var entrada = EntradaListaEspera.Registrar(
            Guid.CreateVersion7(), "Ana Souza", email, "Estúdio Traço", null, api.Relogio.Agora - TimeSpan.FromDays(3));
        await using (var db = api.DaEquipe(Guid.NewGuid()))
        {
            db.Add(entrada);
            await db.SaveChangesAsync();
        }

        await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);
        var operador = await OperadorNebrys.TokenAsync(api);

        using var resposta = await OperadorNebrys.EnviarAsync(
            api.NovoHttp(), HttpMethod.Post, $"/api/v1/nebrys/lista-espera/{entrada.Id}/convite", operador);

        resposta.StatusCode.Should().Be((HttpStatusCode)422);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("type").GetString().Should().EndWith("/transicao-invalida");
        corpo.GetProperty("detail").GetString().Should().Be(ConvitesEmpresaNebrys.EntradaComEmailDescartavel);
        (await ConvitesDoEmailAsync(email)).Should().Be(0, "nenhum convite gravado");
        (await TrilhaComOEmailAsync(email)).Should().Be(0, "sem trilha");
        api.Emails.Para(email).Should().BeEmpty("nenhum e-mail sai");
        await using var leitura = api.DaEquipe(Guid.NewGuid());
        var depois = await leitura.Set<EntradaListaEspera>().AsNoTracking().SingleAsync(e => e.Id == entrada.Id);
        depois.ConvidadaEm.Should().BeNull("a entrada continua aguardando");
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

    /// <summary>Um convite pendente gravado direto no banco (sem passar pela área), com o token do link.</summary>
    private async Task<string> ConviteNoBancoAsync(string email)
    {
        var (convite, token) = ConviteEmpresa.Emitir(Guid.CreateVersion7(), email, TimeSpan.FromDays(7), api.Relogio.Agora);
        await using var db = api.DaEquipe(Guid.NewGuid());
        db.Add(convite);
        await db.SaveChangesAsync();
        return token;
    }

    private async Task<int> ConvitesDoEmailAsync(string email)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        return await db.Set<ConviteEmpresa>().CountAsync(c => c.Email == email);
    }

    /// <summary>Linhas da trilha (com ou sem tenant) que citam o e-mail.</summary>
    private async Task<int> TrilhaComOEmailAsync(string email)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        var padrao = $"%{email}%";
        return await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM auditoria WHERE dados::text LIKE {padrao}")
            .SingleAsync();
    }

    private async Task<int> TotalDeEntradasAsync()
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        return await db.Set<EntradaListaEspera>().CountAsync();
    }

    private static string Erro(JsonElement corpo, string campo) => corpo.GetProperty("errors").GetProperty(campo)[0].GetString()!;
}
