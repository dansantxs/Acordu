using System.Globalization;
using Microsoft.Net.Http.Headers;

namespace Acordu.Api.Endpoints;

/// <summary>Leitura do <c>If-Match</c> (convenções §4): a versão recebida no recurso, com ou sem aspas.</summary>
internal static class Versoes
{
    public static string? IfMatch(HttpRequest requisicao)
    {
        var valor = requisicao.Headers[HeaderNames.IfMatch].ToString().Trim();
        if (valor.Length == 0)
            return null;
        if (valor.StartsWith("W/", StringComparison.Ordinal))
            valor = valor[2..];
        valor = valor.Trim('"');
        return uint.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out _)
            ? valor
            : throw new BadHttpRequestException("If-Match deve trazer a versão recebida no recurso.");
    }
}
