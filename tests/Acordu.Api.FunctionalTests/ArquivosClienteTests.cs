using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §4.3 (RF-COL-05): aba de arquivos do cliente, juntando interações e comentários de tarefa.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class ArquivosClienteTests(ApiFixture api)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private async Task EnviarAsync(string rota, string token, string arquivo, params (string Campo, string Valor)[] campos)
    {
        using var formulario = new MultipartFormDataContent();
        foreach (var (campo, valor) in campos)
            formulario.Add(new StringContent(valor), campo);
        var parte = new ByteArrayContent(Png);
        parte.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        formulario.Add(parte, "anexos", arquivo);
        using var http = api.NovoHttp();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri(rota, UriKind.Relative)) { Content = formulario };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(pedido);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Arquivos_DeInteracoesEComentarios_DoMaisNovo()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var outro = await ClienteAsync(equipe, "Oficina");
        var solicitacao = await SolicitacaoAsync(equipe, cliente);
        var tarefa = await TarefaAsync(equipe, solicitacao, "Arte do banner");
        var deOutro = await SolicitacaoAsync(equipe, outro);

        await EnviarAsync($"/api/v1/solicitacoes/{solicitacao}/interacoes", equipe.Dono, "briefing.png", ("texto", "Segue"), ("visibilidade", "EXTERNA"));
        api.Relogio.Agora = api.Relogio.Agora.AddMinutes(5);
        await EnviarAsync($"/api/v1/tarefas/{tarefa}/comentarios", equipe.Dono, "rascunho.png", ("texto", "V1"));
        await EnviarAsync($"/api/v1/solicitacoes/{deOutro}/interacoes", equipe.Dono, "outro.png", ("texto", "x"), ("visibilidade", "INTERNA"));

        var arquivos = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/clientes/{cliente}/arquivos");
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/clientes/{Guid.NewGuid()}/arquivos");

        arquivos.GetProperty("total").GetInt32().Should().Be(2);
        var itens = arquivos.GetProperty("itens").EnumerateArray().ToList();
        itens.Select(i => i.GetProperty("nomeArquivo").GetString()).Should().Equal("rascunho.png", "briefing.png");
        itens[0].GetProperty("interno").GetBoolean().Should().BeTrue();
        itens[0].GetProperty("origem").GetProperty("tipo").GetString().Should().Be("TAREFA");
        itens[0].GetProperty("origem").GetProperty("titulo").GetString().Should().Be("Arte do banner");
        itens[1].GetProperty("interno").GetBoolean().Should().BeFalse();
        itens[1].GetProperty("origem").GetProperty("numero").GetInt32().Should().BePositive();
        itens[1].GetProperty("autor").GetProperty("nome").GetString().Should().Be("Pessoa da equipe");
    }
}
