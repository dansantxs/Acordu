using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Os documentos OpenAPI versionados em <c>openapi/</c> (fonte dos tipos do front, convenções §4) batem com a API.
/// Divergiu: rode com <c>ACORDU_ATUALIZAR_OPENAPI=1</c> (ou peça pelo workflow OpenAPI) e regenere os tipos do front.
/// As rotas de teste do fixture (<c>/teste…</c>) e os schemas que só elas usam ficam de fora.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class ContratoOpenApiTests(ApiFixture api)
{
    // Acentos legíveis no arquivo (o JSON continua válido; ninguém o embute em HTML).
    private static readonly JsonSerializerOptions Indentado = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Theory]
    [InlineData("interno")]
    [InlineData("portal")]
    public async Task Documento_BateComOVersionado(string documento)
    {
        using var cliente = api.NovoHttp();
        using var resposta = await cliente.GetAsync(new Uri($"/openapi/{documento}.json", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var gerado = Normalizar(JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!.AsObject());
        var arquivo = Path.Combine(RaizDoRepositorio(), "openapi", $"{documento}.json");

        if (Environment.GetEnvironmentVariable("ACORDU_ATUALIZAR_OPENAPI") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
            await File.WriteAllTextAsync(arquivo, gerado);
            return;
        }

        File.Exists(arquivo).Should().BeTrue("o contrato {0} precisa estar versionado em openapi/", documento);
        (await File.ReadAllTextAsync(arquivo)).ReplaceLineEndings("\n").Should().Be(
            gerado, "o contrato mudou: rode os testes com ACORDU_ATUALIZAR_OPENAPI=1 e depois pnpm api:tipos no apps/web");
    }

    private static string Normalizar(JsonObject documento)
    {
        if (documento["paths"] is JsonObject caminhos)
        {
            foreach (var chave in caminhos.Select(c => c.Key).Where(EhDeTeste).ToList())
                caminhos.Remove(chave);
        }

        RemoverSchemasSemUso(documento);
        return documento.ToJsonString(Indentado).ReplaceLineEndings("\n") + "\n";
    }

    private static bool EhDeTeste(string caminho) =>
        caminho.Split('/').Any(parte => parte == "teste" || parte.StartsWith("teste-", StringComparison.Ordinal));

    /// <summary>Mantém só os schemas alcançáveis a partir dos caminhos que ficaram.</summary>
    private static void RemoverSchemasSemUso(JsonObject documento)
    {
        if (documento["components"]?["schemas"] is not JsonObject schemas)
            return;
        var usados = new HashSet<string>(StringComparer.Ordinal);
        var pendentes = new Stack<JsonNode?>();
        pendentes.Push(documento["paths"]);
        while (pendentes.Count > 0)
        {
            switch (pendentes.Pop())
            {
                case JsonObject objeto:
                    foreach (var (chave, valor) in objeto)
                    {
                        if (chave == "$ref" && valor?.GetValue<string>() is { } referencia
                            && referencia.StartsWith("#/components/schemas/", StringComparison.Ordinal))
                        {
                            var nome = referencia["#/components/schemas/".Length..];
                            if (usados.Add(nome))
                                pendentes.Push(schemas[nome]);
                        }
                        else
                        {
                            pendentes.Push(valor);
                        }
                    }

                    break;
                case JsonArray lista:
                    foreach (var item in lista)
                        pendentes.Push(item);
                    break;
            }
        }

        foreach (var nome in schemas.Select(s => s.Key).Where(n => !usados.Contains(n)).ToList())
            schemas.Remove(nome);
    }

    private static string RaizDoRepositorio()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
        {
            if (File.Exists(Path.Combine(pasta.FullName, "Acordu.sln")))
                return pasta.FullName;
        }

        throw new InvalidOperationException("Raiz do repositório (Acordu.sln) não encontrada.");
    }
}
