using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §8.1a (RF-COL-01): comentários internos de tarefa, com anexos, exclusão pelo autor e download.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class ComentariosTarefaTests(ApiFixture api)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> MultipartAsync(string rota, string token, string texto, string arquivo)
    {
        using var formulario = new MultipartFormDataContent();
        formulario.Add(new StringContent(texto), "texto");
        var parte = new ByteArrayContent(Png);
        parte.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        formulario.Add(parte, "anexos", arquivo);
        using var http = api.NovoHttp();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri(rota, UriKind.Relative)) { Content = formulario };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(pedido);
        var corpo = await resposta.Content.ReadAsStringAsync();
        return (resposta.StatusCode, corpo.Length == 0 ? default : JsonDocument.Parse(corpo).RootElement.Clone());
    }

    private async Task<HttpResponseMessage> BaixarAsync(string rota, string token)
    {
        using var http = api.NovoHttp();
        using var pedido = new HttpRequestMessage(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(pedido);
    }

    [Fact]
    public async Task Comentar_ListarBaixarEExcluirSoPeloAutor()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var leitor = await equipe.ComPapelAsync(Papel.NomeLeitor);
        var tarefa = await TarefaAsync(equipe);
        var rota = $"/api/v1/tarefas/{tarefa}/comentarios";

        var simples = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, rota, new { texto = "Mudei a fonte." });
        var (status, comAnexo) = await MultipartAsync(rota, membro, "Segue o print", "tela.png");
        await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, rota, new { texto = "  " });
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Post, rota, new { texto = "Leitor" }, leitor);
        var lista = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, rota, token: leitor);
        var anexo = comAnexo.GetProperty("anexos")[0].GetProperty("id").GetGuid();
        using var download = await BaixarAsync($"/api/v1/anexos/{anexo}/download", leitor);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Delete, $"{rota}/{Id(comAnexo)}");
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"{rota}/{Id(comAnexo)}", token: membro);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Delete, $"{rota}/{Id(simples)}", token: membro);
        var depois = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, rota);

        simples.GetProperty("texto").GetString().Should().Be("Mudei a fonte.");
        simples.GetProperty("autor").GetProperty("nome").GetString().Should().Be("Pessoa da equipe");
        status.Should().Be(HttpStatusCode.Created, "corpo: {0}", comAnexo);
        comAnexo.GetProperty("anexos")[0].GetProperty("tipoConteudo").GetString().Should().Be("image/png");
        lista.GetArrayLength().Should().Be(2);
        lista[0].GetProperty("id").GetGuid().Should().Be(Id(simples));
        download.StatusCode.Should().Be(HttpStatusCode.Redirect);
        depois.EnumerateArray().Select(Id).Should().Equal(Id(simples));
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/anexos/{anexo}/download");
    }

    [Fact]
    public async Task TarefaDeOutroTenantOuInexistente404()
    {
        var equipe = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        var tarefa = await TarefaAsync(equipe);

        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/tarefas/{tarefa}/comentarios");
        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Post, $"/api/v1/tarefas/{tarefa}/comentarios", new { texto = "Oi" });
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/tarefas/{Guid.NewGuid()}/comentarios");
    }
}
