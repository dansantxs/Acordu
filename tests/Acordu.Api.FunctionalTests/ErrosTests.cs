using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato da API §13: toda falha sai em Problem Details com <c>type</c> estável e <c>traceId</c>.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class ErrosTests(ApiFixture api)
{
    private const string Base = "https://acordu.app.br/erros/";

    private async Task<(HttpStatusCode Status, string? TipoConteudo, JsonElement Corpo)> GetAsync(string rota)
    {
        using var cliente = api.NovoHttp();
        using var resposta = await cliente.GetAsync(new Uri(rota, UriKind.Relative));
        return (resposta.StatusCode, resposta.Content.Headers.ContentType?.MediaType, await resposta.Content.ReadFromJsonAsync<JsonElement>());
    }

    [Theory]
    [InlineData("transicao", 422, "transicao-invalida", "Solicitação fechada não aceita interação.")]
    [InlineData("periodo", 422, "periodo-fechado", "Outubro/2026 está fechado.")]
    [InlineData("contrato", 422, "contrato-incoerente", "Contrato de outro cliente.")]
    [InlineData("concorrencia", 409, "conflito-versao", null)]
    public async Task ExcecaoConhecida_ProblemDetailsComTipo(string erro, int status, string tipo, string? detalhe)
    {
        var (codigo, conteudo, corpo) = await GetAsync($"/api/v1/teste/erro/{erro}");

        ((int)codigo).Should().Be(status);
        conteudo.Should().Be("application/problem+json");
        corpo.GetProperty("type").GetString().Should().Be(Base + tipo);
        corpo.GetProperty("status").GetInt32().Should().Be(status);
        corpo.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
        if (detalhe is not null)
            corpo.GetProperty("detail").GetString().Should().Be(detalhe);
    }

    [Fact]
    public async Task ArgumentoInvalido_400ComErroPorCampoSemSufixoDoDotNet()
    {
        var (codigo, _, corpo) = await GetAsync("/api/v1/teste/erro/argumento");

        codigo.Should().Be(HttpStatusCode.BadRequest);
        corpo.GetProperty("type").GetString().Should().Be(Base + "validacao");
        corpo.GetProperty("detail").GetString().Should().Be("O texto é obrigatório.");
        corpo.GetProperty("errors").GetProperty("nome")[0].GetString().Should().Be("O texto é obrigatório.");
    }

    [Fact]
    public async Task ExcecaoInesperada_500SemVazarDetalhe()
    {
        var (codigo, _, corpo) = await GetAsync("/api/v1/teste/erro/inesperado");

        codigo.Should().Be(HttpStatusCode.InternalServerError);
        corpo.GetProperty("type").GetString().Should().Be(Base + "erro-interno");
        corpo.GetRawText().Should().NotContain("detalhe interno");
        corpo.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CorpoMalformado_400Validacao()
    {
        using var cliente = api.NovoHttp();
        using var conteudo = new StringContent("{\"nome\": ", System.Text.Encoding.UTF8, "application/json");

        using var resposta = await cliente.PostAsync(new Uri("/api/v1/teste/eco", UriKind.Relative), conteudo);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("type").GetString().Should().Be(Base + "validacao");
    }

    [Fact]
    public async Task RotaInexistente_404EmProblemDetails()
    {
        using var cliente = api.NovoHttp();

        using var resposta = await cliente.GetAsync(new Uri("/api/v1/nao-existe", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resposta.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Enum_SerializadoEmScreamingSnake()
    {
        using var cliente = api.NovoHttp();

        using var conteudo = new StringContent("{\"nome\":\"x\",\"prioridade\":\"URGENTE\"}", System.Text.Encoding.UTF8, "application/json");

        using var resposta = await cliente.PostAsync(new Uri("/api/v1/teste/eco", UriKind.Relative), conteudo);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("\"prioridade\":\"URGENTE\"");
    }
}
