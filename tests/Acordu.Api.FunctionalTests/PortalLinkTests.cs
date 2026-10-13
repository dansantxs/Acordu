using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// RF-POR-08, contrato §2 e §7, segurança §5.3: link de acesso a uma solicitação, sem conta. Emissão, troca por sessão,
/// rotas ✓L, isolamento na solicitação, revogação e expiração.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed partial class PortalLinkTests(ApiFixture api)
{
    private const string Portal = "/api/v1/portal";

    [Fact]
    public async Task Emitir_Trocar_EUsarAsRotasDoLink()
    {
        var (equipe, cliente, solicitacao) = await CenarioAsync();
        var (contato, email) = await ContatoAsync(equipe, cliente);

        var emitido = await Emitir(equipe, solicitacao, contato);

        emitido.GetProperty("expiraEm").GetDateTimeOffset().Should().Be(api.Relogio.Agora.AddDays(30));
        emitido.GetProperty("emailEnviado").GetBoolean().Should().BeTrue();
        var token = Token(emitido.GetProperty("link").GetString()!);
        var mensagem = api.Emails.Para(email).Single(m => m.Categoria == "link-portal");
        mensagem.Texto.Should().Contain(token).And.Contain("#");
        var links = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal");
        links.GetArrayLength().Should().Be(1);
        links[0].GetProperty("acessoId").GetGuid().Should().Be(emitido.GetProperty("acessoId").GetGuid());
        links[0].GetProperty("contatoNome").GetString().Should().Be("Carlos Prado");

        var sessao = await TrocarAsync(token);
        sessao.GetProperty("solicitacaoId").GetGuid().Should().Be(solicitacao);
        sessao.GetProperty("tipo").GetString().Should().Be("LINK_SOLICITACAO");
        var acesso = sessao.GetProperty("accessToken").GetString()!;
        var auditoria = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auditoria?acao=portal.link.troca");
        auditoria.GetProperty("itens")[0].GetProperty("alvoId").GetGuid().Should().Be(solicitacao);
        auditoria.GetProperty("itens")[0].GetProperty("usuario").ValueKind.Should().Be(JsonValueKind.Null);

        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/eu", token: acesso);
        eu.GetProperty("contato").GetProperty("id").GetGuid().Should().Be(contato);
        eu.GetProperty("solicitacaoId").GetGuid().Should().Be(solicitacao);
        eu.GetProperty("veContratos").GetBoolean().Should().BeFalse();
        var detalhe = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes/{solicitacao}", token: acesso);
        detalhe.GetProperty("numero").GetInt32().Should().BePositive();

        var resposta = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"{Portal}/solicitacoes/{solicitacao}/interacoes",
            new { texto = "Confirmo o horário" }, acesso);
        resposta.GetProperty("autorNome").GetString().Should().Be("Carlos Prado");
        resposta.GetProperty("daEquipe").GetBoolean().Should().BeFalse();
        var interacoes = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/interacoes");
        interacoes[0].GetProperty("autor").GetProperty("nome").GetString().Should().Be("Carlos Prado");
        interacoes[0].GetProperty("autorEhCliente").GetBoolean().Should().BeTrue();

        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/resolucao",
            new { solucao = new { texto = "Feito" } });
        var aprovada = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Post, $"{Portal}/solicitacoes/{solicitacao}/aprovacao", token: acesso);
        aprovada.GetProperty("statusExterno").GetString().Should().Be("CONCLUIDA");
    }

    [Fact]
    public async Task SessaoDeLink_SoAlcancaASuaSolicitacao()
    {
        var (equipe, cliente, solicitacao) = await CenarioAsync();
        var (contato, _) = await ContatoAsync(equipe, cliente);
        var outra = await Cenarios.SolicitacaoAsync(equipe, cliente);
        var acesso = (await TrocarAsync(Token((await Emitir(equipe, solicitacao, contato)).GetProperty("link").GetString()!)))
            .GetProperty("accessToken").GetString()!;

        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"{Portal}/solicitacoes/{outra}", token: acesso);
        await equipe.EsperarAsync(
            HttpStatusCode.NotFound, HttpMethod.Post, $"{Portal}/solicitacoes/{outra}/interacoes", new { texto = "Oi" }, acesso);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, $"{Portal}/solicitacoes", token: acesso);
        await equipe.EsperarAsync(
            HttpStatusCode.Forbidden, HttpMethod.Post, $"{Portal}/solicitacoes", new { titulo = "Nova" }, acesso);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, $"{Portal}/contratos", token: acesso);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, "/api/v1/clientes", token: acesso);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, "/api/v1/auth/eu", token: acesso);
    }

    [Fact]
    public async Task RevogarOuReemitir_InvalidaOLinkNaHora()
    {
        var (equipe, cliente, solicitacao) = await CenarioAsync();
        var (contato, _) = await ContatoAsync(equipe, cliente);
        var primeiro = await Emitir(equipe, solicitacao, contato);
        var tokenPrimeiro = Token(primeiro.GetProperty("link").GetString()!);
        var sessaoPrimeiro = (await TrocarAsync(tokenPrimeiro)).GetProperty("accessToken").GetString()!;

        var segundo = await Emitir(equipe, solicitacao, contato);

        await equipe.EsperarAsync(HttpStatusCode.Unauthorized, HttpMethod.Get, $"{Portal}/eu", token: sessaoPrimeiro);
        (await TentarTrocarAsync(tokenPrimeiro)).Should().Be(HttpStatusCode.Unauthorized);
        var sessaoSegundo = (await TrocarAsync(Token(segundo.GetProperty("link").GetString()!))).GetProperty("accessToken").GetString()!;

        await equipe.EsperarAsync(
            HttpStatusCode.NoContent, HttpMethod.Delete,
            $"/api/v1/solicitacoes/{solicitacao}/acessos-portal/{segundo.GetProperty("acessoId").GetGuid()}");

        await equipe.EsperarAsync(HttpStatusCode.Unauthorized, HttpMethod.Get, $"{Portal}/eu", token: sessaoSegundo);
        var links = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal");
        links.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task LinkExpirado_401_EInativarContatoRevoga()
    {
        var (equipe, cliente, solicitacao) = await CenarioAsync();
        var (contato, _) = await ContatoAsync(equipe, cliente);
        var (outroContato, _) = await ContatoAsync(equipe, cliente, "Rita Alves");
        var curto = Token((await Emitir(equipe, solicitacao, contato, validadeDias: 1)).GetProperty("link").GetString()!);
        var doOutro = Token((await Emitir(equipe, solicitacao, outroContato)).GetProperty("link").GetString()!);

        var agora = api.Relogio.Agora;
        api.Relogio.Agora = agora.AddDays(1);
        try
        {
            (await TentarTrocarAsync(curto)).Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            api.Relogio.Agora = agora;
        }

        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/clientes/{cliente}/contatos/{outroContato}");
        (await TentarTrocarAsync(doOutro)).Should().Be(HttpStatusCode.Unauthorized);
        (await TentarTrocarAsync("token-que-nao-existe")).Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Emitir_Recusas_ESemEmail()
    {
        var (equipe, cliente, solicitacao) = await CenarioAsync();
        var (contato, email) = await ContatoAsync(equipe, cliente);
        var outroCliente = await Cenarios.ClienteAsync(equipe, "Oficina do Zé");
        var (deOutroCliente, _) = await ContatoAsync(equipe, outroCliente);
        var rota = $"/api/v1/solicitacoes/{solicitacao}/acessos-portal";

        var contatoErrado = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, rota, new { contatoId = deOutroCliente });
        contatoErrado.GetProperty("errors").TryGetProperty("contatoId", out _).Should().BeTrue();
        var validade = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, rota, new { contatoId = contato, validadeDias = 91 });
        validade.GetProperty("errors").TryGetProperty("validadeDias", out _).Should().BeTrue();
        var leitor = await equipe.ComPapelAsync(Papel.NomeLeitor);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Post, rota, new { contatoId = contato }, leitor);

        var semEmail = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, rota, new { contatoId = contato, enviarEmail = false });
        semEmail.GetProperty("emailEnviado").GetBoolean().Should().BeFalse();
        api.Emails.Para(email).Should().BeEmpty();

        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/cancelamento",
            new { motivo = "Cliente desistiu" });
        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, rota, new { contatoId = contato });
    }

    private async Task<(Equipe Equipe, Guid Cliente, Guid Solicitacao)> CenarioAsync()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        await Cenarios.ContratoAsync(equipe, cliente);
        return (equipe, cliente, await Cenarios.SolicitacaoAsync(equipe, cliente));
    }

    private static async Task<(Guid Id, string Email)> ContatoAsync(Equipe equipe, Guid cliente, string nome = "Carlos Prado")
    {
        var email = $"contato-{Guid.NewGuid():N}@bomgrao.com.br";
        var contato = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos", new { nome, email });
        return (Cenarios.Id(contato), email);
    }

    private static Task<JsonElement> Emitir(Equipe equipe, Guid solicitacao, Guid contato, int? validadeDias = null) =>
        equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal",
            new { contatoId = contato, validadeDias });

    private static string Token(string link) => TokenDoLink().Match(link).Groups[1].Value;

    private async Task<JsonElement> TrocarAsync(string token)
    {
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/portal/link", UriKind.Relative), new { token });
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, "corpo: {0}", texto);
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    private async Task<HttpStatusCode> TentarTrocarAsync(string token)
    {
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/portal/link", UriKind.Relative), new { token });
        return resposta.StatusCode;
    }

    [GeneratedRegex(@"/portal/l/([A-Za-z0-9_-]{43})$")]
    private static partial Regex TokenDoLink();
}
