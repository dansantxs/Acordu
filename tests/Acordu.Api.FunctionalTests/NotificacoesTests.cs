using System.Net;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;

namespace Acordu.Api.FunctionalTests;

/// <summary>RF-NOT-03/04 (contrato §11): sino gerado com os avisos internos, leitura e preferências de e-mail.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class NotificacoesTests(ApiFixture api)
{
    private static async Task<string> EmailAsync(Equipe equipe, string token) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: token)).GetProperty("email").GetString()!;

    [Fact]
    public async Task NovaSolicitacao_ViraNotificacao_ELeitura()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);

        var sino = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: membro);
        var doDono = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes");

        sino.GetProperty("naoLidas").GetInt32().Should().Be(1);
        var item = sino.GetProperty("itens")[0];
        item.GetProperty("categoria").GetString().Should().Be("NOVA_SOLICITACAO");
        item.GetProperty("titulo").GetString().Should().StartWith("Nova solicitação #");
        item.GetProperty("link").GetString().Should().EndWith($"/solicitacoes/{solicitacao}");
        doDono.GetProperty("total").GetInt32().Should().Be(0, "quem abriu não é avisado");

        var id = Cenarios.Id(item);
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Post, $"/api/v1/notificacoes/{id}/leitura");
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Post, $"/api/v1/notificacoes/{id}/leitura", token: membro);
        var lidas = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: membro);
        var naoLidas = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes?naoLidas=true", token: membro);

        lidas.GetProperty("naoLidas").GetInt32().Should().Be(0);
        lidas.GetProperty("itens")[0].GetProperty("lidaEm").GetString().Should().NotBeNull();
        naoLidas.GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task MarcarTodas_ZeraOSino()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var cliente = await Cenarios.ClienteAsync(equipe);
        await Cenarios.SolicitacaoAsync(equipe, cliente);
        await Cenarios.SolicitacaoAsync(equipe, cliente);

        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Post, "/api/v1/notificacoes/leitura", token: membro);
        var sino = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: membro);

        sino.GetProperty("total").GetInt32().Should().Be(2);
        sino.GetProperty("naoLidas").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Preferencias_EmailDesligado_SinoContinua()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var email = await EmailAsync(equipe, membro);
        var cliente = await Cenarios.ClienteAsync(equipe);

        var padrao = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes/preferencias", token: membro);
        var salvas = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/notificacoes/preferencias", new
        {
            categorias = new[] { new { categoria = "NOVA_SOLICITACAO", email = false } },
        }, membro);
        await Cenarios.SolicitacaoAsync(equipe, cliente);

        padrao.GetProperty("categorias").GetArrayLength().Should().Be(5);
        padrao.GetProperty("categorias").EnumerateArray().Should().OnlyContain(c => c.GetProperty("email").GetBoolean());
        salvas.GetProperty("categorias").EnumerateArray()
            .Single(c => c.GetProperty("categoria").GetString() == "NOVA_SOLICITACAO").GetProperty("email").GetBoolean().Should().BeFalse();
        salvas.GetProperty("categorias").EnumerateArray()
            .Single(c => c.GetProperty("categoria").GetString() == "SLA").GetProperty("email").GetBoolean().Should().BeTrue();
        api.Emails.Para(email).Should().NotContain(m => m.Categoria == "aviso-nova-solicitacao");
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: membro))
            .GetProperty("naoLidas").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Contato_NaoAcessa()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);

        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, "/api/v1/notificacoes", token: contato.Token);
    }
}
