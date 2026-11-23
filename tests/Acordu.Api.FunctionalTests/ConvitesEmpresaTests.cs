using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Acordu.Api.Composicao;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14n (F14-45, F14-47 a F14-49, F14-51, F14-72 a F14-74, F14-105; design D11, D12 e D14): convite de
/// empresa emitido, reenviado e revogado pela área da Nebrys, modo do cadastro e o registro com convite. A fixture sobe o
/// cadastro aberto; o modo convite sobe outra Api sobre o mesmo banco.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class ConvitesEmpresaTests(ApiFixture api)
{
    private const string Area = "/api/v1/nebrys";
    private const string Senha = "senha-bem-comprida";
    private const string CabecalhoIp = "X-Teste-Ip";

    // ---------- F14-72: emitir ----------

    [Fact]
    public async Task Emitir_EmailNovo_EmailComOLinkEConvitePendenteComValidadeNaTrilha() // F14-72
    {
        var (token, operador) = await OperadorAsync();
        var email = NovoEmail();

        var (status, corpo) = await EmitirAsync(token, $"  {email.ToUpperInvariant()} ");

        status.Should().Be(HttpStatusCode.Created, "corpo: {0}", corpo);
        corpo.GetProperty("emailEnviado").GetBoolean().Should().BeTrue();
        var convite = corpo.GetProperty("convite");
        var id = convite.GetProperty("id").GetGuid();
        convite.GetProperty("email").GetString().Should().Be(email);
        convite.GetProperty("situacao").GetString().Should().Be("PENDENTE");
        convite.GetProperty("expiraEm").GetDateTimeOffset().Should().Be(api.Relogio.Agora + TimeSpan.FromDays(7));
        convite.GetProperty("usadoEm").ValueKind.Should().Be(JsonValueKind.Null);
        convite.TryGetProperty("token", out _).Should().BeFalse("o token só vai no e-mail");
        var mensagem = api.Emails.Para(email).Should().ContainSingle().Subject;
        mensagem.Categoria.Should().Be("convite-empresa");
        mensagem.Texto.Should().Contain("/cadastro/convite/").And.Contain("7 dias");
        (await ConsultarAsync(api.NovoHttp(), TokenDoConvite(email))).Status.Should().Be(HttpStatusCode.OK);

        var lista = await ListaAsync(token);
        lista.GetProperty("itens").EnumerateArray().Should().Contain(i => i.GetProperty("id").GetGuid() == id);
        var trilha = await TrilhaAsync(id);
        trilha.Should().ContainSingle(t => t.Acao == "nebrys.convite.emissao" && t.Usuario == operador);
        trilha.Should().AllSatisfy(t => t.Dados.Should().Contain(email).And.NotContain(TokenDoConvite(email)));
    }

    [Fact]
    public async Task Emitir_EmailJaComConviteEmAberto_422SemOutroConviteNemEmail()
    {
        var (token, _) = await OperadorAsync();
        var email = NovoEmail();
        (await EmitirAsync(token, email)).Status.Should().Be(HttpStatusCode.Created);

        var (status, corpo) = await EmitirAsync(token, email);

        status.Should().Be((HttpStatusCode)422, "corpo: {0}", corpo);
        corpo.GetProperty("type").GetString().Should().EndWith("/transicao-invalida");
        api.Emails.Para(email).Should().ContainSingle();
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-arroba")]
    public async Task Emitir_EmailVazioOuInvalido_400NoCampo(string email)
    {
        var (token, _) = await OperadorAsync();

        var (status, corpo) = await EmitirAsync(token, email);

        status.Should().Be(HttpStatusCode.BadRequest, "corpo: {0}", corpo);
        corpo.GetProperty("errors").TryGetProperty("email", out _).Should().BeTrue();
    }

    [Fact]
    public async Task RotasDosConvites_SemSessaoDeOperacao_404() // F14-63 vale para as rotas novas
    {
        var equipe = await Equipe.CriarAsync(api);

        using var comEquipe = await OperadorNebrys.EnviarAsync(
            api.NovoHttp(), HttpMethod.Post, $"{Area}/convites", equipe.Dono, Json(new { email = NovoEmail() }));
        using var anonimo = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Get, $"{Area}/convites", null);

        comEquipe.StatusCode.Should().Be(HttpStatusCode.NotFound);
        anonimo.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- F14-73: reenviar ----------

    [Fact]
    public async Task Reenviar_Pendente_LinkNovoValeEOAnteriorNao() // F14-73
    {
        var (token, operador) = await OperadorAsync();
        var email = NovoEmail();
        var id = await EmitidoAsync(token, email);
        var anterior = TokenDoConvite(email);

        var (status, corpo) = await AgirAsync(token, id, "reenvio");

        status.Should().Be(HttpStatusCode.OK, "corpo: {0}", corpo);
        corpo.GetProperty("emailEnviado").GetBoolean().Should().BeTrue();
        corpo.GetProperty("convite").GetProperty("situacao").GetString().Should().Be("PENDENTE");
        var novo = TokenDoConvite(email);
        novo.Should().NotBe(anterior);
        (await ConsultarAsync(api.NovoHttp(), anterior)).Status.Should().Be(HttpStatusCode.Unauthorized);
        await using var app = await ApiNoModoConviteAsync();
        var (recusado, erro) = await RegistrarAsync(app, email, anterior);
        recusado.Should().Be(HttpStatusCode.BadRequest);
        Erro(erro, "convite").Should().Contain("não vale mais");
        (await RegistrarAsync(app, email, novo)).Status.Should().Be(HttpStatusCode.Created);
        (await TrilhaAsync(id)).Should().Contain(t => t.Acao == "nebrys.convite.reenvio" && t.Usuario == operador);
    }

    [Fact]
    public async Task Reenviar_Vencido_VoltaAValerComValidadeNova() // F14-73
    {
        var (token, _) = await OperadorAsync();
        var email = NovoEmail();
        var id = await EmitidoAsync(token, email);
        await VencerAsync(id);
        (await ConsultarAsync(api.NovoHttp(), TokenDoConvite(email))).Status.Should().Be(HttpStatusCode.Unauthorized);

        var (status, corpo) = await AgirAsync(token, id, "reenvio");

        status.Should().Be(HttpStatusCode.OK, "corpo: {0}", corpo);
        corpo.GetProperty("convite").GetProperty("expiraEm").GetDateTimeOffset().Should().Be(api.Relogio.Agora + TimeSpan.FromDays(7));
        (await ConsultarAsync(api.NovoHttp(), TokenDoConvite(email))).Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReenviarERevogar_ConviteInexistente_404()
    {
        var (token, _) = await OperadorAsync();

        (await AgirAsync(token, Guid.NewGuid(), "reenvio")).Status.Should().Be(HttpStatusCode.NotFound);
        (await AgirAsync(token, Guid.NewGuid(), "revogacao")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- F14-74: revogar ----------

    [Fact]
    public async Task Revogar_Pendente_OLinkParaDeValerEORegistroERecusado() // F14-74, F14-45
    {
        var (token, operador) = await OperadorAsync();
        var email = NovoEmail();
        var id = await EmitidoAsync(token, email);

        var (status, corpo) = await AgirAsync(token, id, "revogacao");

        status.Should().Be(HttpStatusCode.OK, "corpo: {0}", corpo);
        corpo.GetProperty("situacao").GetString().Should().Be("REVOGADO");
        corpo.GetProperty("revogadoEm").GetDateTimeOffset().Should().Be(api.Relogio.Agora);
        (await ConsultarAsync(api.NovoHttp(), TokenDoConvite(email))).Status.Should().Be(HttpStatusCode.Unauthorized);
        await using var app = await ApiNoModoConviteAsync();
        var antes = await ContagensAsync();
        var (recusado, erro) = await RegistrarAsync(app, email, TokenDoConvite(email));
        recusado.Should().Be(HttpStatusCode.BadRequest);
        Erro(erro, "convite").Should().Contain("não vale mais");
        (await ContagensAsync()).Should().Be(antes);
        (await AgirAsync(token, id, "revogacao")).Status.Should().Be((HttpStatusCode)422, "já revogado");
        (await AgirAsync(token, id, "reenvio")).Status.Should().Be((HttpStatusCode)422, "revogado não volta");
        (await TrilhaAsync(id)).Should().Contain(t => t.Acao == "nebrys.convite.revogacao" && t.Usuario == operador);
    }

    // ---------- D11: modo do cadastro ----------

    [Fact]
    public async Task Cadastro_ModoDaConfiguracao_DevolveOModo()
    {
        await using var convite = await ApiNoModoConviteAsync();
        await using var semModo = await api.NovaApiAsync(new Dictionary<string, string?> { ["Cadastro:Modo"] = null });

        (await ModoAsync(api.NovoHttp())).Should().Be("ABERTO");
        (await ModoAsync(convite.GetTestClient())).Should().Be("CONVITE");
        (await ModoAsync(semModo.GetTestClient())).Should().Be("CONVITE", "sem a variável, o cadastro fica fechado");
    }

    [Theory]
    [InlineData("Cadastro:Modo", "aberta", "Cadastro__Modo")]
    [InlineData("Cadastro:DiasConvite", "0", "Cadastro__DiasConvite")]
    public async Task Api_ConfiguracaoDoCadastroInvalida_NaoSobe(string chave, string valor, string mensagem)
    {
        var acao = () => api.NovaApiAsync(new Dictionary<string, string?> { [chave] = valor });

        (await acao.Should().ThrowAsync<InvalidOperationException>()).WithMessage($"*{mensagem}*");
    }

    // ---------- F14-45: modo convite sem convite que vale ----------

    [Fact]
    public async Task Registro_ModoConviteSemConvite_400NoConviteENadaCriado() // F14-45
    {
        await using var app = await ApiNoModoConviteAsync();
        await using var semModo = await api.NovaApiAsync(new Dictionary<string, string?> { ["Cadastro:Modo"] = null });
        var antes = await ContagensAsync();

        var (status, corpo) = await RegistrarAsync(app, NovoEmail(), convite: null);
        var (statusSemModo, _) = await RegistrarAsync(semModo, NovoEmail(), convite: "   ");

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo, "convite").Should().Contain("só por convite").And.Contain(ApiFixture.ContatoNebrys);
        statusSemModo.Should().Be(HttpStatusCode.BadRequest, "sem a variável, vale o modo convite");
        (await ContagensAsync()).Should().Be(antes);
    }

    [Theory]
    [InlineData("inexistente")]
    [InlineData("vencido")]
    [InlineData("usado")]
    [InlineData("revogado")]
    public async Task Registro_ConviteQueNaoVale_400NoConviteSemDistinguirENadaCriado(string motivo) // F14-45, F14-48
    {
        var (token, _) = await OperadorAsync();
        var email = NovoEmail();
        var id = await EmitidoAsync(token, email);
        await using var app = await ApiNoModoConviteAsync();
        var tokenConvite = TokenDoConvite(email);
        switch (motivo)
        {
            case "inexistente":
                tokenConvite = new string('x', 43);
                break;
            case "vencido":
                await VencerAsync(id);
                break;
            case "usado":
                (await RegistrarAsync(app, email, tokenConvite)).Status.Should().Be(HttpStatusCode.Created);
                break;
            case "revogado":
                (await AgirAsync(token, id, "revogacao")).Status.Should().Be(HttpStatusCode.OK);
                break;
        }

        var antes = await ContagensAsync();

        var (status, corpo) = await RegistrarAsync(app, motivo == "usado" ? NovoEmail() : email, tokenConvite);

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo, "convite").Should().Be(
            $"Este convite não vale mais: venceu, já foi usado ou foi revogado. Para pedir outro, fale com a Nebrys: {ApiFixture.ContatoNebrys}.");
        (await ContagensAsync()).Should().Be(antes);
    }

    // ---------- F14-47 e F14-48: registro com o convite ----------

    [Fact]
    public async Task Registro_ConviteValidoComOEmailDele_201EmTesteConviteUsadoEContaConfirmada() // F14-47
    {
        var (token, _) = await OperadorAsync();
        var email = NovoEmail();
        var id = await EmitidoAsync(token, email);
        await using var app = await ApiNoModoConviteAsync();

        var (status, corpo) = await RegistrarAsync(app, email.ToUpperInvariant(), TokenDoConvite(email));

        status.Should().Be(HttpStatusCode.Created, "corpo: {0}", corpo);
        var eu = await EuAsync(corpo.GetProperty("accessToken").GetString()!);
        eu.GetProperty("tenant").GetProperty("situacao").GetString().Should().Be("TESTE");
        eu.GetProperty("tenant").GetProperty("fimDoTeste").GetDateTimeOffset().Should().Be(api.Relogio.Agora + TimeSpan.FromDays(30));
        eu.GetProperty("emailConfirmado").GetBoolean().Should().BeTrue("o token do convite chegou por este e-mail");
        eu.GetProperty("tenant").GetProperty("envioLiberado").GetBoolean().Should().BeTrue();
        api.Emails.Para(email).Should().NotContain(m => m.Categoria == "confirmacao-email");
        var convite = await ConviteAsync(token, id);
        convite.GetProperty("situacao").GetString().Should().Be("USADO");
        convite.GetProperty("usadoEm").GetDateTimeOffset().Should().Be(api.Relogio.Agora);
    }

    [Fact]
    public async Task Registro_ConviteNoModoAberto_TambemFicaUsado() // F14-47 (o convite vale em qualquer modo)
    {
        var (token, _) = await OperadorAsync();
        var email = NovoEmail();
        var id = await EmitidoAsync(token, email);

        var (status, corpo) = await RegistrarAsync(api.App, email, TokenDoConvite(email));

        status.Should().Be(HttpStatusCode.Created, "corpo: {0}", corpo);
        (await ConviteAsync(token, id)).GetProperty("situacao").GetString().Should().Be("USADO");
        var (outro, _) = await RegistrarAsync(api.App, NovoEmail(), TokenDoConvite(email));
        outro.Should().Be(HttpStatusCode.BadRequest, "usado, não vale nem no modo aberto (F14-48)");
    }

    [Fact]
    public async Task Registro_DoisCadastrosAoMesmoTempoComOMesmoConvite_UmPassa() // F14-48
    {
        var (token, _) = await OperadorAsync();
        var email = NovoEmail();
        await EmitidoAsync(token, email);
        await using var app = await ApiNoModoConviteAsync();
        var convite = TokenDoConvite(email);

        var resultados = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RegistrarStatusAsync(app, email, convite)));

        resultados.Count(s => s == HttpStatusCode.Created).Should().Be(1);
        resultados.Where(s => s != HttpStatusCode.Created).Should().AllSatisfy(s => s.Should().Be(HttpStatusCode.BadRequest));
        await using var db = api.DaEquipe(Guid.NewGuid());
        (await db.Users.CountAsync(u => u.NormalizedEmail == email.ToUpperInvariant())).Should().Be(1);
    }

    // ---------- F14-49: outro e-mail ----------

    [Fact]
    public async Task Registro_ConviteComOutroEmail_400NoEmailEOConviteContinuaValido() // F14-49
    {
        var (token, _) = await OperadorAsync();
        var email = NovoEmail();
        var id = await EmitidoAsync(token, email);
        await using var app = await ApiNoModoConviteAsync();
        var antes = await ContagensAsync();

        var (status, corpo) = await RegistrarAsync(app, NovoEmail(), TokenDoConvite(email));

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo, "email").Should().Contain("e-mail que recebeu o convite").And.NotContain(email);
        (await ContagensAsync()).Should().Be(antes);
        (await ConviteAsync(token, id)).GetProperty("situacao").GetString().Should().Be("PENDENTE");
        (await RegistrarAsync(app, email, TokenDoConvite(email))).Status.Should().Be(HttpStatusCode.Created, "o convite continua valendo");
    }

    // ---------- F14-105: e-mail que já tem conta ----------

    [Fact]
    public async Task Registro_ConviteParaEmailQueJaTemConta_400NoEmailEOConviteContinuaValido() // F14-105
    {
        var equipe = await Equipe.CriarAsync(api);
        var (token, _) = await OperadorAsync();
        var id = await EmitidoAsync(token, equipe.EmailDono);
        await using var app = await ApiNoModoConviteAsync();
        var (consulta, info) = await ConsultarAsync(app.GetTestClient(), TokenDoConvite(equipe.EmailDono));
        consulta.Should().Be(HttpStatusCode.OK);
        info.GetProperty("temConta").GetBoolean().Should().BeTrue();
        var antes = await ContagensAsync();

        var (status, corpo) = await RegistrarAsync(app, equipe.EmailDono, TokenDoConvite(equipe.EmailDono));

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo, "email").Should().Be("Este e-mail já tem cadastro.");
        (await ContagensAsync()).Should().Be(antes);
        (await ConviteAsync(token, id)).GetProperty("situacao").GetString().Should().Be("PENDENTE");
    }

    // ---------- F14-46 (API): consulta do convite ----------

    [Fact]
    public async Task ConsultarConvite_TokenValido_EmailSemContaETokenRuim401()
    {
        var (token, _) = await OperadorAsync();
        var email = NovoEmail();
        await EmitidoAsync(token, email);

        var (status, corpo) = await ConsultarAsync(api.NovoHttp(), TokenDoConvite(email));
        var (ruim, _) = await ConsultarAsync(api.NovoHttp(), new string('y', 43));

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("email").GetString().Should().Be(email);
        corpo.GetProperty("temConta").GetBoolean().Should().BeFalse();
        ruim.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- F14-51: Turnstile e limite por IP no modo convite ----------

    [Fact]
    public async Task Registro_ModoConviteComConviteValidoSemTurnstile_400NoTurnstileEOConviteContinuaValido() // F14-51
    {
        var (token, _) = await OperadorAsync();
        var email = NovoEmail();
        var id = await EmitidoAsync(token, email);
        using var siteverify = new SiteverifyAprovado();
        await using var app = await api.NovaApiAsync(
            new Dictionary<string, string?>
            {
                ["Cadastro:Modo"] = "convite",
                ["Turnstile:Segredo"] = "segredo-dos-testes",
                ["App:UrlPublica"] = "https://acordu.app.br/",
            },
            servicos: s => s.AddHttpClient(ComposicaoApi.NomeClienteTurnstile).ConfigurePrimaryHttpMessageHandler(() => siteverify));

        var (status, corpo) = await RegistrarAsync(app, email, TokenDoConvite(email), tokenTurnstile: null);

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo, "tokenTurnstile").Should().Contain("robô");
        siteverify.Chamadas.Should().Be(0);
        (await ConviteAsync(token, id)).GetProperty("situacao").GetString().Should().Be("PENDENTE");
        (await RegistrarAsync(app, email, TokenDoConvite(email), tokenTurnstile: "token-bom")).Status.Should().Be(HttpStatusCode.Created);
        siteverify.Chamadas.Should().Be(1);
    }

    [Fact]
    public async Task Registro_ModoConvite_LimitePorIpContaComESemConvite() // F14-51
    {
        var (token, _) = await OperadorAsync();
        var email = NovoEmail();
        await EmitidoAsync(token, email);
        await using var app = await api.NovaApiAsync(
            new Dictionary<string, string?> { ["Cadastro:Modo"] = "convite", ["RateLimit:Registro:PorHora"] = "3" },
            antes: IpDoCabecalho);
        const string Ip = "198.51.100.40";

        for (var i = 0; i < 3; i++)
            (await RegistrarAsync(app, NovoEmail(), convite: null, ip: Ip)).Status.Should().Be(HttpStatusCode.BadRequest);

        (await RegistrarAsync(app, email, TokenDoConvite(email), ip: Ip)).Status.Should().Be(HttpStatusCode.TooManyRequests);
        (await RegistrarAsync(app, email, TokenDoConvite(email), ip: "198.51.100.41")).Status.Should().Be(HttpStatusCode.Created);
    }

    // ---------- lacunas do testador: permissão, leitura, situações e concorrência ----------

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/{id}")]
    [InlineData("POST", "")]
    [InlineData("POST", "/{id}/reenvio")]
    [InlineData("POST", "/{id}/revogacao")]
    public async Task RotasDosConvites_UsuarioDeEmpresaOuAnonimo_404EOConviteNaoMuda(string metodo, string sufixo) // F14-63, F14-72 a F14-74
    {
        var (token, _) = await OperadorAsync();
        var email = NovoEmail();
        var id = await EmitidoAsync(token, email);
        var equipe = await Equipe.CriarAsync(api);
        var rota = $"{Area}/convites{sufixo.Replace("{id}", id.ToString(), StringComparison.Ordinal)}";
        var verbo = new HttpMethod(metodo);

        foreach (var portador in new string?[] { equipe.Dono, null })
        {
            using var resposta = await OperadorNebrys.EnviarAsync(
                api.NovoHttp(), verbo, rota, portador, metodo == "POST" && sufixo.Length == 0 ? Json(new { email = NovoEmail() }) : null);
            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        var convite = await ConviteAsync(token, id);
        convite.GetProperty("situacao").GetString().Should().Be("PENDENTE");
        api.Emails.Para(email).Should().ContainSingle("nenhuma das chamadas sem sessão de operação reenviou");
    }

    [Fact]
    public async Task Convites_DetalheInexistente_404()
    {
        var (token, _) = await OperadorAsync();

        using var resposta = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Get, $"{Area}/convites/{Guid.NewGuid()}", token);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Convites_ListaEDetalhe_SituacoesCalculadasENuncaOTokenNemOHash()
    {
        var (token, _) = await OperadorAsync();
        var vencido = NovoEmail();
        var revogado = NovoEmail();
        var usado = NovoEmail();
        var pendente = NovoEmail();
        var idVencido = await EmitidoAsync(token, vencido);
        var idRevogado = await EmitidoAsync(token, revogado);
        var idUsado = await EmitidoAsync(token, usado);
        var idPendente = await EmitidoAsync(token, pendente);
        await VencerAsync(idVencido);
        (await AgirAsync(token, idRevogado, "revogacao")).Status.Should().Be(HttpStatusCode.OK);
        (await RegistrarAsync(api.App, usado, TokenDoConvite(usado))).Status.Should().Be(HttpStatusCode.Created);

        var lista = await ListaAsync(token);

        var itens = lista.GetProperty("itens").EnumerateArray().ToList();
        string Situacao(Guid id) => itens.Single(i => i.GetProperty("id").GetGuid() == id).GetProperty("situacao").GetString()!;
        Situacao(idVencido).Should().Be("VENCIDO");
        Situacao(idRevogado).Should().Be("REVOGADO");
        Situacao(idUsado).Should().Be("USADO");
        Situacao(idPendente).Should().Be("PENDENTE");
        itens.Should().AllSatisfy(i =>
        {
            i.TryGetProperty("token", out _).Should().BeFalse();
            i.TryGetProperty("tokenHash", out _).Should().BeFalse();
        });
        (await ConviteAsync(token, idVencido)).GetProperty("situacao").GetString().Should().Be("VENCIDO");
    }

    [Fact]
    public async Task Convites_ListaComPaginacao_RespeitaOTamanhoDaPagina()
    {
        var (token, _) = await OperadorAsync();
        await EmitidoAsync(token, NovoEmail());
        await EmitidoAsync(token, NovoEmail());

        using var resposta = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Get, $"{Area}/convites?pagina=1&tamanhoPagina=1", token);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CorpoAsync(resposta)).GetProperty("itens").GetArrayLength().Should().Be(1);
    }

    [Theory]
    [InlineData("usado")]
    [InlineData("revogado")]
    public async Task ReenviarERevogar_ConviteUsadoOuRevogado_422SemEnviarNemMudar(string situacao)
    {
        var (token, _) = await OperadorAsync();
        var email = NovoEmail();
        var id = await EmitidoAsync(token, email);
        if (situacao == "usado")
            (await RegistrarAsync(api.App, email, TokenDoConvite(email))).Status.Should().Be(HttpStatusCode.Created);
        else
            (await AgirAsync(token, id, "revogacao")).Status.Should().Be(HttpStatusCode.OK);
        var enviados = api.Emails.Para(email).Count(m => m.Categoria == "convite-empresa");

        var (reenvio, corpoReenvio) = await AgirAsync(token, id, "reenvio");
        var (revogacao, corpoRevogacao) = await AgirAsync(token, id, "revogacao");

        reenvio.Should().Be((HttpStatusCode)422, "corpo: {0}", corpoReenvio);
        corpoReenvio.GetProperty("type").GetString().Should().EndWith("/transicao-invalida");
        revogacao.Should().Be((HttpStatusCode)422, "corpo: {0}", corpoRevogacao);
        api.Emails.Para(email).Count(m => m.Categoria == "convite-empresa").Should().Be(enviados);
        (await ConviteAsync(token, id)).GetProperty("situacao").GetString().Should().Be(situacao.ToUpperInvariant());
    }

    [Fact]
    public async Task Emitir_EmailComConviteRevogado_EmiteOutroEOLinkNovoVale()
    {
        var (token, _) = await OperadorAsync();
        var email = NovoEmail();
        var primeiro = await EmitidoAsync(token, email);
        (await AgirAsync(token, primeiro, "revogacao")).Status.Should().Be(HttpStatusCode.OK);

        var (status, corpo) = await EmitirAsync(token, email);

        status.Should().Be(HttpStatusCode.Created, "revogado não conta como em aberto; corpo: {0}", corpo);
        corpo.GetProperty("convite").GetProperty("id").GetGuid().Should().NotBe(primeiro);
        await using var app = await ApiNoModoConviteAsync();
        (await RegistrarAsync(app, email, api.Emails.TokenDe(email, "convite-empresa"))).Status.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Convites_PendenteVencidoNoRegistro_NaoVale_ReenvioReabre()
    {
        var (token, _) = await OperadorAsync();
        var email = NovoEmail();
        var id = await EmitidoAsync(token, email);
        var antigo = TokenDoConvite(email);
        await VencerAsync(id);
        await using var app = await ApiNoModoConviteAsync();

        (await RegistrarAsync(app, email, antigo)).Status.Should().Be(HttpStatusCode.BadRequest);
        (await AgirAsync(token, id, "reenvio")).Status.Should().Be(HttpStatusCode.OK);

        (await RegistrarAsync(app, email, antigo)).Status.Should().Be(HttpStatusCode.BadRequest, "o link velho não volta");
        (await RegistrarAsync(app, email, TokenDoConvite(email))).Status.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Registro_ModoAbertoSemConvite_201() // F14-50, o contraste do modo convite
    {
        var (status, corpo) = await RegistrarAsync(api.App, NovoEmail(), convite: null);

        status.Should().Be(HttpStatusCode.Created, "corpo: {0}", corpo);
    }

    [Fact]
    public async Task Registro_ModoAbertoComConviteQueNaoVale_400NoConvite()
    {
        var (status, corpo) = await RegistrarAsync(api.App, NovoEmail(), new string('z', 43));

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo, "convite").Should().Contain("não vale mais");
    }

    [Fact]
    public async Task Registro_AbertoDoisCadastrosAoMesmoTempoComOMesmoEmail_UmPassaOsOutros400NoEmailSem500()
    {
        var email = NovoEmail();

        var resultados = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RegistrarAsync(api.App, email, convite: null)));

        resultados.Count(r => r.Status == HttpStatusCode.Created).Should().Be(1);
        resultados.Where(r => r.Status != HttpStatusCode.Created).Should().AllSatisfy(r =>
        {
            r.Status.Should().Be(HttpStatusCode.BadRequest);
            Erro(r.Corpo, "email").Should().Be("Este e-mail já tem cadastro.");
        });
        await using var db = api.DaEquipe(Guid.NewGuid());
        (await db.Users.CountAsync(u => u.NormalizedEmail == email.ToUpperInvariant())).Should().Be(1);
    }

    [Fact]
    public async Task Registro_ConvitePresenteComCamposInvalidos_400SemUsarOConvite()
    {
        var (token, _) = await OperadorAsync();
        var email = NovoEmail();
        var id = await EmitidoAsync(token, email);
        await using var app = await ApiNoModoConviteAsync();
        using var http = app.GetTestClient();

        using var resposta = await http.PostAsJsonAsync(
            new Uri("/api/v1/auth/registro", UriKind.Relative),
            new
            {
                nome = "Ana Souza", email, senha = "curta", nomeEmpresa = "Estúdio Traço", documento = DocumentosDeTeste.Novo(),
                versaoTermos = TextosAceitos.Termos, versaoPrivacidade = TextosAceitos.Privacidade, convite = TokenDoConvite(email),
            });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty("senha", out _).Should().BeTrue();
        (await ConviteAsync(token, id)).GetProperty("situacao").GetString().Should().Be("PENDENTE", "o cadastro que falhou não gasta o convite");
    }

    // ---------- apoio ----------

    private static string NovoEmail() => $"convite-{Guid.NewGuid():N}@exemplo.com";

    private string TokenDoConvite(string email) => api.Emails.TokenDe(email, "convite-empresa");

    private Task<WebApplication> ApiNoModoConviteAsync() =>
        api.NovaApiAsync(new Dictionary<string, string?> { ["Cadastro:Modo"] = "convite" });

    private async Task<(string Token, Guid Operador)> OperadorAsync()
    {
        var operador = await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);
        return (await OperadorNebrys.TokenAsync(api), operador);
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> EmitirAsync(string token, string email)
    {
        using var resposta = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Post, $"{Area}/convites", token, Json(new { email }));
        return (resposta.StatusCode, await CorpoAsync(resposta));
    }

    private async Task<Guid> EmitidoAsync(string token, string email)
    {
        var (status, corpo) = await EmitirAsync(token, email);
        status.Should().Be(HttpStatusCode.Created, "corpo: {0}", corpo);
        return corpo.GetProperty("convite").GetProperty("id").GetGuid();
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> AgirAsync(string token, Guid id, string acao)
    {
        using var resposta = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Post, $"{Area}/convites/{id}/{acao}", token);
        return (resposta.StatusCode, await CorpoAsync(resposta));
    }

    private async Task<JsonElement> ConviteAsync(string token, Guid id)
    {
        using var resposta = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Get, $"{Area}/convites/{id}", token);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await CorpoAsync(resposta);
    }

    private async Task<JsonElement> ListaAsync(string token)
    {
        using var resposta = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Get, $"{Area}/convites?tamanhoPagina=200", token);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await CorpoAsync(resposta);
    }

    private static async Task<(HttpStatusCode Status, JsonElement Corpo)> ConsultarAsync(HttpClient http, string token)
    {
        using (http)
        {
            using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro/convite/consulta", UriKind.Relative), new { token });
            return (resposta.StatusCode, await CorpoAsync(resposta));
        }
    }

    private static async Task<string> ModoAsync(HttpClient http)
    {
        using (http)
        {
            using var resposta = await http.GetAsync(new Uri("/api/v1/auth/cadastro", UriKind.Relative));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await CorpoAsync(resposta)).GetProperty("modo").GetString()!;
        }
    }

    private static async Task<(HttpStatusCode Status, JsonElement Corpo)> RegistrarAsync(
        WebApplication app, string email, string? convite, string? tokenTurnstile = null, string? ip = null)
    {
        using var http = app.GetTestClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/auth/registro", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                nome = "Ana Souza", email, senha = Senha, nomeEmpresa = "Estúdio Traço", documento = DocumentosDeTeste.Novo(),
                versaoTermos = TextosAceitos.Termos, versaoPrivacidade = TextosAceitos.Privacidade, tokenTurnstile, convite,
            }),
        };
        if (ip is not null)
            requisicao.Headers.Add(CabecalhoIp, ip);
        using var resposta = await http.SendAsync(requisicao);
        return (resposta.StatusCode, await CorpoAsync(resposta));
    }

    private static async Task<HttpStatusCode> RegistrarStatusAsync(WebApplication app, string email, string convite)
    {
        var (status, _) = await RegistrarAsync(app, email, convite);
        return status;
    }

    private async Task<JsonElement> EuAsync(string accessToken)
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/auth/eu", UriKind.Relative));
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resposta = await http.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await CorpoAsync(resposta);
    }

    private async Task VencerAsync(Guid id)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        var vencimento = api.Relogio.Agora - TimeSpan.FromMinutes(1);
        (await db.Database.ExecuteSqlAsync($"UPDATE convites_empresa SET expira_em = {vencimento} WHERE id = {id}")).Should().Be(1);
    }

    private async Task<(int Usuarios, int Empresas, int Documentos, int ConvitesUsados)> ContagensAsync()
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        return (
            await db.Users.CountAsync(),
            await db.Tenants.IgnoreQueryFilters().CountAsync(),
            await db.Set<DocumentoTeste>().CountAsync(),
            await db.Set<ConviteEmpresa>().CountAsync(c => c.UsadoEm != null));
    }

    /// <summary>As ações da área sobre o convite, na trilha sem tenant (lidas fora dos filtros).</summary>
    private async Task<List<(string Acao, Guid? Usuario, string Dados)>> TrilhaAsync(Guid convite)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        var linhas = await db.Database.SqlQuery<LinhaTrilha>($"""
            SELECT acao, usuario_id, dados::text AS dados FROM auditoria
            WHERE tenant_id IS NULL AND alvo_id = {convite} AND alvo_tipo = 'convite' AND acao LIKE 'nebrys.convite.%'
            ORDER BY ocorrido_em
            """).ToListAsync();
        return linhas.Select(l => (l.Acao, l.UsuarioId, l.Dados ?? "")).ToList();
    }

    private static string Erro(JsonElement corpo, string campo) => corpo.GetProperty("errors").GetProperty(campo)[0].GetString()!;

    private static StringContent Json(object corpo) => new(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");

    private static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta)
    {
        var texto = await resposta.Content.ReadAsStringAsync();
        return texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone();
    }

    /// <summary>O IP da conexão vem de um cabeçalho de teste (o TestServer não tem conexão de verdade).</summary>
    private static void IpDoCabecalho(WebApplication app) => app.Use((http, proximo) =>
    {
        if (http.Request.Headers.TryGetValue(CabecalhoIp, out var ip))
            http.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
        return proximo(http);
    });

    private sealed class LinhaTrilha
    {
        public string Acao { get; set; } = "";

        public Guid? UsuarioId { get; set; }

        public string? Dados { get; set; }
    }

    /// <summary>O siteverify do Cloudflare, falso, sempre aprovando o token do cadastro.</summary>
    private sealed class SiteverifyAprovado : HttpMessageHandler
    {
        private int _chamadas;

        public int Chamadas => _chamadas;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _chamadas);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"success":true,"hostname":"acordu.app.br","action":"registro"}""", Encoding.UTF8, "application/json"),
            });
        }
    }
}
