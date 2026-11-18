using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14c (F14-20, F14-21, F14-97 a F14-99; design D2 e D3, ADR 0015): na empresa só leitura o contato lê como
/// antes, só o que é dele; as gravações dele são recusadas com <c>422 portal-indisponivel</c> e a mensagem neutra; convite
/// antigo não cria vínculo nem conta e continua pendente; link antigo entra e só lê.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed partial class PortalSomenteLeituraTests(ApiFixture api)
{
    private const string Portal = "/api/v1/portal";
    private const string Erros = "https://acordu.app.br/erros/";
    private const string Neutra = "No momento não é possível enviar por aqui; fale com Estúdio Traço.";
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    [GeneratedRegex(@"/portal/l/([A-Za-z0-9_\-]+)")]
    private static partial Regex TokenDoLink();

    private async Task SituacaoAsync(Guid tenantId, string situacao)
    {
        var desde = api.Relogio.Agora.ToUniversalTime();
        await using var db = api.DaEquipe(tenantId);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE tenants SET situacao = {situacao}, somente_leitura_desde = {desde} WHERE id = {tenantId}");
    }

    /// <summary>422 <c>portal-indisponivel</c>, com a mensagem neutra: nada de "só leitura", "vencida" ou "cancelada".</summary>
    private static async Task IndisponivelAsync(Equipe equipe, HttpMethod metodo, string rota, object? corpo, string token)
    {
        var problema = await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, metodo, rota, corpo, token);
        NeutroDeveSer(problema);
    }

    private static void NeutroDeveSer(JsonElement problema)
    {
        problema.GetProperty("type").GetString().Should().Be(Erros + "portal-indisponivel");
        problema.GetProperty("detail").GetString().Should().Be(Neutra);
        var texto = problema.GetRawText().ToUpperInvariant();
        foreach (var palavra in new[] { "LEITURA", "VENC", "CANCEL", "NEBRYS", "TESTE" })
            texto.Should().NotContain(palavra);
    }

    private async Task<HttpResponseMessage> PostAsync(string rota, object corpo)
    {
        using var http = api.NovoHttp();
        return await http.PostAsJsonAsync(new Uri(rota, UriKind.Relative), corpo);
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> MultipartAsync(string rota, string token)
    {
        using var formulario = new MultipartFormDataContent
        {
            { new StringContent("Segue a foto"), "texto" },
        };
        var parte = new ByteArrayContent(Png);
        parte.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        formulario.Add(parte, "anexos", "foto.png");
        using var http = api.NovoHttp();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri(rota, UriKind.Relative)) { Content = formulario };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(pedido);
        var texto = await resposta.Content.ReadAsStringAsync();
        return (resposta.StatusCode, texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone());
    }

    private static Task<JsonElement> Resolver(Equipe equipe, Guid solicitacao) =>
        equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/resolucao",
            new { solucao = new { texto = "Feito" } });

    private async Task<int> AnexosNoStorageAsync(Guid tenantId)
    {
        await using var db = api.DaEquipe(tenantId);
        return await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM anexos a JOIN interacoes i ON i.id = a.interacao_id WHERE i.tenant_id = {tenantId}").SingleAsync();
    }

    [Fact]
    public async Task Leituras_EmpresaVencida_ContatoLeComoAntesSoDoClienteDele() // F14-20
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var contrato = await ContratoAsync(equipe, cliente);
        var solicitacao = await SolicitacaoAsync(equipe, cliente);
        var outroCliente = await ClienteAsync(equipe, "Oficina do Zé");
        var daOficina = await SolicitacaoAsync(equipe, outroCliente);
        var contratoOficina = await ContratoAsync(equipe, outroCliente);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        string[] rotas =
        [
            $"{Portal}/solicitacoes", $"{Portal}/solicitacoes/{solicitacao}", $"{Portal}/solicitacoes/{solicitacao}/interacoes",
            $"{Portal}/contratos", $"{Portal}/contratos/{contrato}/extrato",
        ];
        var antes = new Dictionary<string, string>();
        foreach (var rota in rotas)
            antes[rota] = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, rota, token: contato.Token)).GetRawText();

        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        foreach (var rota in rotas)
            (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, rota, token: contato.Token)).GetRawText().Should().Be(antes[rota], rota);
        var lista = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes", token: contato.Token);
        lista.GetProperty("itens").EnumerateArray().Select(Id).Should().Equal(solicitacao);
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"{Portal}/solicitacoes/{daOficina}", token: contato.Token);
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"{Portal}/contratos/{contratoOficina}/extrato", token: contato.Token);
    }

    [Theory]
    [InlineData("TESTE", true)]
    [InlineData("ATIVA", true)]
    [InlineData("VENCIDA", false)]
    [InlineData("CANCELADA", false)]
    public async Task Eu_EnvioDisponivelPelaSituacao(string situacao, bool disponivel) // F14-21 (o que a tela usa para desabilitar)
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        await SituacaoAsync(equipe.TenantId, situacao);

        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/eu", token: contato.Token);

        eu.GetProperty("envioDisponivel").GetBoolean().Should().Be(disponivel);
        eu.TryGetProperty("situacao", out _).Should().BeFalse("o portal não vê a situação comercial do prestador");
        eu.GetRawText().ToUpperInvariant().Should().NotContain("VENC").And.NotContain("CANCEL").And.NotContain("LEITURA");
    }

    [Fact]
    public async Task Gravar_ContatoEmEmpresaVencida_404E400VemAntesDaRecusa() // F14-21 + isolamento por cliente
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var outroCliente = await ClienteAsync(equipe, "Oficina do Zé");
        var daOficina = await SolicitacaoAsync(equipe, outroCliente);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        // Solicitação de outro cliente: 404, sem revelar nada (nem a recusa).
        var alheia = await equipe.EsperarAsync(
            HttpStatusCode.NotFound, HttpMethod.Post, $"{Portal}/solicitacoes/{daOficina}/interacoes", new { texto = "Oi" }, contato.Token);
        alheia.GetProperty("type").GetString().Should().Be(Erros + "nao-encontrado");
        // Dado inválido: 400 com errors.
        var invalido = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, $"{Portal}/solicitacoes", new { titulo = "" }, contato.Token);
        invalido.GetProperty("type").GetString().Should().Be(Erros + "validacao");
        invalido.TryGetProperty("errors", out _).Should().BeTrue();
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{daOficina}/interacoes"))
            .GetArrayLength().Should().Be(0, "nada foi gravado na solicitação do outro cliente");
    }

    [Theory]
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task Gravar_ContatoComConta_RecusaNeutraSemGravar(string situacao) // F14-21
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        await ContratoAsync(equipe, cliente);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        var aberta = await SolicitacaoAsync(equipe, cliente);
        var resolvida = await SolicitacaoAsync(equipe, cliente);
        await Resolver(equipe, resolvida);
        var lista = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes", token: contato.Token)).GetRawText();
        var conversa = (await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes/{aberta}/interacoes", token: contato.Token)).GetRawText();
        var anexos = await AnexosNoStorageAsync(equipe.TenantId);
        await SituacaoAsync(equipe.TenantId, situacao);

        await IndisponivelAsync(equipe, HttpMethod.Post, $"{Portal}/solicitacoes", new { titulo = "Trocar a foto da vitrine" }, contato.Token);
        await IndisponivelAsync(
            equipe, HttpMethod.Post, $"{Portal}/solicitacoes/{aberta}/interacoes", new { texto = "Alguma novidade?" }, contato.Token);
        var (status, anexo) = await MultipartAsync($"{Portal}/solicitacoes/{aberta}/interacoes", contato.Token);
        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        NeutroDeveSer(anexo);
        await IndisponivelAsync(equipe, HttpMethod.Post, $"{Portal}/solicitacoes/{resolvida}/aprovacao", null, contato.Token);
        await IndisponivelAsync(
            equipe, HttpMethod.Post, $"{Portal}/solicitacoes/{resolvida}/reprovacao", new { motivo = "Faltou a capa" }, contato.Token);

        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes", token: contato.Token)).GetRawText()
            .Should().Be(lista);
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes/{aberta}/interacoes", token: contato.Token))
            .GetRawText().Should().Be(conversa);
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{resolvida}"))
            .GetProperty("status").GetString().Should().Be("RESOLVIDA");
        (await AnexosNoStorageAsync(equipe.TenantId)).Should().Be(anexos);
        api.Arquivos.ComPrefixo($"{equipe.TenantId}/{aberta}/")
            .Should().Be(0, "o anexo enviado antes da recusa sai do storage");
    }

    [Fact]
    public async Task ConviteDaEquipeAntigo_EmpresaVencida_RecusaENaoCriaNada() // F14-97
    {
        var equipe = await Equipe.CriarAsync(api);
        var papeis = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/papeis");
        var membro = Id(papeis.EnumerateArray().Single(p => p.GetProperty("nome").GetString() == Papel.NomeMembro));
        var email = $"pessoa-{Guid.NewGuid():N}@estudio.com";
        await equipe.EsperarAsync(HttpStatusCode.Accepted, HttpMethod.Post, "/api/v1/convites", new { email, papelId = membro });
        var token = api.Emails.TokenDoConviteEquipe(email);
        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        using var aceite = await PostAsync("/api/v1/auth/convite", new { token, nome = "Bruno Lima", senha = Equipe.Senha, versaoTermos = Apoio.TextosAceitos.Termos, versaoPrivacidade = Apoio.TextosAceitos.Privacidade });

        aceite.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problema = await Equipe.JsonAsync(aceite);
        problema.GetProperty("type").GetString().Should().Be(Erros + "empresa-somente-leitura");
        aceite.Headers.TryGetValues("Set-Cookie", out _).Should().BeFalse("nenhuma sessão é aberta");
        await using (var db = api.DaEquipe(equipe.TenantId))
        {
            (await db.Users.AnyAsync(u => u.Email == email)).Should().BeFalse("a conta nova volta com a transação");
            (await db.Vinculos.CountAsync()).Should().Be(1, "só o Dono");
        }

        var convites = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/convites");
        convites.EnumerateArray().Should().ContainSingle(c => c.GetProperty("email").GetString() == email);
        using var consulta = await PostAsync("/api/v1/auth/convite/consulta", new { token });
        consulta.StatusCode.Should().Be(HttpStatusCode.OK, "o convite continua pendente e válido");

        // Com a empresa gravável de novo (ativação do F14l), o mesmo convite é aceito.
        await SituacaoAsync(equipe.TenantId, "ATIVA");
        using var depois = await PostAsync("/api/v1/auth/convite", new { token, nome = "Bruno Lima", senha = Equipe.Senha, versaoTermos = Apoio.TextosAceitos.Termos, versaoPrivacidade = Apoio.TextosAceitos.Privacidade });
        depois.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ConviteDaEquipeAntigo_ContaExistente_RecusaSemVinculo() // F14-97 (conta que já existia)
    {
        var equipe = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        var papeis = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/papeis");
        var membro = Id(papeis.EnumerateArray().Single(p => p.GetProperty("nome").GetString() == Papel.NomeMembro));
        await equipe.EsperarAsync(HttpStatusCode.Accepted, HttpMethod.Post, "/api/v1/convites", new { email = outra.EmailDono, papelId = membro });
        var token = api.Emails.TokenDoConviteEquipe(outra.EmailDono);
        await SituacaoAsync(equipe.TenantId, "CANCELADA");

        using var aceite = await PostAsync("/api/v1/auth/convite", new { token, senha = Equipe.Senha, versaoTermos = Apoio.TextosAceitos.Termos, versaoPrivacidade = Apoio.TextosAceitos.Privacidade });

        aceite.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Equipe.JsonAsync(aceite)).GetProperty("type").GetString().Should().Be(Erros + "empresa-somente-leitura");
        await using var db = api.DaEquipe(equipe.TenantId);
        (await db.Vinculos.CountAsync()).Should().Be(1, "só o Dono");
        using var consulta = await PostAsync("/api/v1/auth/convite/consulta", new { token });
        consulta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ConviteDoPortalAntigo_EmpresaVencida_RecusaNeutraENaoCriaNada() // F14-98
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var email = $"contato-{Guid.NewGuid():N}@bomgrao.com.br";
        var contato = Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos", new { nome = "Marina Souza", email }));
        await equipe.EsperarAsync(
            HttpStatusCode.Accepted, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos/{contato}/acesso-portal",
            new { escopoPortal = "TODAS_DO_CLIENTE" });
        var token = api.Emails.TokenDoConvite(email);
        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        using var aceite = await PostAsync("/api/v1/auth/portal/convite", new { token, senha = ContatosDoPortal.Senha });

        aceite.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        NeutroDeveSer(await Equipe.JsonAsync(aceite));
        aceite.Headers.TryGetValues("Set-Cookie", out _).Should().BeFalse("nenhuma sessão é aberta");
        await using (var db = api.DaEquipe(equipe.TenantId))
        {
            (await db.Users.AnyAsync(u => u.Email == email)).Should().BeFalse("a conta nova volta com a transação");
            (await db.Vinculos.CountAsync()).Should().Be(1, "só o Dono");
            var registro = await db.Clientes.AsNoTracking().SingleAsync(c => c.Id == cliente);
            registro.Contatos.Single(c => c.Id == contato).UsuarioId.Should().BeNull();
        }

        using var consulta = await PostAsync("/api/v1/auth/portal/convite/consulta", new { token });
        consulta.StatusCode.Should().Be(HttpStatusCode.OK, "o convite continua pendente e válido");
        await SituacaoAsync(equipe.TenantId, "ATIVA");
        using var depois = await PostAsync("/api/v1/auth/portal/convite", new { token, senha = ContatosDoPortal.Senha });
        depois.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task LinkAntigo_EmpresaVencida_EntraLeEGravacoesRecusadas() // F14-99
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var solicitacao = await SolicitacaoAsync(equipe, cliente);
        var outra = await SolicitacaoAsync(equipe, cliente);
        var contato = Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos",
            new { nome = "Carlos Prado", email = $"carlos-{Guid.NewGuid():N}@bomgrao.com.br" }));
        var emitido = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal", new { contatoId = contato });
        var token = TokenDoLink().Match(emitido.GetProperty("link").GetString()!).Groups[1].Value;
        var conversa = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/interacoes")).GetRawText();
        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        using var troca = await PostAsync("/api/v1/auth/portal/link", new { token });
        troca.StatusCode.Should().Be(HttpStatusCode.OK, "o link antigo ainda entra");
        var acesso = (await Equipe.JsonAsync(troca)).GetProperty("accessToken").GetString()!;

        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/eu", token: acesso);
        eu.GetProperty("solicitacaoId").GetGuid().Should().Be(solicitacao);
        eu.GetProperty("envioDisponivel").GetBoolean().Should().BeFalse();
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes/{solicitacao}", token: acesso))
            .GetProperty("titulo").GetString().Should().NotBeNullOrEmpty();
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes/{solicitacao}/interacoes", token: acesso);
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"{Portal}/solicitacoes/{outra}", token: acesso);

        await IndisponivelAsync(equipe, HttpMethod.Post, $"{Portal}/solicitacoes/{solicitacao}/interacoes", new { texto = "Confirmo" }, acesso);
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/interacoes")).GetRawText()
            .Should().Be(conversa);
    }

    [Fact]
    public async Task Gravar_ContatoDaEmpresaGravavel_SegueComOutraVencida() // F14-21 (isolamento entre tenants)
    {
        var vencida = await Equipe.CriarAsync(api);
        var gravavel = await Equipe.CriarAsync(api);
        var clienteVencida = await ClienteAsync(vencida);
        var clienteGravavel = await ClienteAsync(gravavel);
        var naVencida = await ContatosDoPortal.CriarAsync(api, vencida, clienteVencida);
        var naGravavel = await ContatosDoPortal.CriarAsync(api, gravavel, clienteGravavel);
        await SituacaoAsync(vencida.TenantId, "VENCIDA");

        await IndisponivelAsync(vencida, HttpMethod.Post, $"{Portal}/solicitacoes", new { titulo = "Pedido" }, naVencida.Token);
        await gravavel.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"{Portal}/solicitacoes", new { titulo = "Pedido" }, naGravavel.Token);
        (await gravavel.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/eu", token: naGravavel.Token))
            .GetProperty("envioDisponivel").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Gravar_CaminhoEmMaiusculas_AindaPortalIndisponivel() // F14-21: o tipo não depende da caixa do caminho
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        await IndisponivelAsync(equipe, HttpMethod.Post, "/api/v1/PORTAL/solicitacoes", new { titulo = "Trocar a foto da vitrine" }, contato.Token);
        await IndisponivelAsync(equipe, HttpMethod.Post, "/API/V1/Portal/solicitacoes", new { titulo = "Trocar a foto da vitrine" }, contato.Token);
    }

    [Fact]
    public async Task Gravar_EquipeNaEmpresaVencida_SegueComOTipoDaEquipe() // D3: a superfície da rota decide o tipo
    {
        var equipe = await Equipe.CriarAsync(api);
        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        var problema = await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, "/api/v1/clientes", new { nome = "Mercado Central" });

        problema.GetProperty("type").GetString().Should().Be(Erros + "empresa-somente-leitura");
    }
}
