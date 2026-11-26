using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F15, F15a (F15-60, F15-61; design D8; ADR 0016, item 6): cada mensagem da conversa traz o canal por onde
/// entrou, na conversa da equipe e na do portal. A mensagem por e-mail é gravada direto no banco, porque a entrada por
/// e-mail só chega no F15f.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class CanalDasMensagensTests(ApiFixture api)
{
    private const string Portal = "/api/v1/portal";

    private sealed record Conversa(Equipe Equipe, ContatoComAcesso Contato, Guid Solicitacao);

    /// <summary>Resposta da equipe, nota interna, resposta pelo portal e uma mensagem do contato pelo canal E-mail.</summary>
    private async Task<Conversa> ConversaComOsTresCanaisAsync()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);
        var rota = $"/api/v1/solicitacoes/{solicitacao}/interacoes";

        var daEquipe = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, rota,
            new { texto = "Estamos vendo o forno", visibilidade = "EXTERNA" });
        var nota = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, rota,
            new { texto = "Cliente pediu desconto", visibilidade = "INTERNA" });
        var peloPortal = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post,
            $"{Portal}/solicitacoes/{solicitacao}/interacoes", new { texto = "Segue o print" }, contato.Token);
        daEquipe.GetProperty("canal").GetString().Should().Be("ACORDU");
        nota.GetProperty("canal").GetString().Should().Be("ACORDU");
        peloPortal.GetProperty("canal").GetString().Should().Be("PORTAL");

        await using var db = api.DaEquipe(equipe.TenantId);
        db.Add(Interacao.Registrar(
            Guid.CreateVersion7(), equipe.TenantId, cliente, solicitacao, contato.Id, autorEhCliente: true,
            "Mandei também por e-mail", Visibilidade.Externa, CanalMensagem.Email, [],
            new PoliticaAnexos(1024, ["image/png"], 5), api.Relogio.Agora));
        await db.SaveChangesAsync();
        return new Conversa(equipe, contato, solicitacao);
    }

    private static Dictionary<string, JsonElement> PorTexto(JsonElement interacoes) =>
        interacoes.EnumerateArray().ToDictionary(i => i.GetProperty("texto").GetString()!, i => i);

    [Fact] // F15-60
    public async Task InteracoesDaEquipe_MensagensDosTresCanais_CadaUmaComOSeuCanal()
    {
        var conversa = await ConversaComOsTresCanaisAsync();

        var interacoes = PorTexto(await conversa.Equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{conversa.Solicitacao}/interacoes"));

        interacoes.Should().HaveCount(4);
        interacoes["Estamos vendo o forno"].GetProperty("canal").GetString().Should().Be("ACORDU");
        interacoes["Cliente pediu desconto"].GetProperty("canal").GetString().Should().Be("ACORDU");
        interacoes["Segue o print"].GetProperty("canal").GetString().Should().Be("PORTAL");
        var porEmail = interacoes["Mandei também por e-mail"];
        porEmail.GetProperty("canal").GetString().Should().Be("EMAIL");
        porEmail.GetProperty("autorEhCliente").GetBoolean().Should().BeTrue();
        porEmail.GetProperty("autor").GetProperty("nome").GetString().Should().Be("Marina Souza");
    }

    [Fact] // F15-61
    public async Task InteracoesDoPortal_SoAsExternas_CadaUmaComOSeuCanal()
    {
        var conversa = await ConversaComOsTresCanaisAsync();

        using var resposta = await conversa.Equipe.EnviarAsync(
            HttpMethod.Get, $"{Portal}/solicitacoes/{conversa.Solicitacao}/interacoes", token: conversa.Contato.Token);
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        texto.Should().NotContain("Cliente pediu desconto", "nota interna nunca chega ao portal (RF-POR-04)");
        var interacoes = PorTexto(JsonDocument.Parse(texto).RootElement);
        interacoes.Should().HaveCount(3);
        interacoes["Estamos vendo o forno"].GetProperty("canal").GetString().Should().Be("ACORDU");
        interacoes["Estamos vendo o forno"].GetProperty("daEquipe").GetBoolean().Should().BeTrue();
        interacoes["Segue o print"].GetProperty("canal").GetString().Should().Be("PORTAL");
        interacoes["Mandei também por e-mail"].GetProperty("canal").GetString().Should().Be("EMAIL");
        interacoes["Mandei também por e-mail"].GetProperty("daEquipe").GetBoolean().Should().BeFalse();
    }

    [Fact] // F15-60, F15-61
    public async Task AbrirNoPortalComAnexo_AMensagemDaAberturaSaiNoCanalPortal()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);

        using var formulario = new MultipartFormDataContent();
        formulario.Add(new StringContent("Forno quebrado"), "titulo");
        formulario.Add(new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2]), "anexos", "forno.png");
        using var http = api.NovoHttp();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri($"{Portal}/solicitacoes", UriKind.Relative))
        {
            Content = formulario,
        };
        pedido.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", contato.Token);
        using var aberta = await http.SendAsync(pedido);
        var corpo = await aberta.Content.ReadAsStringAsync();
        aberta.StatusCode.Should().Be(HttpStatusCode.Created, "corpo: {0}", corpo);
        var solicitacao = Cenarios.Id(JsonDocument.Parse(corpo).RootElement);

        var daEquipe = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/interacoes");
        daEquipe.GetArrayLength().Should().Be(1);
        daEquipe[0].GetProperty("canal").GetString().Should().Be("PORTAL");
        var doPortal = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes/{solicitacao}/interacoes", token: contato.Token);
        doPortal[0].GetProperty("canal").GetString().Should().Be("PORTAL");
    }

    [Fact] // F15-61 (isolamento)
    public async Task InteracoesDoPortal_ContatoDeOutroCliente_404()
    {
        var conversa = await ConversaComOsTresCanaisAsync();
        var outroCliente = await Cenarios.ClienteAsync(conversa.Equipe, "Outra Padaria");
        var intruso = await ContatosDoPortal.CriarAsync(api, conversa.Equipe, outroCliente, nome: "Intruso Lima");

        await conversa.Equipe.EsperarAsync(
            HttpStatusCode.NotFound, HttpMethod.Get, $"{Portal}/solicitacoes/{conversa.Solicitacao}/interacoes",
            token: intruso.Token);
    }
}
