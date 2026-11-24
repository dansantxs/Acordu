using System.Net;
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
/// Spec do F14, F14o (F14-52 a F14-55, F14-75 a F14-77; design D12 e D14): a lista de espera pública, com o Turnstile
/// (ação <c>lista-espera</c>) e o limite por IP, e a lista na área da Nebrys, com o convite a partir da entrada e o
/// descarte. A fixture sobe o Turnstile desligado; os cenários dele sobem outra Api com o siteverify falso.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class ListaEsperaTests(ApiFixture api)
{
    private const string Rota = "/api/v1/lista-espera";
    private const string Area = "/api/v1/nebrys";
    private const string CabecalhoIp = "X-Teste-Ip";

    // ---------- F14-52: entrada gravada fora dos tenants ----------

    [Fact]
    public async Task Inscrever_TurnstileAprovado_202EEntradaGravadaComAData() // F14-52
    {
        using var siteverify = new SiteverifyFalso(Aprovado("lista-espera"));
        await using var app = await ApiComTurnstileAsync(siteverify);
        var email = NovoEmail();
        var empresasAntes = await EmpresasAsync();

        using var resposta = await EnviarAsync(app, Corpo(email, tokenTurnstile: "token-bom"), ip: "203.0.113.20");

        var corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.Accepted, "corpo: {0}", corpo);
        corpo.Should().BeEmpty("a resposta não diz nada sobre a lista");
        siteverify.Corpos.Should().ContainSingle().Which.Should()
            .Contain("secret=segredo-dos-testes").And.Contain("response=token-bom").And.Contain("remoteip=203.0.113.20");
        var entrada = (await EntradasAsync(email)).Should().ContainSingle().Subject;
        entrada.Nome.Should().Be("Ana Souza");
        entrada.NomeEmpresa.Should().Be("Estúdio Traço");
        entrada.Mensagem.Should().Be("Organizar o atendimento dos clientes.");
        entrada.CriadaEm.Should().Be(api.Relogio.Agora);
        entrada.ConvidadaEm.Should().BeNull();
        (await EmpresasAsync()).Should().Be(empresasAntes, "a entrada é de fora dos tenants e não cria empresa");
        api.Emails.Para(email).Should().BeEmpty("a lista de espera não manda e-mail");
    }

    [Fact] // F14-52: só os campos obrigatórios, sem mensagem; o Turnstile desligado ignora o token
    public async Task Inscrever_SemMensagem_202EMensagemNula()
    {
        var email = NovoEmail();

        var (status, _) = await InscreverAsync(api.App, new { nome = "Bia", email, nomeEmpresa = "Ateliê Bia" });

        status.Should().Be(HttpStatusCode.Accepted);
        (await EntradasAsync(email)).Should().ContainSingle().Which.Mensagem.Should().BeNull();
    }

    [Fact] // F14-52: a Bearer de outra sessão enviada junto não muda nada (rota pública)
    public async Task Inscrever_ComTokenDeEquipe_IgnoraOTokenEGrava()
    {
        var equipe = await Equipe.CriarAsync(api);
        var email = NovoEmail();
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri(Rota, UriKind.Relative)) { Content = JsonContent.Create(Corpo(email)) };
        requisicao.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", equipe.Dono);

        using var resposta = await http.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await EntradasAsync(email)).Should().ContainSingle();
    }

    // ---------- F14-53: não duplica e responde igual ----------

    [Fact]
    public async Task Inscrever_EmailQueJaEstaNaLista_MesmaRespostaSemDuplicarNemMudar() // F14-53
    {
        var email = NovoEmail();
        using var primeira = await EnviarAsync(api.App, Corpo(email));
        var corpoPrimeira = await primeira.Content.ReadAsStringAsync();

        using var segunda = await EnviarAsync(
            api.App, new { nome = "Outra Pessoa", email = $"  {email.ToUpperInvariant()} ", nomeEmpresa = "Outra Empresa", mensagem = "outra" });
        var corpoSegunda = await segunda.Content.ReadAsStringAsync();

        primeira.StatusCode.Should().Be(HttpStatusCode.Accepted);
        segunda.StatusCode.Should().Be(primeira.StatusCode, "a resposta não revela quem está na lista");
        corpoSegunda.Should().Be(corpoPrimeira);
        segunda.Content.Headers.ContentType.Should().Be(primeira.Content.Headers.ContentType);
        segunda.Headers.Location.Should().BeNull();
        var entrada = (await EntradasAsync(email)).Should().ContainSingle().Subject;
        entrada.Nome.Should().Be("Ana Souza", "a entrada que já estava não muda");
        entrada.NomeEmpresa.Should().Be("Estúdio Traço");
    }

    [Fact] // F14-53: vários envios ao mesmo tempo com o mesmo e-mail: o índice único decide, sem 500
    public async Task Inscrever_VariosAoMesmoTempoComOMesmoEmail_TodosIguaisEUmaEntradaSo()
    {
        var email = NovoEmail();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => InscreverAsync(api.App, Corpo(email))));

        respostas.Should().AllSatisfy(r => r.Status.Should().Be(HttpStatusCode.Accepted));
        (await EntradasAsync(email)).Should().ContainSingle();
    }

    // ---------- F14-54: Turnstile e limite por IP ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Inscrever_SemTokenDoTurnstile_400NoTokenSemChamarOCloudflareNemGravar(string? token) // F14-54
    {
        using var siteverify = new SiteverifyFalso(Aprovado("lista-espera"));
        await using var app = await ApiComTurnstileAsync(siteverify);
        var email = NovoEmail();

        var (status, corpo) = await InscreverAsync(app, Corpo(email, tokenTurnstile: token));

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo, "tokenTurnstile").Should().Contain("robô");
        siteverify.Corpos.Should().BeEmpty();
        (await EntradasAsync(email)).Should().BeEmpty();
    }

    [Theory] // F14-54: o token do cadastro (outra ação), de outro site ou recusado não vale aqui
    [InlineData("""{"success":true,"hostname":"acordu.app.br","action":"registro"}""")]
    [InlineData("""{"success":true,"hostname":"outro-site.com","action":"lista-espera"}""")]
    [InlineData("""{"success":false,"error-codes":["invalid-input-response"]}""")]
    public async Task Inscrever_TokenRecusadoOuDeOutraAcao_400NoTokenSemGravar(string respostaDoCloudflare) // F14-54
    {
        using var siteverify = new SiteverifyFalso(respostaDoCloudflare);
        await using var app = await ApiComTurnstileAsync(siteverify);
        var email = NovoEmail();

        var (status, corpo) = await InscreverAsync(app, Corpo(email, tokenTurnstile: "token-de-fora"));

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo, "tokenTurnstile").Should().Contain("robô");
        (await EntradasAsync(email)).Should().BeEmpty();
    }

    [Fact] // F14-54: Cloudflare fora do ar recusa (fail-closed), como no cadastro
    public async Task Inscrever_CloudflareForaDoAr_400PedindoNovaTentativaSemGravar()
    {
        using var siteverify = new SiteverifyFalso(_ => throw new HttpRequestException("fora do ar"));
        await using var app = await ApiComTurnstileAsync(siteverify);
        var email = NovoEmail();

        var (status, corpo) = await InscreverAsync(app, Corpo(email, tokenTurnstile: "token-bom"));

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo, "tokenTurnstile").Should().Contain("Tente de novo");
        (await EntradasAsync(email)).Should().BeEmpty();
    }

    [Fact] // F14-54: a ação da lista não vale no cadastro (cada rota com a sua)
    public async Task Registro_TokenDaListaDeEspera_400NoToken()
    {
        using var siteverify = new SiteverifyFalso(Aprovado("lista-espera"));
        await using var app = await ApiComTurnstileAsync(siteverify);
        using var http = app.GetTestClient();

        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), new
        {
            nome = "Ana Souza", email = NovoEmail(), senha = "senha-bem-comprida", nomeEmpresa = "Estúdio Traço",
            documento = DocumentosDeTeste.Novo(), versaoTermos = TextosAceitos.Termos, versaoPrivacidade = TextosAceitos.Privacidade,
            tokenTurnstile = "token-da-lista",
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Erro(await CorpoAsync(resposta), "tokenTurnstile").Should().Contain("robô");
    }

    [Fact]
    public async Task Inscrever_QuartoDoMesmoIpNaHora_429_OutroIpSegue() // F14-54
    {
        await using var app = await ApiComLimiteAsync();
        const string Ip = "198.51.100.60";

        for (var i = 0; i < 3; i++)
            (await InscreverAsync(app, Corpo(NovoEmail()), ip: Ip)).Status.Should().Be(HttpStatusCode.Accepted);

        var email = NovoEmail();
        (await InscreverAsync(app, Corpo(email), ip: Ip)).Status.Should().Be(HttpStatusCode.TooManyRequests);
        (await EntradasAsync(email)).Should().BeEmpty();
        (await InscreverAsync(app, Corpo(NovoEmail()), ip: "198.51.100.61")).Status.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact] // F14-54: as tentativas recusadas também contam, como no cadastro
    public async Task Inscrever_TentativasRecusadas_ContamNoLimite()
    {
        await using var app = await ApiComLimiteAsync();
        const string Ip = "198.51.100.62";

        for (var i = 0; i < 3; i++)
            (await InscreverAsync(app, new { nome = "", email = NovoEmail(), nomeEmpresa = "X" }, ip: Ip)).Status.Should().Be(HttpStatusCode.BadRequest);

        (await InscreverAsync(app, Corpo(NovoEmail()), ip: Ip)).Status.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact] // F14-54: cota própria, separada da do cadastro
    public async Task Inscrever_LimiteDaListaEsgotado_CadastroSegue()
    {
        await using var app = await ApiComLimiteAsync();
        const string Ip = "198.51.100.63";
        for (var i = 0; i < 4; i++)
            await InscreverAsync(app, Corpo(NovoEmail()), ip: Ip);

        using var http = app.GetTestClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/auth/registro", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                nome = "Ana Souza", email = NovoEmail(), senha = "senha-bem-comprida", nomeEmpresa = "Estúdio Traço",
                documento = DocumentosDeTeste.Novo(), versaoTermos = TextosAceitos.Termos, versaoPrivacidade = TextosAceitos.Privacidade,
            }),
        };
        requisicao.Headers.Add(CabecalhoIp, Ip);
        using var registro = await http.SendAsync(requisicao);

        registro.StatusCode.Should().Be(HttpStatusCode.Created, "o limite da lista não vale para o cadastro");
    }

    // ---------- F14-55: campo obrigatório ou e-mail inválido ----------

    public static TheoryData<string, string> CorposInvalidos => new()
    {
        { """{"email":"x@exemplo.com","nomeEmpresa":"Estúdio"}""", "nome" },
        { """{"nome":"   ","email":"x@exemplo.com","nomeEmpresa":"Estúdio"}""", "nome" },
        { """{"nome":"Ana","nomeEmpresa":"Estúdio"}""", "email" },
        { """{"nome":"Ana","email":"","nomeEmpresa":"Estúdio"}""", "email" },
        { """{"nome":"Ana","email":"sem-arroba","nomeEmpresa":"Estúdio"}""", "email" },
        { """{"nome":"Ana","email":"x@exemplo.com"}""", "nomeEmpresa" },
        { """{"nome":"Ana","email":"x@exemplo.com","nomeEmpresa":""}""", "nomeEmpresa" },
    };

    [Theory]
    [MemberData(nameof(CorposInvalidos))]
    public async Task Inscrever_CampoObrigatorioFaltandoOuEmailInvalido_400NoCampoENadaGravado(string modelo, string campo) // F14-55
    {
        var email = NovoEmail();
        var json = modelo.Replace("x@exemplo.com", email, StringComparison.Ordinal);
        var antes = await TotalDeEntradasAsync();
        using var http = api.NovoHttp();

        using var resposta = await http.PostAsync(new Uri(Rota, UriKind.Relative), new StringContent(json, Encoding.UTF8, "application/json"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var corpo = await CorpoAsync(resposta);
        corpo.GetProperty("type").GetString().Should().EndWith("/validacao");
        Erro(corpo, campo).Should().NotBeNullOrWhiteSpace();
        (await EntradasAsync(email)).Should().BeEmpty();
        (await TotalDeEntradasAsync()).Should().Be(antes, "nada gravado");
    }

    [Fact] // F14-55: mensagem acima do limite também é recusada no campo
    public async Task Inscrever_MensagemLongaDemais_400NaMensagem()
    {
        var email = NovoEmail();

        var (status, corpo) = await InscreverAsync(api.App, new { nome = "Ana", email, nomeEmpresa = "Estúdio", mensagem = new string('a', 1001) });

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo, "mensagem").Should().NotBeNullOrWhiteSpace();
        (await EntradasAsync(email)).Should().BeEmpty();
    }

    // ---------- F14-75: a lista na área ----------

    [Fact]
    public async Task Lista_Operador_PorOrdemDeChegadaComASituacao() // F14-75
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var depois = NovoEmail();
        var antes = NovoEmail();
        var original = api.Relogio.Agora;
        try
        {
            // Gravadas fora de ordem: a lista segue a data de chegada.
            api.Relogio.Agora = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
            (await InscreverAsync(api.App, Corpo(depois))).Status.Should().Be(HttpStatusCode.Accepted);
            api.Relogio.Agora = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
            (await InscreverAsync(api.App, Corpo(antes))).Status.Should().Be(HttpStatusCode.Accepted);
        }
        finally
        {
            api.Relogio.Agora = original;
        }

        var idConvidada = (await EntradasAsync(depois)).Single().Id;
        (await ConvidarAsync(token, idConvidada)).Status.Should().Be(HttpStatusCode.Created);

        var lista = await ListaAsync(token);

        var itens = lista.GetProperty("itens").EnumerateArray().ToList();
        var posicaoAntes = itens.FindIndex(i => i.GetProperty("email").GetString() == antes);
        var posicaoDepois = itens.FindIndex(i => i.GetProperty("email").GetString() == depois);
        posicaoAntes.Should().BeGreaterThanOrEqualTo(0);
        posicaoDepois.Should().Be(posicaoAntes + 1, "a que chegou antes vem antes");
        var aguardando = itens[posicaoAntes];
        aguardando.GetProperty("situacao").GetString().Should().Be("AGUARDANDO");
        aguardando.GetProperty("nome").GetString().Should().Be("Ana Souza");
        aguardando.GetProperty("nomeEmpresa").GetString().Should().Be("Estúdio Traço");
        aguardando.GetProperty("mensagem").GetString().Should().Be("Organizar o atendimento dos clientes.");
        aguardando.GetProperty("criadaEm").GetDateTimeOffset().Should().Be(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));
        aguardando.GetProperty("convidadaEm").ValueKind.Should().Be(JsonValueKind.Null);
        aguardando.GetProperty("conviteId").ValueKind.Should().Be(JsonValueKind.Null);
        var convidada = itens[posicaoDepois];
        convidada.GetProperty("situacao").GetString().Should().Be("CONVIDADA");
        convidada.GetProperty("convidadaEm").GetDateTimeOffset().Should().Be(original);
        convidada.GetProperty("conviteId").GetGuid().Should().NotBeEmpty();
        lista.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact] // F14-75: paginada como as outras listas da área
    public async Task Lista_ComPaginacao_RespeitaOTamanhoDaPagina()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        await InscreverAsync(api.App, Corpo(NovoEmail()));
        await InscreverAsync(api.App, Corpo(NovoEmail()));

        using var resposta = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Get, $"{Area}/lista-espera?tamanhoPagina=1&pagina=2", token);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await CorpoAsync(resposta);
        corpo.GetProperty("itens").GetArrayLength().Should().Be(1);
        corpo.GetProperty("pagina").GetInt32().Should().Be(2);
        corpo.GetProperty("tamanhoPagina").GetInt32().Should().Be(1);
        corpo.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }

    // ---------- F14-76: convidar a partir da entrada ----------

    [Fact]
    public async Task Convidar_EntradaAguardando_ConviteParaOEmailDelaEEntradaConvidadaNaTrilha() // F14-76
    {
        var operador = await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);
        var token = await OperadorNebrys.TokenAsync(api);
        var email = NovoEmail();
        await InscreverAsync(api.App, Corpo(email));
        var entrada = (await EntradasAsync(email)).Single();

        var (status, corpo) = await ConvidarAsync(token, entrada.Id);

        status.Should().Be(HttpStatusCode.Created, "corpo: {0}", corpo);
        corpo.GetProperty("emailEnviado").GetBoolean().Should().BeTrue();
        var convite = corpo.GetProperty("convite");
        var conviteId = convite.GetProperty("id").GetGuid();
        convite.GetProperty("email").GetString().Should().Be(email);
        convite.GetProperty("situacao").GetString().Should().Be("PENDENTE");
        convite.TryGetProperty("token", out _).Should().BeFalse();
        var mensagem = api.Emails.Para(email).Should().ContainSingle().Subject;
        mensagem.Categoria.Should().Be("convite-empresa");
        mensagem.Texto.Should().Contain("/cadastro/convite/");
        var depois = (await EntradasAsync(email)).Single();
        depois.ConvidadaEm.Should().Be(api.Relogio.Agora);
        (await OrigemDoConviteAsync(conviteId)).Should().Be(entrada.Id);
        var trilha = await TrilhaAsync(entrada.Id, conviteId);
        trilha.Should().ContainSingle(t => t.Acao == "nebrys.lista.convite" && t.Usuario == operador && t.AlvoTipo == "lista-espera");
        trilha.Should().ContainSingle(t => t.Acao == "nebrys.convite.emissao" && t.Usuario == operador);
        trilha.Should().AllSatisfy(t => t.Dados.Should().Contain(email).And.NotContain(api.Emails.TokenDe(email, "convite-empresa")));
        trilha.Single(t => t.Acao == "nebrys.lista.convite").Dados.Should().Contain("AGUARDANDO").And.Contain("CONVIDADA");
    }

    [Fact] // F14-76: o convite que saiu da entrada vale no cadastro, como o emitido direto
    public async Task Convidar_LinkDoConvite_CadastroComOEmailDaEntradaPassa()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var email = NovoEmail();
        await InscreverAsync(api.App, Corpo(email));
        var entrada = (await EntradasAsync(email)).Single();
        (await ConvidarAsync(token, entrada.Id)).Status.Should().Be(HttpStatusCode.Created);
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?> { ["Cadastro:Modo"] = "convite" });
        using var http = app.GetTestClient();

        using var registro = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), new
        {
            nome = "Ana Souza", email, senha = "senha-bem-comprida", nomeEmpresa = "Estúdio Traço", documento = DocumentosDeTeste.Novo(),
            versaoTermos = TextosAceitos.Termos, versaoPrivacidade = TextosAceitos.Privacidade,
            convite = api.Emails.TokenDe(email, "convite-empresa"),
        });

        registro.StatusCode.Should().Be(HttpStatusCode.Created, await registro.Content.ReadAsStringAsync());
    }

    [Fact] // F14-76: só a entrada aguardando
    public async Task Convidar_EntradaJaConvidada_422SemOutroConviteNemEmail()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var email = NovoEmail();
        await InscreverAsync(api.App, Corpo(email));
        var id = (await EntradasAsync(email)).Single().Id;
        (await ConvidarAsync(token, id)).Status.Should().Be(HttpStatusCode.Created);

        var (status, corpo) = await ConvidarAsync(token, id);

        status.Should().Be((HttpStatusCode)422, "corpo: {0}", corpo);
        corpo.GetProperty("type").GetString().Should().EndWith("/transicao-invalida");
        api.Emails.Para(email).Should().ContainSingle();
        (await ConvitesDoEmailAsync(email)).Should().Be(1);
    }

    [Fact] // F14-76 com a regra do F14n: um convite em aberto por e-mail
    public async Task Convidar_EmailJaComConviteEmAberto_422EEntradaContinuaAguardando()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var email = NovoEmail();
        await InscreverAsync(api.App, Corpo(email));
        var id = (await EntradasAsync(email)).Single().Id;
        using (var emitir = await OperadorNebrys.EnviarAsync(
            api.NovoHttp(), HttpMethod.Post, $"{Area}/convites", token, JsonContent.Create(new { email })))
        {
            emitir.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var (status, corpo) = await ConvidarAsync(token, id);

        status.Should().Be((HttpStatusCode)422, "corpo: {0}", corpo);
        (await EntradasAsync(email)).Single().ConvidadaEm.Should().BeNull("a recusa não marca a entrada");
        (await ConvitesDoEmailAsync(email)).Should().Be(1);
    }

    [Fact]
    public async Task ConvidarEDescartar_EntradaInexistente_404()
    {
        var token = await OperadorNebrys.TokenAsync(api);

        (await ConvidarAsync(token, Guid.NewGuid())).Status.Should().Be(HttpStatusCode.NotFound);
        (await DescartarAsync(token, Guid.NewGuid())).Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- F14-77: descartar ----------

    [Fact]
    public async Task Descartar_EntradaAguardando_ApagadaComTrilhaSoComOEmail() // F14-77
    {
        var operador = await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);
        var token = await OperadorNebrys.TokenAsync(api);
        var email = NovoEmail();
        await InscreverAsync(api.App, Corpo(email));
        var id = (await EntradasAsync(email)).Single().Id;

        (await DescartarAsync(token, id)).Should().Be(HttpStatusCode.NoContent);

        (await EntradasAsync(email)).Should().BeEmpty("a entrada é apagada");
        (await ListaAsync(token)).GetProperty("itens").EnumerateArray().Should().NotContain(i => i.GetProperty("id").GetGuid() == id);
        var registro = (await TrilhaAsync(id, Guid.Empty)).Should().ContainSingle().Subject;
        registro.Acao.Should().Be("nebrys.lista.descarte");
        registro.Usuario.Should().Be(operador);
        registro.Dados.Should().Contain(email).And.NotContain("Estúdio Traço").And.NotContain("Organizar o atendimento");
        (await DescartarAsync(token, id)).Should().Be(HttpStatusCode.NotFound, "já foi apagada");
    }

    [Fact] // F14-77: a entrada convidada também sai; o convite fica, sem a origem, e continua valendo
    public async Task Descartar_EntradaConvidada_ApagaEOConviteFicaSemAOrigem()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var email = NovoEmail();
        await InscreverAsync(api.App, Corpo(email));
        var id = (await EntradasAsync(email)).Single().Id;
        var (_, corpo) = await ConvidarAsync(token, id);
        var conviteId = corpo.GetProperty("convite").GetProperty("id").GetGuid();

        (await DescartarAsync(token, id)).Should().Be(HttpStatusCode.NoContent);

        (await EntradasAsync(email)).Should().BeEmpty();
        (await OrigemDoConviteAsync(conviteId)).Should().BeNull();
        using var convite = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Get, $"{Area}/convites/{conviteId}", token);
        convite.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CorpoAsync(convite)).GetProperty("situacao").GetString().Should().Be("PENDENTE");
    }

    [Fact] // F14-77: descartada, a pessoa pode entrar de novo na lista
    public async Task Inscrever_DepoisDoDescarte_GravaDeNovo()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var email = NovoEmail();
        await InscreverAsync(api.App, Corpo(email));
        (await DescartarAsync(token, (await EntradasAsync(email)).Single().Id)).Should().Be(HttpStatusCode.NoContent);

        (await InscreverAsync(api.App, Corpo(email))).Status.Should().Be(HttpStatusCode.Accepted);

        (await EntradasAsync(email)).Should().ContainSingle();
    }

    [Fact] // F14-76/F14-77 e D14: a trilha do convite e do descarte da convidada não guardam nome, empresa nem mensagem
    public async Task Trilha_ConvidarEDescartarConvidada_SemNomeEmpresaNemMensagem()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var email = NovoEmail();
        await InscreverAsync(api.App, Corpo(email));
        var id = (await EntradasAsync(email)).Single().Id;
        var (_, corpo) = await ConvidarAsync(token, id);
        var conviteId = corpo.GetProperty("convite").GetProperty("id").GetGuid();

        (await DescartarAsync(token, id)).Should().Be(HttpStatusCode.NoContent);

        var trilha = await TrilhaAsync(id, conviteId);
        trilha.Should().Contain(t => t.Acao == "nebrys.lista.convite").And.Contain(t => t.Acao == "nebrys.lista.descarte");
        trilha.Should().AllSatisfy(t => t.Dados.Should().NotContain("Ana Souza").And.NotContain("Estúdio Traço")
            .And.NotContain("Organizar o atendimento"));
        trilha.Single(t => t.Acao == "nebrys.lista.descarte").Dados.Should().Contain("CONVIDADA");
    }

    // ---------- F14-63: a área da lista só para a operação ----------

    [Fact]
    public async Task RotasDaLista_UsuarioDeEmpresaOuAnonimo_404EAEntradaNaoMuda()
    {
        var equipe = await Equipe.CriarAsync(api);
        var email = NovoEmail();
        await InscreverAsync(api.App, Corpo(email));
        var id = (await EntradasAsync(email)).Single().Id;

        foreach (var sessao in new[] { equipe.Dono, null })
        {
            using (var lista = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Get, $"{Area}/lista-espera", sessao))
                lista.StatusCode.Should().Be(HttpStatusCode.NotFound);
            using (var convidar = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Post, $"{Area}/lista-espera/{id}/convite", sessao))
                convidar.StatusCode.Should().Be(HttpStatusCode.NotFound);
            using (var descartar = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Delete, $"{Area}/lista-espera/{id}", sessao))
                descartar.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        var entrada = (await EntradasAsync(email)).Should().ContainSingle().Subject;
        entrada.ConvidadaEm.Should().BeNull();
        api.Emails.Para(email).Should().BeEmpty();
    }

    // ---------- apoio ----------

    private static string NovoEmail() => $"espera-{Guid.NewGuid():N}@exemplo.com";

    private static object Corpo(string email, string? tokenTurnstile = null) => new
    {
        nome = "Ana Souza", email, nomeEmpresa = "Estúdio Traço", mensagem = "Organizar o atendimento dos clientes.", tokenTurnstile,
    };

    private Task<WebApplication> ApiComTurnstileAsync(SiteverifyFalso siteverify) => api.NovaApiAsync(
        new Dictionary<string, string?>
        {
            ["Turnstile:Segredo"] = "segredo-dos-testes",
            ["App:UrlPublica"] = "https://acordu.app.br/",
        },
        antes: IpDoCabecalho,
        servicos: s => s.AddHttpClient(ComposicaoApi.NomeClienteTurnstile).ConfigurePrimaryHttpMessageHandler(() => siteverify));

    private Task<WebApplication> ApiComLimiteAsync() => api.NovaApiAsync(
        new Dictionary<string, string?> { ["RateLimit:ListaEspera:PorHora"] = "3" }, antes: IpDoCabecalho);

    private static async Task<HttpResponseMessage> EnviarAsync(WebApplication app, object corpo, string? ip = null)
    {
        using var http = app.GetTestClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri(Rota, UriKind.Relative)) { Content = JsonContent.Create(corpo) };
        if (ip is not null)
            requisicao.Headers.Add(CabecalhoIp, ip);
        return await http.SendAsync(requisicao);
    }

    private static async Task<(HttpStatusCode Status, JsonElement Corpo)> InscreverAsync(WebApplication app, object corpo, string? ip = null)
    {
        using var resposta = await EnviarAsync(app, corpo, ip);
        return (resposta.StatusCode, await CorpoAsync(resposta));
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> ConvidarAsync(string token, Guid id)
    {
        using var resposta = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Post, $"{Area}/lista-espera/{id}/convite", token);
        return (resposta.StatusCode, await CorpoAsync(resposta));
    }

    private async Task<HttpStatusCode> DescartarAsync(string token, Guid id)
    {
        using var resposta = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Delete, $"{Area}/lista-espera/{id}", token);
        return resposta.StatusCode;
    }

    private async Task<JsonElement> ListaAsync(string token)
    {
        using var resposta = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Get, $"{Area}/lista-espera?tamanhoPagina=200", token);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await CorpoAsync(resposta);
    }

    /// <summary>As entradas do e-mail, lidas direto da tabela global (sem filtro de tenant).</summary>
    private async Task<List<EntradaListaEspera>> EntradasAsync(string email)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        return await db.Set<EntradaListaEspera>().AsNoTracking().Where(e => e.Email == email).ToListAsync();
    }

    private async Task<int> TotalDeEntradasAsync()
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        return await db.Set<EntradaListaEspera>().CountAsync();
    }

    private async Task<int> EmpresasAsync()
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        return await db.Tenants.IgnoreQueryFilters().CountAsync();
    }

    private async Task<Guid?> OrigemDoConviteAsync(Guid convite)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        return await db.Set<ConviteEmpresa>().Where(c => c.Id == convite).Select(c => c.EntradaListaEsperaId).SingleAsync();
    }

    private async Task<int> ConvitesDoEmailAsync(string email)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        return await db.Set<ConviteEmpresa>().CountAsync(c => c.Email == email);
    }

    /// <summary>
    /// As ações da área sobre a entrada e o convite dela (<see cref="Guid.Empty"/> sem convite), na trilha sem tenant (lidas
    /// fora dos filtros).
    /// </summary>
    private async Task<List<(string Acao, string? AlvoTipo, Guid? Usuario, string Dados)>> TrilhaAsync(Guid entrada, Guid convite)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        var linhas = await db.Database.SqlQuery<LinhaTrilha>($"""
            SELECT acao, alvo_tipo, usuario_id, dados::text AS dados FROM auditoria
            WHERE tenant_id IS NULL AND (alvo_id = {entrada} OR alvo_id = {convite}) AND acao LIKE 'nebrys.%'
            ORDER BY ocorrido_em
            """).ToListAsync();
        return linhas.Select(l => (l.Acao, l.AlvoTipo, l.UsuarioId, l.Dados ?? "")).ToList();
    }

    private static string Erro(JsonElement corpo, string campo) => corpo.GetProperty("errors").GetProperty(campo)[0].GetString()!;

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

    private static string Aprovado(string acao) => $$"""{"success":true,"hostname":"acordu.app.br","action":"{{acao}}"}""";

    private sealed class LinhaTrilha
    {
        public string Acao { get; set; } = "";

        public string? AlvoTipo { get; set; }

        public Guid? UsuarioId { get; set; }

        public string? Dados { get; set; }
    }

    /// <summary>O siteverify do Cloudflare, falso: guarda os corpos recebidos e responde o que o teste mandar.</summary>
    private sealed class SiteverifyFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public SiteverifyFalso(string json)
            : this(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") })
        {
        }

        public List<string> Corpos { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Corpos.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return responder(request);
        }
    }
}
