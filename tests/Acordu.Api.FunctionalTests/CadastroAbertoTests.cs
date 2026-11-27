using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14-50 (F14p; RF-AUT-08; design D11): no modo aberto, o registro sem convite cria a empresa como antes do
/// F14 (<c>201</c>, sessão aberta, confirmação de e-mail), com todas as regras do F14: documento (F14g), e-mail descartável
/// (F14p) e aceite dos textos (F14h). A Api sobe com <c>Cadastro__Modo=aberto</c> explícito, sem depender do padrão da
/// fixture.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class CadastroAbertoTests(ApiFixture api)
{
    private const string Senha = "senha-bem-comprida";

    [Fact] // F14-50
    public async Task Registro_ModoAbertoSemConvite_201EmTesteComDocumentoAceiteEConfirmacao()
    {
        await using var app = await ApiNoModoAbertoAsync();
        var email = NovoEmail();
        var cpf = DocumentosDeTeste.NovoCpf();

        var (status, corpo) = await RegistrarAsync(app, Corpo(email, DocumentosDeTeste.ComPontuacaoCpf(cpf)));

        status.Should().Be(HttpStatusCode.Created, "corpo: {0}", corpo);
        var eu = await EuAsync(app, corpo.GetProperty("accessToken").GetString()!);
        eu.GetProperty("tenant").GetProperty("situacao").GetString().Should().Be("TESTE");
        eu.GetProperty("tenant").GetProperty("fimDoTeste").GetDateTimeOffset().Should().Be(api.Relogio.Agora + TimeSpan.FromDays(30));
        eu.GetProperty("papel").GetString().Should().Be(Papel.NomeDono);
        eu.GetProperty("emailConfirmado").GetBoolean().Should().BeFalse("sem convite, a conta confirma o e-mail como antes");
        api.Emails.Para(email).Should().ContainSingle(m => m.Categoria == "confirmacao-email");
        await using var db = api.DaEquipe(Guid.NewGuid());
        var hash = DocumentosDeTeste.Hash(cpf);
        (await db.Set<DocumentoTeste>().AnyAsync(d => d.Hash == hash))
            .Should().BeTrue("o documento fica registrado (um teste por documento)");
        var usuario = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        (await db.Set<AceiteTexto>().Where(a => a.UsuarioId == usuario).Select(a => a.Texto).ToListAsync())
            .Should().BeEquivalentTo(new[] { TipoTextoLegal.Termos, TipoTextoLegal.Privacidade }, "o aceite fica gravado");
    }

    public static TheoryData<string, string> RegrasDoF14 => new()
    {
        { "sem-documento", "documento" },
        { "documento-invalido", "documento" },
        { "email-descartavel", "email" },
        { "sem-aceite", "versaoTermos" },
        { "aceite-de-outra-versao", "versaoPrivacidade" },
    };

    [Theory] // F14-50: aberto não é sem regra; o registro sem convite cai em cada regra do F14 como no modo convite
    [MemberData(nameof(RegrasDoF14))]
    public async Task Registro_ModoAbertoSemConvite_CadaRegraDoF14Recusa400NoCampoENadaCriado(string caso, string campo)
    {
        await using var app = await ApiNoModoAbertoAsync();
        var email = caso == "email-descartavel" ? $"ana-{Guid.NewGuid():N}@mailinator.com" : NovoEmail();
        var corpo = caso switch
        {
            "sem-documento" => Corpo(email, documento: null),
            "documento-invalido" => Corpo(email, DocumentosDeTeste.ComDigitoErrado(DocumentosDeTeste.NovoCpf())),
            "sem-aceite" => Corpo(email, DocumentosDeTeste.Novo(), versaoTermos: null),
            "aceite-de-outra-versao" => Corpo(email, DocumentosDeTeste.Novo(), versaoPrivacidade: "0.0.0"),
            _ => Corpo(email, DocumentosDeTeste.Novo()),
        };

        var (status, resposta) = await RegistrarAsync(app, corpo);

        status.Should().Be(HttpStatusCode.BadRequest, "corpo: {0}", resposta);
        resposta.GetProperty("errors").TryGetProperty(campo, out _).Should().BeTrue("a recusa é no campo {0}: {1}", campo, resposta);
        await using var db = api.DaEquipe(Guid.NewGuid());
        (await db.Users.AnyAsync(u => u.Email == email)).Should().BeFalse("nada criado");
    }

    [Fact] // F14-50: o documento que já teve teste é recusado também no modo aberto
    public async Task Registro_ModoAbertoDocumentoQueJaTeveTeste_400NoDocumento()
    {
        await using var app = await ApiNoModoAbertoAsync();
        var cnpj = DocumentosDeTeste.Novo();
        (await RegistrarAsync(app, Corpo(NovoEmail(), cnpj))).Status.Should().Be(HttpStatusCode.Created);

        var (status, corpo) = await RegistrarAsync(app, Corpo(NovoEmail(), cnpj));

        status.Should().Be(HttpStatusCode.BadRequest);
        corpo.GetProperty("errors").GetProperty("documento")[0].GetString()
            .Should().StartWith("Este CPF ou CNPJ já teve um período de teste no Acordu.").And.Contain(ApiFixture.ContatoFornecedor);
    }

    private Task<WebApplication> ApiNoModoAbertoAsync() =>
        api.NovaApiAsync(new Dictionary<string, string?> { ["Cadastro:Modo"] = "aberto" });

    private static object Corpo(
        string email,
        string? documento,
        string? versaoTermos = TextosAceitos.Termos,
        string? versaoPrivacidade = TextosAceitos.Privacidade) => new
    {
        nome = "Ana Souza", email, senha = Senha, nomeEmpresa = "Estúdio Traço", documento, versaoTermos, versaoPrivacidade,
    };

    private static async Task<(HttpStatusCode Status, JsonElement Corpo)> RegistrarAsync(WebApplication app, object corpo)
    {
        using var http = app.GetTestClient();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), corpo);
        return (resposta.StatusCode, await resposta.Content.ReadFromJsonAsync<JsonElement>());
    }

    private static async Task<JsonElement> EuAsync(WebApplication app, string accessToken)
    {
        using var http = app.GetTestClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/auth/eu", UriKind.Relative));
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resposta = await http.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string NovoEmail() => $"aberto-{Guid.NewGuid():N}@exemplo.com";
}
