using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Application.Cadastro;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14-79 a F14-83 e F14-114 a F14-121 (API): aceite dos termos de uso e da política de privacidade no
/// cadastro e no aceite de convite da equipe, "ciente" de versão nova e de quem não tem aceite, tudo atrás de
/// <c>TextosLegais__Publicados</c> (design D10 e D16). A fixture liga a configuração; os testes do estado desligado sobem
/// outra Api, sobre o mesmo banco, com ela desligada.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class TextosLegaisTests(ApiFixture api)
{
    private const string Erros = "https://acordu.app.br/erros/";
    private static readonly Dictionary<string, string?> Desligados = new() { ["TextosLegais:Publicados"] = "false" };

    private static string NovoEmail() => $"textos-{Guid.NewGuid():N}@exemplo.com";

    // ---- Publicados ----

    [Fact] // F14-80
    public async Task Registro_PublicadosSemAceite_400NoCampoENadaCriado()
    {
        var email = NovoEmail();
        using var http = api.NovoHttp();

        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), Corpo(email, null, null));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var erros = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        erros.GetProperty(AceiteDosTextos.CampoTermos)[0].GetString().Should().Be(AceiteDosTextos.AceiteObrigatorio);
        await NenhumaContaAsync(email);
    }

    [Theory] // F14-80 (só a privacidade falta) e D10 (versão que não é a vigente)
    [InlineData(TextosLegais.VersaoTermos, null, AceiteDosTextos.CampoPrivacidade)]
    [InlineData("0", TextosLegais.VersaoPrivacidade, AceiteDosTextos.CampoTermos)]
    public async Task Registro_PublicadosComAceiteIncompletoOuVencido_400NoCampoENadaCriado(
        string? termos, string? privacidade, string campo)
    {
        var email = NovoEmail();
        using var http = api.NovoHttp();

        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), Corpo(email, termos, privacidade));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty(campo, out _).Should().BeTrue();
        await NenhumaContaAsync(email);
    }

    [Fact] // F14-81
    public async Task Registro_PublicadosComAceite_GuardaVersaoDeCadaTextoEData()
    {
        var (token, _) = await RegistrarAsync(api.NovoHttp());

        var usuario = await UsuarioAsync(api.NovoHttp(), token);
        var aceites = await AceitesAsync(usuario);
        aceites.Select(a => (a.Texto, a.Versao, a.Forma)).Should().BeEquivalentTo(new[]
        {
            (TipoTextoLegal.Termos, TextosLegais.VersaoTermos, FormaAceiteTexto.Aceite),
            (TipoTextoLegal.Privacidade, TextosLegais.VersaoPrivacidade, FormaAceiteTexto.Aceite),
        });
        aceites.Should().OnlyContain(a => a.RegistradoEm == api.Relogio.Agora);
        (await EuAsync(api.NovoHttp(), token)).GetProperty("textosPendentes").GetArrayLength().Should().Be(0);
    }

    [Fact] // F14-82 (conta nova)
    public async Task AceiteConviteEquipe_PublicadosContaNova_ExigeEGravaOAceite()
    {
        var equipe = await Equipe.CriarAsync(api);
        var email = NovoEmail();
        var token = await ConvidarAsync(equipe, email);

        using (var semAceite = await PostAsync(api.NovoHttp(), "/api/v1/auth/convite", new { token, nome = "Bruno", senha = Equipe.Senha }))
        {
            semAceite.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Equipe.JsonAsync(semAceite)).GetProperty("errors").TryGetProperty(AceiteDosTextos.CampoTermos, out _).Should().BeTrue();
        }

        await using (var db = api.DaEquipe(equipe.TenantId))
            (await db.Vinculos.CountAsync()).Should().Be(1, "só o Dono: a recusa não cria vínculo");

        var sessao = await AceitarConviteAsync(api.NovoHttp(), token, "Bruno", TextosLegais.VersaoTermos, TextosLegais.VersaoPrivacidade);
        var aceites = await AceitesAsync(await UsuarioAsync(api.NovoHttp(), sessao));
        aceites.Select(a => a.Texto).Should().BeEquivalentTo([TipoTextoLegal.Termos, TipoTextoLegal.Privacidade]);
        aceites.Should().OnlyContain(a => a.Forma == FormaAceiteTexto.Aceite && a.RegistradoEm == api.Relogio.Agora);
    }

    [Fact] // F14-82 (conta existente que não aceitou a versão vigente)
    public async Task AceiteConviteEquipe_PublicadosContaExistenteSemAceite_GravaOAceite()
    {
        var equipe = await Equipe.CriarAsync(api);
        string email;
        await using (var desligada = await api.NovaApiAsync(Desligados))
            (_, email) = await RegistrarAsync(desligada.GetTestClient(), aceite: false);
        var token = await ConvidarAsync(equipe, email);

        var sessao = await AceitarConviteAsync(api.NovoHttp(), token, null, TextosLegais.VersaoTermos, TextosLegais.VersaoPrivacidade);

        var usuario = await UsuarioAsync(api.NovoHttp(), sessao);
        (await AceitesAsync(usuario)).Should().HaveCount(2).And.OnlyContain(a => a.Forma == FormaAceiteTexto.Aceite);
        (await EuAsync(api.NovoHttp(), sessao)).GetProperty("textosPendentes").GetArrayLength().Should().Be(0);
    }

    [Fact] // F14-82 (conta existente que já tem a versão vigente: nada duplica)
    public async Task AceiteConviteEquipe_PublicadosContaQueJaAceitou_NaoDuplica()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (_, email) = await RegistrarAsync(api.NovoHttp());
        var token = await ConvidarAsync(equipe, email);

        var sessao = await AceitarConviteAsync(api.NovoHttp(), token, null, TextosLegais.VersaoTermos, TextosLegais.VersaoPrivacidade);

        (await AceitesAsync(await UsuarioAsync(api.NovoHttp(), sessao))).Should().HaveCount(2);
    }

    [Fact] // F14-83
    public async Task Ciente_VersaoNovaPublicada_AvisaAteOCienteSemBloquear()
    {
        var (token, _) = await RegistrarAsync(api.NovoHttp());
        var usuario = await UsuarioAsync(api.NovoHttp(), token);
        // A conta aceitou a versão anterior dos termos: a vigente (a constante) é "nova" para ela.
        await TrocarVersaoAceitaAsync(usuario, TipoTextoLegal.Termos, "0");

        var pendentes = (await EuAsync(api.NovoHttp(), token)).GetProperty("textosPendentes");
        pendentes.GetArrayLength().Should().Be(1);
        pendentes[0].GetProperty("texto").GetString().Should().Be("TERMOS");
        pendentes[0].GetProperty("versao").GetString().Should().Be(TextosLegais.VersaoTermos);
        // Uso não bloqueado.
        using (var leitura = await EnviarAsync(api.NovoHttp(), HttpMethod.Get, "/api/v1/clientes", token))
            leitura.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var gravacao = await EnviarAsync(api.NovoHttp(), HttpMethod.Post, "/api/v1/clientes", token, new { nome = "Padaria" }))
            gravacao.StatusCode.Should().Be(HttpStatusCode.Created);

        api.Relogio.Agora = api.Relogio.Agora.AddMinutes(5);
        using (var ciente = await EnviarAsync(api.NovoHttp(), HttpMethod.Post, "/api/v1/auth/textos/ciente", token))
            ciente.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await EuAsync(api.NovoHttp(), token)).GetProperty("textosPendentes").GetArrayLength().Should().Be(0);
        var aceites = await AceitesAsync(usuario);
        aceites.Should().ContainSingle(a => a.Texto == TipoTextoLegal.Termos && a.Versao == "0", "a versão anterior fica no histórico");
        aceites.Should().ContainSingle(a =>
            a.Texto == TipoTextoLegal.Termos && a.Versao == TextosLegais.VersaoTermos
            && a.Forma == FormaAceiteTexto.Ciente && a.RegistradoEm == api.Relogio.Agora);
        aceites.Should().ContainSingle(a => a.Texto == TipoTextoLegal.Privacidade && a.Forma == FormaAceiteTexto.Aceite);

        // De novo, nada pendente: nada a gravar.
        using (var deNovo = await EnviarAsync(api.NovoHttp(), HttpMethod.Post, "/api/v1/auth/textos/ciente", token))
            deNovo.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AceitesAsync(usuario)).Should().HaveCount(3);
    }

    [Fact] // F14-83 com a empresa só leitura: "ciente" é da pessoa (decisão 10 do G2)
    public async Task Ciente_EmpresaSomenteLeitura_Grava()
    {
        var equipe = await Equipe.CriarAsync(api);
        var usuario = await UsuarioAsync(api.NovoHttp(), equipe.Dono);
        await TrocarVersaoAceitaAsync(usuario, TipoTextoLegal.Privacidade, "0");
        await using (var db = api.DaEquipe(equipe.TenantId))
        {
            await db.Database.ExecuteSqlAsync(
                $"UPDATE tenants SET situacao = 'VENCIDA', somente_leitura_desde = {api.Relogio.Agora.ToUniversalTime()} WHERE id = {equipe.TenantId}");
        }

        using var ciente = await EnviarAsync(api.NovoHttp(), HttpMethod.Post, "/api/v1/auth/textos/ciente", equipe.Dono);

        ciente.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AceitesAsync(usuario)).Should().Contain(a => a.Texto == TipoTextoLegal.Privacidade && a.Forma == FormaAceiteTexto.Ciente);
    }

    [Fact] // F14-83: sem sessão, a rota não grava
    public async Task Ciente_SemSessao_401()
    {
        using var resposta = await EnviarAsync(api.NovoHttp(), HttpMethod.Post, "/api/v1/auth/textos/ciente", null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- Não publicados ----

    [Fact] // F14-115
    public async Task Registro_NaoPublicadosSemAceite_CriaSemAceite()
    {
        await using var desligada = await api.NovaApiAsync(Desligados);

        var (token, _) = await RegistrarAsync(desligada.GetTestClient(), aceite: false);

        (await AceitesAsync(await UsuarioAsync(desligada.GetTestClient(), token))).Should().BeEmpty();
    }

    [Fact] // F14-116
    public async Task Registro_NaoPublicadosComAceite_IgnoraEOCadastroSegue()
    {
        await using var desligada = await api.NovaApiAsync(Desligados);
        using var http = desligada.GetTestClient();

        // Até uma versão que não é a vigente: nada é conferido.
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), Corpo(NovoEmail(), "0", "1"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var token = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        (await AceitesAsync(await UsuarioAsync(desligada.GetTestClient(), token))).Should().BeEmpty();
    }

    [Fact] // F14-116: as demais regras do cadastro seguem
    public async Task Registro_NaoPublicadosComOutroCampoInvalido_RecusaPeloCampo()
    {
        await using var desligada = await api.NovaApiAsync(Desligados);
        using var http = desligada.GetTestClient();

        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), new
        {
            nome = "Ana", email = NovoEmail(), senha = "curta", nomeEmpresa = "Estúdio", documento = DocumentosDeTeste.Novo(),
            versaoTermos = TextosLegais.VersaoTermos, versaoPrivacidade = TextosLegais.VersaoPrivacidade,
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var erros = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        erros.TryGetProperty("senha", out _).Should().BeTrue();
        erros.TryGetProperty(AceiteDosTextos.CampoTermos, out _).Should().BeFalse();
    }

    [Theory] // F14-117 (conta nova, com e sem o aceite no corpo)
    [InlineData(false)]
    [InlineData(true)]
    public async Task AceiteConviteEquipe_NaoPublicados_CriaVinculoSemAceite(bool comAceite)
    {
        var equipe = await Equipe.CriarAsync(api);
        var email = NovoEmail();
        var token = await ConvidarAsync(equipe, email);
        await using var desligada = await api.NovaApiAsync(Desligados);

        var sessao = comAceite
            ? await AceitarConviteAsync(desligada.GetTestClient(), token, "Bruno", TextosLegais.VersaoTermos, TextosLegais.VersaoPrivacidade)
            : await AceitarConviteAsync(desligada.GetTestClient(), token, "Bruno", null, null);

        await using (var db = api.DaEquipe(equipe.TenantId))
            (await db.Vinculos.CountAsync()).Should().Be(2);
        (await AceitesAsync(await UsuarioAsync(desligada.GetTestClient(), sessao))).Should().BeEmpty();
    }

    [Fact] // F14-117 (conta existente)
    public async Task AceiteConviteEquipe_NaoPublicadosContaExistente_NaoGravaAceite()
    {
        var equipe = await Equipe.CriarAsync(api);
        await using var desligada = await api.NovaApiAsync(Desligados);
        var (_, email) = await RegistrarAsync(desligada.GetTestClient(), aceite: false);
        var token = await ConvidarAsync(equipe, email);

        var sessao = await AceitarConviteAsync(desligada.GetTestClient(), token, null, null, null);

        (await AceitesAsync(await UsuarioAsync(desligada.GetTestClient(), sessao))).Should().BeEmpty();
    }

    [Fact] // F14-118
    public async Task EuECiente_NaoPublicados_SemPendentesECiente404SemGravar()
    {
        await using var desligada = await api.NovaApiAsync(Desligados);
        var (token, _) = await RegistrarAsync(desligada.GetTestClient(), aceite: false);
        var usuario = await UsuarioAsync(desligada.GetTestClient(), token);

        (await EuAsync(desligada.GetTestClient(), token)).GetProperty("textosPendentes").GetArrayLength().Should().Be(0);
        using var ciente = await EnviarAsync(desligada.GetTestClient(), HttpMethod.Post, "/api/v1/auth/textos/ciente", token);

        ciente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ciente.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString().Should().Be(Erros + "nao-encontrado");
        (await AceitesAsync(usuario)).Should().BeEmpty();
    }

    // ---- Virada ----

    [Fact] // F14-119 (conta criada com os textos escondidos)
    public async Task Eu_ContaSemAceiteETextosPublicados_AvisaAteOCienteComAVersaoVigente()
    {
        string token;
        await using (var desligada = await api.NovaApiAsync(Desligados))
            (token, _) = await RegistrarAsync(desligada.GetTestClient(), aceite: false);

        // Os textos foram publicados (a Api da fixture está ligada) e a pessoa entra.
        var pendentes = (await EuAsync(api.NovoHttp(), token)).GetProperty("textosPendentes");
        pendentes.EnumerateArray().Select(p => (p.GetProperty("texto").GetString(), p.GetProperty("versao").GetString()))
            .Should().Equal(("TERMOS", TextosLegais.VersaoTermos), ("PRIVACIDADE", TextosLegais.VersaoPrivacidade));
        using (var leitura = await EnviarAsync(api.NovoHttp(), HttpMethod.Get, "/api/v1/clientes", token))
            leitura.StatusCode.Should().Be(HttpStatusCode.OK, "o uso não é bloqueado");

        using (var ciente = await EnviarAsync(api.NovoHttp(), HttpMethod.Post, "/api/v1/auth/textos/ciente", token))
            ciente.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var aceites = await AceitesAsync(await UsuarioAsync(api.NovoHttp(), token));
        aceites.Select(a => (a.Texto, a.Versao)).Should().BeEquivalentTo(new[]
        {
            (TipoTextoLegal.Termos, TextosLegais.VersaoTermos),
            (TipoTextoLegal.Privacidade, TextosLegais.VersaoPrivacidade),
        });
        aceites.Should().OnlyContain(a => a.Forma == FormaAceiteTexto.Ciente && a.RegistradoEm == api.Relogio.Agora);
        (await EuAsync(api.NovoHttp(), token)).GetProperty("textosPendentes").GetArrayLength().Should().Be(0);
    }

    [Fact] // F14-119 (pessoa que entrou por convite da equipe com os textos escondidos)
    public async Task Eu_EntrouPorConviteSemAceiteETextosPublicados_Avisa()
    {
        var equipe = await Equipe.CriarAsync(api);
        var token = await ConvidarAsync(equipe, NovoEmail());
        string sessao;
        await using (var desligada = await api.NovaApiAsync(Desligados))
            sessao = await AceitarConviteAsync(desligada.GetTestClient(), token, "Bruno", null, null);

        (await EuAsync(api.NovoHttp(), sessao)).GetProperty("textosPendentes").GetArrayLength().Should().Be(2);
    }

    // ---- Portal ----

    [Fact] // F14-120
    public async Task Portal_TextosPublicados_ContatoSemPendentesNemAceiteECiente403()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);

        // Aceite de convite do portal com os textos publicados: nada de aceite gravado.
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        var usuario = await UsuarioAsync(api.NovoHttp(), contato.Token);

        (await AceitesAsync(usuario)).Should().BeEmpty();
        (await EuAsync(api.NovoHttp(), contato.Token)).GetProperty("textosPendentes").GetArrayLength().Should().Be(0);
        using (var portalEu = await EnviarAsync(api.NovoHttp(), HttpMethod.Get, "/api/v1/portal/eu", contato.Token))
        {
            portalEu.StatusCode.Should().Be(HttpStatusCode.OK);
            (await portalEu.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("textosPendentes", out _).Should().BeFalse();
        }

        using var ciente = await EnviarAsync(api.NovoHttp(), HttpMethod.Post, "/api/v1/auth/textos/ciente", contato.Token);
        ciente.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AceitesAsync(usuario)).Should().BeEmpty();
    }

    [Fact] // F14-120 (contato por link)
    public async Task Ciente_SessaoDeLink_403()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = Cenarios.Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos", new { nome = "Marta", email = NovoEmail() }));
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);
        var emitido = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal", new { contatoId = contato });
        var tokenDoLink = emitido.GetProperty("link").GetString()!.Split('/')[^1];
        using var http = api.NovoHttp();
        using var troca = await PostAsync(http, "/api/v1/auth/portal/link", new { token = tokenDoLink });
        troca.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenLink = (await troca.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        using var ciente = await EnviarAsync(api.NovoHttp(), HttpMethod.Post, "/api/v1/auth/textos/ciente", tokenLink);

        ciente.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // F14-120 (textos não publicados): nada gravado, sem pendentes, "ciente" recusado
    public async Task Portal_TextosNaoPublicados_ContatoSemAceiteSemPendentesECienteRecusado()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var email = $"contato-{Guid.NewGuid():N}@bomgrao.com.br";
        var contato = Cenarios.Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos", new { nome = "Marina", email }));
        await equipe.EsperarAsync(
            HttpStatusCode.Accepted, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos/{contato}/acesso-portal",
            new { escopoPortal = "TODAS_DO_CLIENTE" });
        await using var desligada = await api.NovaApiAsync(Desligados);

        using var convite = await PostAsync(desligada.GetTestClient(), "/api/v1/auth/portal/convite", new
        {
            token = api.Emails.TokenDoConvite(email), senha = ContatosDoPortal.Senha,
            versaoTermos = TextosLegais.VersaoTermos, versaoPrivacidade = TextosLegais.VersaoPrivacidade,
        });
        convite.StatusCode.Should().Be(HttpStatusCode.Created);
        var token = (await convite.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var usuario = await UsuarioAsync(desligada.GetTestClient(), token);

        (await EuAsync(desligada.GetTestClient(), token)).GetProperty("textosPendentes").GetArrayLength().Should().Be(0);
        using var ciente = await EnviarAsync(desligada.GetTestClient(), HttpMethod.Post, "/api/v1/auth/textos/ciente", token);
        ciente.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        (await AceitesAsync(usuario)).Should().BeEmpty();
    }

    // ---- Bordas ----

    [Fact] // D10: versão que não é a vigente no convite da equipe → 400 e nada criado
    public async Task AceiteConviteEquipe_PublicadosComVersaoVencida_400ENadaCriado()
    {
        var equipe = await Equipe.CriarAsync(api);
        var email = NovoEmail();
        var token = await ConvidarAsync(equipe, email);

        using (var resposta = await PostAsync(api.NovoHttp(), "/api/v1/auth/convite", new
        {
            token, nome = "Bruno", senha = Equipe.Senha, versaoTermos = TextosLegais.VersaoTermos, versaoPrivacidade = "0",
        }))
        {
            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Equipe.JsonAsync(resposta)).GetProperty("errors").TryGetProperty(AceiteDosTextos.CampoPrivacidade, out _).Should().BeTrue();
        }

        await using var db = api.DaEquipe(equipe.TenantId);
        (await db.Vinculos.CountAsync()).Should().Be(1, "só o Dono");
        (await db.Users.CountAsync(u => u.Email == email)).Should().Be(0);
    }

    [Fact] // Isolamento: o "ciente" é da conta; outra conta continua com o aviso
    public async Task Ciente_DeUmaConta_NaoAfetaOutra()
    {
        string tokenA, tokenB;
        await using (var desligada = await api.NovaApiAsync(Desligados))
        {
            (tokenA, _) = await RegistrarAsync(desligada.GetTestClient(), aceite: false);
            (tokenB, _) = await RegistrarAsync(desligada.GetTestClient(), aceite: false);
        }
        var usuarioB = await UsuarioAsync(api.NovoHttp(), tokenB);

        using (var ciente = await EnviarAsync(api.NovoHttp(), HttpMethod.Post, "/api/v1/auth/textos/ciente", tokenA))
            ciente.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await EuAsync(api.NovoHttp(), tokenA)).GetProperty("textosPendentes").GetArrayLength().Should().Be(0);
        (await EuAsync(api.NovoHttp(), tokenB)).GetProperty("textosPendentes").GetArrayLength().Should().Be(2);
        (await AceitesAsync(usuarioB)).Should().BeEmpty();
    }

    // ---- Configuração (F14-121, lado da API) ----

    [Theory] // F14-121: valor fora de true/false não sobe (o deploy.yml já barra antes)
    [InlineData("1")]
    [InlineData("sim")]
    public async Task Composicao_TextosLegaisPublicadosInvalida_NaoSobe(string valor)
    {
        var acao = async () =>
        {
            await using var app = await api.NovaApiAsync(new Dictionary<string, string?> { ["TextosLegais:Publicados"] = valor });
        };

        await acao.Should().ThrowAsync<InvalidOperationException>().WithMessage("*TextosLegais__Publicados*");
    }

    [Fact] // F14-121 e D16: sem a configuração, desligada (padrão seguro)
    public async Task Composicao_SemTextosLegaisPublicados_Desligada()
    {
        await using var semValor = await api.NovaApiAsync(new Dictionary<string, string?> { ["TextosLegais:Publicados"] = null });

        var (token, _) = await RegistrarAsync(semValor.GetTestClient(), aceite: false);

        (await EuAsync(semValor.GetTestClient(), token)).GetProperty("textosPendentes").GetArrayLength().Should().Be(0);
    }

    // ---- Apoio ----

    private static object Corpo(string email, string? termos, string? privacidade) => new
    {
        nome = "Ana Souza",
        email,
        senha = Equipe.Senha,
        nomeEmpresa = "Estúdio Traço",
        documento = DocumentosDeTeste.Novo(),
        versaoTermos = termos,
        versaoPrivacidade = privacidade,
    };

    private static async Task<(string Token, string Email)> RegistrarAsync(HttpClient http, bool aceite = true)
    {
        using (http)
        {
            var email = NovoEmail();
            using var resposta = await http.PostAsJsonAsync(
                new Uri("/api/v1/auth/registro", UriKind.Relative),
                aceite ? Corpo(email, TextosLegais.VersaoTermos, TextosLegais.VersaoPrivacidade) : Corpo(email, null, null));
            var texto = await resposta.Content.ReadAsStringAsync();
            resposta.StatusCode.Should().Be(HttpStatusCode.Created, "corpo: {0}", texto);
            return (JsonDocument.Parse(texto).RootElement.GetProperty("accessToken").GetString()!, email);
        }
    }

    private async Task<string> ConvidarAsync(Equipe equipe, string email)
    {
        Guid membro;
        await using (var db = api.DaEquipe(equipe.TenantId))
            membro = (await db.Papeis.ToListAsync()).Single(p => p.Nome == Papel.NomeMembro).Id;
        await equipe.EsperarAsync(HttpStatusCode.Accepted, HttpMethod.Post, "/api/v1/convites", new { email, papelId = membro });
        return api.Emails.TokenDoConviteEquipe(email);
    }

    private static async Task<string> AceitarConviteAsync(
        HttpClient http, string token, string? nome, string? termos, string? privacidade)
    {
        using (http)
        {
            using var resposta = await PostAsync(http, "/api/v1/auth/convite", new
            {
                token, nome, senha = Equipe.Senha, versaoTermos = termos, versaoPrivacidade = privacidade,
            });
            var texto = await resposta.Content.ReadAsStringAsync();
            resposta.StatusCode.Should().Be(HttpStatusCode.Created, "corpo: {0}", texto);
            return JsonDocument.Parse(texto).RootElement.GetProperty("accessToken").GetString()!;
        }
    }

    private static async Task<JsonElement> EuAsync(HttpClient http, string token)
    {
        using (http)
        {
            using var resposta = await EnviarAsync(http, HttpMethod.Get, "/api/v1/auth/eu", token);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        }
    }

    private static async Task<Guid> UsuarioAsync(HttpClient http, string token) =>
        (await EuAsync(http, token)).GetProperty("usuarioId").GetGuid();

    private static Task<HttpResponseMessage> PostAsync(HttpClient http, string rota, object corpo) =>
        http.PostAsJsonAsync(new Uri(rota, UriKind.Relative), corpo);

    private static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient http, HttpMethod metodo, string rota, string? token, object? corpo = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, new Uri(rota, UriKind.Relative));
        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);
        if (token is not null)
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(requisicao);
    }

    /// <summary>Contexto de um tenant qualquer: a tabela dos aceites é global.</summary>
    private async Task<List<AceiteTexto>> AceitesAsync(Guid usuario)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        return await db.Set<AceiteTexto>().AsNoTracking().Where(a => a.UsuarioId == usuario).ToListAsync();
    }

    /// <summary>Simula a conta que aceitou uma versão anterior do texto (a constante é a "nova").</summary>
    private async Task TrocarVersaoAceitaAsync(Guid usuario, TipoTextoLegal texto, string anterior)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        await db.Set<AceiteTexto>().Where(a => a.UsuarioId == usuario && a.Texto == texto).ExecuteDeleteAsync();
        db.Add(AceiteTexto.Registrar(usuario, texto, anterior, FormaAceiteTexto.Aceite, api.Relogio.Agora.AddYears(-1)));
        await db.SaveChangesAsync();
    }

    private async Task NenhumaContaAsync(string email)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        (await db.Users.CountAsync(u => u.Email == email)).Should().Be(0);
        (await db.Set<AceiteTexto>().CountAsync(a => !db.Users.Any(u => u.Id == a.UsuarioId))).Should().Be(0);
    }
}
