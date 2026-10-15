using System.Net;
using System.Net.Http.Headers;
using Acordu.Api.FunctionalTests.Apoio;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §11e e ADR 0009: sinais de mudança por SSE, só do próprio tenant e só para a equipe.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class TempoRealTests(ApiFixture api)
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(15);

    private sealed class Fluxo(HttpClient http, HttpResponseMessage resposta, StreamReader leitor) : IDisposable
    {
        public HttpResponseMessage Resposta { get; } = resposta;

        /// <summary>Lê linhas até achar uma com o trecho (ou estourar o limite).</summary>
        public async Task<string> EsperarAsync(string trecho)
        {
            using var limite = new CancellationTokenSource(Limite);
            while (true)
            {
                var linha = await leitor.ReadLineAsync(limite.Token) ?? throw new InvalidOperationException("Fluxo fechado.");
                if (linha.Contains(trecho, StringComparison.Ordinal))
                    return linha;
            }
        }

        public void Dispose()
        {
            leitor.Dispose();
            Resposta.Dispose();
            http.Dispose();
        }
    }

    private async Task<Fluxo> AbrirAsync(string token)
    {
        var http = api.NovoHttp();
        using var pedido = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/eventos", UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resposta = await http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead);
        var leitor = new StreamReader(await resposta.Content.ReadAsStreamAsync());
        return new Fluxo(http, resposta, leitor);
    }

    [Fact]
    public async Task Mudanca_ChegaComoSinal_SoNoProprioTenant()
    {
        var equipe = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        using var meu = await AbrirAsync(equipe.Dono);
        using var deOutro = await AbrirAsync(outra.Dono);
        await meu.EsperarAsync("retry:");
        await deOutro.EsperarAsync("retry:");

        var tarefa = await TarefaAsync(equipe);
        var linha = await meu.EsperarAsync(tarefa.ToString());
        var tarefaDoOutro = await TarefaAsync(outra);
        var primeiraDoOutro = await deOutro.EsperarAsync("data:");

        meu.Resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        meu.Resposta.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        linha.Should().Be($"data: {{\"tipo\":\"tarefa\",\"id\":\"{tarefa}\"}}");
        primeiraDoOutro.Should().Contain(tarefaDoOutro.ToString()).And.NotContain(tarefa.ToString());
    }

    [Fact]
    public async Task ContatoDoPortal403()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);

        using var fluxo = await AbrirAsync(contato.Token);

        fluxo.Resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
