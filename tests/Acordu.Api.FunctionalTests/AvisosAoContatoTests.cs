using System.Net;
using System.Net.Http.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Application.Abstractions;

namespace Acordu.Api.FunctionalTests;

/// <summary>RF-NOT-01: e-mails ao contato da solicitação, com link do portal (conta) ou link sem senha (sem conta).</summary>
[Collection(ColecaoApi.Nome)]
public sealed class AvisosAoContatoTests(ApiFixture api)
{
    [Fact]
    public async Task ContatoSemConta_RecebeOsQuatroAvisos_ComLinkNovoACadaEmail()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (contato, email) = await ContatoAsync(equipe, cliente);

        var solicitacao = Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, contatoId = contato, titulo = "Trocar a logo do site", prioridade = "MEDIA",
        }));

        var recebida = Ultimo(email, "aviso-recebida");
        recebida.Assunto.Should().Contain("#");
        recebida.NomeRemetente.Should().Be("Estúdio Traço");
        var primeiroLink = api.Emails.TokenDe(email, "aviso-recebida");
        (await TrocarAsync(primeiroLink)).Should().Be(HttpStatusCode.OK);

        await Comentar(equipe, solicitacao, "Anotação só da equipe", "INTERNA");
        api.Emails.Para(email).Should().NotContain(m => m.Categoria == "aviso-mensagem");
        await Comentar(equipe, solicitacao, "Pode mandar a logo em alta resolução?", "EXTERNA");
        Ultimo(email, "aviso-mensagem").Texto.Should().Contain("Pode mandar a logo em alta resolução?");
        (await TrocarAsync(primeiroLink)).Should().Be(HttpStatusCode.Unauthorized, "cada e-mail leva um link novo e revoga o anterior");
        (await TrocarAsync(api.Emails.TokenDe(email, "aviso-mensagem"))).Should().Be(HttpStatusCode.OK);

        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/aguardando-cliente");
        Ultimo(email, "aviso-aguardando").Assunto.Should().Contain("Precisamos de você");

        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/resolucao",
            new { solucao = new { texto = "Logo trocada" } });
        var resolvida = Ultimo(email, "aviso-resolvida");
        resolvida.Texto.Should().Contain("5 dias");
        (await TrocarAsync(api.Emails.TokenDe(email, "aviso-resolvida"))).Should().Be(HttpStatusCode.OK);
        api.Emails.Para(email).Count(m => m.Categoria == "aviso-resolvida").Should().Be(1, "resolver não duplica com a mudança de status");
    }

    [Fact]
    public async Task EnviarEAguardar_UmSoEmailQuePedeORetorno() // F11-04
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (contato, email) = await ContatoAsync(equipe, cliente);
        var solicitacao = Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, contatoId = contato, titulo = "Trocar a logo do site", prioridade = "MEDIA",
        }));

        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/interacoes",
            new { texto = "Pode mandar a logo em alta resolução?", visibilidade = "EXTERNA", aguardarCliente = true });

        var mensagem = Ultimo(email, "aviso-mensagem");
        mensagem.Assunto.Should().Contain("Precisamos de você");
        mensagem.Texto.Should().Contain("Pode mandar a logo em alta resolução?");
        api.Emails.Para(email).Should().NotContain(m => m.Categoria == "aviso-aguardando", "o aviso de espera já foi na mensagem");
    }

    [Fact]
    public async Task ContatoComConta_RecebeLinkDoPortal_ENaoEhAvisadoDaPropriaResposta()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);

        var aberta = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, "/api/v1/portal/solicitacoes", new { titulo = "Site fora do ar" }, contato.Token);
        var solicitacao = Cenarios.Id(aberta);

        Ultimo(contato.Email, "aviso-recebida").Texto.Should().Contain($"/portal/solicitacoes/{solicitacao}");
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal"))
            .GetArrayLength().Should().Be(0, "contato com conta não ganha link sem senha");
        var antes = api.Emails.Para(contato.Email).Count;
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/portal/solicitacoes/{solicitacao}/interacoes",
            new { texto = "Continua fora" }, contato.Token);
        api.Emails.Para(contato.Email).Should().HaveCount(antes);
    }

    [Fact]
    public async Task SemContatoOuContatoInativo_NadaSai()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (contato, email) = await ContatoAsync(equipe, cliente);
        var semContato = await Cenarios.SolicitacaoAsync(equipe, cliente);
        var comContato = Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, contatoId = contato, titulo = "Ajuste no cardápio", prioridade = "BAIXA",
        }));
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/clientes/{cliente}/contatos/{contato}");
        var antes = api.Emails.Para(email).Count;

        await Comentar(equipe, semContato, "Sem ninguém para avisar", "EXTERNA");
        await Comentar(equipe, comContato, "Contato inativo", "EXTERNA");

        api.Emails.Para(email).Should().HaveCount(antes);
    }

    [Fact]
    public async Task ResponderParaDaEmpresa_VaiNosEmailsAoContato()
    {
        var equipe = await Equipe.CriarAsync(api);
        var empresa = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/configuracoes/empresa");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/configuracoes/empresa", new
        {
            nome = empresa.GetProperty("nome").GetString(),
            responderPara = "atendimento@estudiotraco.com.br",
            slaPadrao = empresa.GetProperty("slaPadrao"),
        });
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (contato, email) = await ContatoAsync(equipe, cliente);

        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, contatoId = contato, titulo = "Trocar a logo do site", prioridade = "MEDIA",
        });

        Ultimo(email, "aviso-recebida").ResponderPara.Should().Be("atendimento@estudiotraco.com.br");
    }

    private MensagemEmail Ultimo(string email, string categoria) =>
        api.Emails.Para(email).Last(m => m.Categoria == categoria);

    private static async Task<(Guid Id, string Email)> ContatoAsync(Equipe equipe, Guid cliente)
    {
        var email = $"contato-{Guid.NewGuid():N}@bomgrao.com.br";
        var contato = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos", new { nome = "Carlos Prado", email });
        return (Cenarios.Id(contato), email);
    }

    private static Task<System.Text.Json.JsonElement> Comentar(Equipe equipe, Guid solicitacao, string texto, string visibilidade) =>
        equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/interacoes",
            new { texto, visibilidade });

    private async Task<HttpStatusCode> TrocarAsync(string token)
    {
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/portal/link", UriKind.Relative), new { token });
        return resposta.StatusCode;
    }
}
