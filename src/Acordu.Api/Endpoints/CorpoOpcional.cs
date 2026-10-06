using System.Text.Json;

namespace Acordu.Api.Endpoints;

/// <summary>
/// Corpo JSON opcional lido à mão: requisição sem corpo (ou sem JSON) vale como nulo. O binding de corpo opcional do
/// minimal API pode responder 415 quando falta o <c>Content-Type</c>.
/// </summary>
internal static class CorpoOpcional
{
    public static async Task<T?> LerAsync<T>(HttpRequest requisicao, CancellationToken cancellationToken)
        where T : class
    {
        if (!requisicao.HasJsonContentType())
            return null;
        try
        {
            return await requisicao.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new BadHttpRequestException("Corpo da requisição inválido.", ex);
        }
    }
}
