using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F15, F15b (F15-01, F15-03, F15-04, F15-06, F15-07, F15-09, F15-93 a F15-96; design D2, D9 e §1.2; contrato
/// §6): o endereço de entrada de cada empresa no <c>GET /configuracoes/empresa</c> e o
/// <c>PUT /configuracoes/recebimento-email</c> que liga e desliga o recebimento, com as recusas e a trilha.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed partial class RecebimentoEmailTests(ApiFixture api)
{
    private const string Erros = "https://acordu.app.br/erros/";
    private const string Empresa = "/api/v1/configuracoes/empresa";
    private const string Recebimento = "/api/v1/configuracoes/recebimento-email";

    private static readonly string[] CamposDaEmpresa =
        ["nome", "responderPara", "slaPadrao", "slaPersonalizado", "enderecoEntrada", "recebimentoEmail"];

    [GeneratedRegex(@"^[23456789abcdefghjkmnpqrstuvwxyz]{8}@entrada\.acordu\.app\.br$")]
    private static partial Regex FormatoDoEndereco();

    private static async Task<JsonElement> EmpresaAsync(Equipe equipe, string? token = null) =>
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, Empresa, token: token);

    private async Task SituacaoAsync(Guid tenantId, string situacao)
    {
        var desde = api.Relogio.Agora.ToUniversalTime();
        await using var db = api.DaEquipe(tenantId);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE tenants SET situacao = {situacao}, somente_leitura_desde = {desde} WHERE id = {tenantId}");
    }

    private static async Task<int> TrilhaAsync(Equipe equipe)
    {
        var trilha = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auditoria?acao=configuracao.recebimento-email");
        return trilha.GetProperty("total").GetInt32();
    }

    [Fact]
    public async Task Registro_EmpresasNovas_EnderecoProprioDiferenteSemONomeComRecebimentoDesligado() // F15-01
    {
        var primeira = await Equipe.CriarAsync(api);
        var segunda = await Equipe.CriarAsync(api);

        var a = await EmpresaAsync(primeira);
        var b = await EmpresaAsync(segunda);

        var enderecoA = a.GetProperty("enderecoEntrada").GetString();
        var enderecoB = b.GetProperty("enderecoEntrada").GetString();
        enderecoA.Should().MatchRegex(FormatoDoEndereco());
        enderecoB.Should().MatchRegex(FormatoDoEndereco());
        enderecoA.Should().NotBe(enderecoB, "as duas empresas têm o mesmo nome, e o endereço não sai dele");
        enderecoA.Should().NotContainEquivalentOf("traco");
        a.GetProperty("recebimentoEmail").GetBoolean().Should().BeFalse();
        b.GetProperty("recebimentoEmail").GetBoolean().Should().BeFalse();
        await using var db = api.DaEquipe(primeira.TenantId);
        var tenant = await db.Tenants.AsNoTracking().SingleAsync();
        enderecoA.Should().Be($"{tenant.CodigoEntrada}@{ApiFixture.DominioEntrada}");
    }

    [Fact]
    public async Task Ligar_RecebimentoDesligado_FicaLigadoNaRespostaENaLeitura() // F15-03
    {
        var equipe = await Equipe.CriarAsync(api);
        var antes = await EmpresaAsync(equipe);

        var ligado = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, Recebimento, new { ligado = true });

        ligado.GetProperty("recebimentoEmail").GetBoolean().Should().BeTrue();
        ligado.GetProperty("enderecoEntrada").GetString().Should().Be(antes.GetProperty("enderecoEntrada").GetString());
        var depois = await EmpresaAsync(equipe);
        depois.GetProperty("recebimentoEmail").GetBoolean().Should().BeTrue();
        depois.GetProperty("enderecoEntrada").GetString().Should().Be(antes.GetProperty("enderecoEntrada").GetString());
        await using var db = api.DaEquipe(equipe.TenantId);
        (await db.Tenants.AsNoTracking().SingleAsync()).RecebimentoEmail.Should().BeTrue();
    }

    [Fact]
    public async Task Desligar_RecebimentoLigado_FicaDesligadoComOMesmoEndereco() // F15-04
    {
        var equipe = await Equipe.CriarAsync(api);
        var ligado = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, Recebimento, new { ligado = true });

        var desligado = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, Recebimento, new { ligado = false });

        desligado.GetProperty("recebimentoEmail").GetBoolean().Should().BeFalse();
        desligado.GetProperty("enderecoEntrada").GetString().Should().Be(ligado.GetProperty("enderecoEntrada").GetString());
        (await EmpresaAsync(equipe)).GetProperty("recebimentoEmail").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Alterar_SemConfiguracaoGerenciar_403SemMudar() // F15-06
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var leitor = await equipe.ComPapelAsync(Papel.NomeLeitor);

        var ligar = await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Put, Recebimento, new { ligado = true }, membro);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, Recebimento, new { ligado = true });
        var desligar = await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Put, Recebimento, new { ligado = false }, leitor);

        ligar.GetProperty("type").GetString().Should().Be(Erros + "sem-permissao");
        desligar.GetProperty("type").GetString().Should().Be(Erros + "sem-permissao");
        (await EmpresaAsync(equipe)).GetProperty("recebimentoEmail").GetBoolean().Should().BeTrue("o desligar do Leitor não mudou nada");
        (await TrilhaAsync(equipe)).Should().Be(1, "só o ligar do Dono");
    }

    [Theory]
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task Alterar_EmpresaSoLeitura_422SoLeituraDepoisDaPermissaoSemMudar(string situacao) // F15-07
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, Recebimento, new { ligado = true });
        await SituacaoAsync(equipe.TenantId, situacao);
        var antes = (await EmpresaAsync(equipe)).GetRawText();

        var desligar = await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Put, Recebimento, new { ligado = false });
        var ligar = await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Put, Recebimento, new { ligado = true });
        var semPermissao = await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Put, Recebimento, new { ligado = false }, membro);

        desligar.GetProperty("type").GetString().Should().Be(Erros + "empresa-somente-leitura");
        ligar.GetProperty("type").GetString().Should().Be(Erros + "empresa-somente-leitura");
        semPermissao.GetProperty("type").GetString().Should().Be(Erros + "sem-permissao", "a permissão vem antes da situação");
        (await EmpresaAsync(equipe)).GetRawText().Should().Be(antes);
        (await TrilhaAsync(equipe)).Should().Be(1, "só o ligar de antes do só leitura");
    }

    [Fact]
    public async Task Ler_PessoaNasDuasEmpresas_VeSoOEnderecoDeCada_LigarEmANaoLigaB() // F15-09
    {
        var a = await Equipe.CriarAsync(api);
        var b = await Equipe.CriarAsync(api);
        var tokenEmB = await VincularComoDonoAsync(a, b);

        await a.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, Recebimento, new { ligado = true });
        var lidaEmA = await EmpresaAsync(a);
        var lidaEmB = await EmpresaAsync(b, tokenEmB);

        lidaEmA.GetProperty("recebimentoEmail").GetBoolean().Should().BeTrue();
        lidaEmB.GetProperty("recebimentoEmail").GetBoolean().Should().BeFalse("ligar em A não liga em B");
        lidaEmA.GetProperty("enderecoEntrada").GetString().Should().NotBe(lidaEmB.GetProperty("enderecoEntrada").GetString());
        (await EmpresaAsync(b)).GetProperty("enderecoEntrada").GetString()
            .Should().Be(lidaEmB.GetProperty("enderecoEntrada").GetString(), "em B a pessoa vê o endereço de B");
        (await TrilhaAsync(b)).Should().Be(0, "a trilha de A não aparece em B");
    }

    [Fact]
    public async Task Alterar_Ligar_TrilhaComAntesDepoisQuemEQuando() // F15-93
    {
        var equipe = await Equipe.CriarAsync(api);
        var usuario = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("usuarioId").GetGuid();

        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, Recebimento, new { ligado = true });
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, Recebimento, new { ligado = true });
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, Recebimento, new { ligado = false });

        var trilha = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auditoria?acao=configuracao.recebimento-email");
        var itens = trilha.GetProperty("itens").EnumerateArray().ToList();
        itens.Should().HaveCount(2, "ligar o que já está ligado não muda nada nem vai à trilha");
        // O relógio dos testes é fixo: as duas ações têm o mesmo instante, e a ordem da lista não diz qual veio antes.
        var ligou = itens.Single(i => i.GetProperty("dados").GetProperty("depois").GetProperty("recebimentoEmail").GetBoolean());
        var desligou = itens.Single(i => !i.GetProperty("dados").GetProperty("depois").GetProperty("recebimentoEmail").GetBoolean());
        ligou.GetProperty("dados").GetProperty("antes").GetProperty("recebimentoEmail").GetBoolean().Should().BeFalse();
        ligou.GetProperty("dados").GetProperty("depois").GetProperty("recebimentoEmail").GetBoolean().Should().BeTrue();
        desligou.GetProperty("dados").GetProperty("antes").GetProperty("recebimentoEmail").GetBoolean().Should().BeTrue();
        desligou.GetProperty("dados").GetProperty("depois").GetProperty("recebimentoEmail").GetBoolean().Should().BeFalse();
        foreach (var item in itens)
        {
            item.GetProperty("usuario").GetProperty("id").GetGuid().Should().Be(usuario);
            item.GetProperty("ocorridoEm").GetDateTimeOffset().Should().BeCloseTo(api.Relogio.Agora, TimeSpan.FromSeconds(1));
            item.GetProperty("alvoTipo").GetString().Should().Be("empresa");
            item.GetProperty("alvoId").GetGuid().Should().Be(equipe.TenantId);
            item.GetProperty("dados").GetRawText().Should().NotContain("@", "sem o endereço de entrada");
        }
    }

    [Fact]
    public async Task Ligar_SemDominioDeEntrada_SemEnderecoE422TransicaoInvalidaDepoisDaPermissao() // F15-94
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        await using var semEntrada = await api.NovaApiAsync(new Dictionary<string, string?> { ["EmailEntrada:Dominio"] = null });

        var (statusLeitura, leitura) = await EnviarAsync(semEntrada, HttpMethod.Get, Empresa, equipe.Dono);
        var (statusLigar, ligar) = await EnviarAsync(semEntrada, HttpMethod.Put, Recebimento, equipe.Dono, new { ligado = true });
        var (statusMembro, doMembro) = await EnviarAsync(semEntrada, HttpMethod.Put, Recebimento, membro, new { ligado = true });

        statusLeitura.Should().Be(HttpStatusCode.OK);
        leitura.GetProperty("enderecoEntrada").ValueKind.Should().Be(JsonValueKind.Null);
        leitura.GetProperty("recebimentoEmail").GetBoolean().Should().BeFalse();
        leitura.GetProperty("nome").GetString().Should().Be("Estúdio Traço", "os campos de hoje continuam");
        statusLigar.Should().Be(HttpStatusCode.UnprocessableEntity);
        ligar.GetProperty("type").GetString().Should().Be(Erros + "transicao-invalida");
        ligar.GetProperty("detail").GetString().Should().Contain("ecebimento por e-mail indisponível");
        statusMembro.Should().Be(HttpStatusCode.Forbidden, "a permissão vem antes");
        doMembro.GetProperty("type").GetString().Should().Be(Erros + "sem-permissao");
        (await EmpresaAsync(equipe)).GetProperty("recebimentoEmail").GetBoolean().Should().BeFalse("nada muda");
        (await TrilhaAsync(equipe)).Should().Be(0);
    }

    [Fact]
    public async Task Ler_PessoaSemConfiguracaoGerenciar_EnderecoERecebimentoAoLadoDosCamposDeHoje() // F15-95
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var leitor = await equipe.ComPapelAsync(Papel.NomeLeitor);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, Recebimento, new { ligado = true });
        var doDono = await EmpresaAsync(equipe);

        foreach (var token in new[] { membro, leitor })
        {
            var lida = await EmpresaAsync(equipe, token);

            lida.GetProperty("enderecoEntrada").GetString().Should().MatchRegex(FormatoDoEndereco())
                .And.Be(doDono.GetProperty("enderecoEntrada").GetString());
            lida.GetProperty("recebimentoEmail").GetBoolean().Should().BeTrue();
            lida.EnumerateObject().Select(p => p.Name).Should().Contain(CamposDaEmpresa);
        }
    }

    [Fact]
    public async Task Demonstracao_LerELigar_SemEnderecoDesligadoE422TransicaoInvalida() // F15-96
    {
        var operador = await OperadorNebrys.TokenAsync(api);
        var demonstracao = await Demonstracoes.GarantirAsync(api, operador);
        var pessoa = await Equipe.CriarAsync(api);
        await Demonstracoes.IndicarAsync(api, operador, pessoa.EmailDono);
        var naDemonstracao = await Demonstracoes.SessaoNaDemonstracaoAsync(api, pessoa, demonstracao);

        var lida = await EmpresaAsync(pessoa, naDemonstracao);
        var ligar = await pessoa.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Put, Recebimento, new { ligado = true }, naDemonstracao);

        lida.GetProperty("enderecoEntrada").ValueKind.Should().Be(JsonValueKind.Null);
        lida.GetProperty("recebimentoEmail").GetBoolean().Should().BeFalse();
        ligar.GetProperty("type").GetString().Should().Be(Erros + "transicao-invalida");
        (await EmpresaAsync(pessoa, naDemonstracao)).GetProperty("recebimentoEmail").GetBoolean().Should().BeFalse("nada muda");
        await using var db = api.DaEquipe(demonstracao);
        var tenant = await db.Tenants.AsNoTracking().SingleAsync();
        tenant.CodigoEntrada.Should().BeNull();
        tenant.RecebimentoEmail.Should().BeFalse();
    }

    [Fact]
    public async Task Alterar_SemLigado_400NoCampo()
    {
        var equipe = await Equipe.CriarAsync(api);

        var problema = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Put, Recebimento, new { });

        problema.GetProperty("errors").TryGetProperty("ligado", out _).Should().BeTrue();
        (await EmpresaAsync(equipe)).GetProperty("recebimentoEmail").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task EditarEmpresa_RecebimentoLigado_RespostaTrazOEnderecoESegueLigado() // F15-95 (PUT /configuracoes/empresa)
    {
        var equipe = await Equipe.CriarAsync(api);
        var ligado = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, Recebimento, new { ligado = true });
        var empresa = await EmpresaAsync(equipe);

        var salvo = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, Empresa, new
        {
            nome = "Estúdio Traço Design",
            responderPara = "atendimento@estudiotraco.com.br",
            slaPadrao = empresa.GetProperty("slaPadrao"),
        });

        salvo.GetProperty("enderecoEntrada").GetString().Should().Be(ligado.GetProperty("enderecoEntrada").GetString());
        salvo.GetProperty("recebimentoEmail").GetBoolean().Should().BeTrue("editar a empresa não mexe no recebimento");
        (await EmpresaAsync(equipe)).GetProperty("recebimentoEmail").GetBoolean().Should().BeTrue();
        (await TrilhaAsync(equipe)).Should().Be(1, "só o ligar; editar a empresa não vai à trilha do recebimento");
    }

    [Fact]
    public async Task Registro_CodigoValidoGravado() // F15-01: o código do cadastro é o do domínio
    {
        var equipe = await Equipe.CriarAsync(api);

        await using var db = api.DaEquipe(equipe.TenantId);
        var tenant = await db.Tenants.AsNoTracking().SingleAsync();

        CodigoDeEntrada.Valido(tenant.CodigoEntrada).Should().BeTrue();
        tenant.RecebimentoEmail.Should().BeFalse();
    }

    /// <summary>
    /// O Dono de <paramref name="a"/> ganha vínculo de Dono em <paramref name="b"/> (direto no banco, como o
    /// <see cref="Equipe.ComPapelAsync"/>) e troca a sessão para B: devolve o token em B. O token de A continua valendo
    /// até expirar (a troca só revoga o refresh).
    /// </summary>
    private async Task<string> VincularComoDonoAsync(Equipe a, Equipe b)
    {
        var usuario = (await a.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("usuarioId").GetGuid();
        await using (var db = api.DaEquipe(b.TenantId))
        {
            var dono = (await db.Papeis.ToListAsync()).Single(p => p.Nome == Papel.NomeDono);
            db.Add(Vinculo.Criar(Guid.NewGuid(), usuario, dono));
            await db.SaveChangesAsync();
        }

        using var http = api.NovoHttp();
        using var troca = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/auth/tenant-ativo", UriKind.Relative))
        {
            Content = JsonContent.Create(new { tenantId = b.TenantId }),
        };
        troca.Headers.Authorization = new AuthenticationHeaderValue("Bearer", a.Dono);
        troca.Headers.Add("Cookie", $"acordu_refresh={a.RefreshDono}");
        using var resposta = await http.SendAsync(troca);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private static async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        WebApplication app, HttpMethod metodo, string rota, string token, object? corpo = null)
    {
        using var http = app.GetTestClient();
        using var requisicao = new HttpRequestMessage(metodo, new Uri(rota, UriKind.Relative));
        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(requisicao);
        return (resposta.StatusCode, await resposta.Content.ReadFromJsonAsync<JsonElement>());
    }
}
