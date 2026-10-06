namespace Acordu.Api.Composicao;

/// <summary>
/// Headers de segurança da API (segurança §9). A API só responde JSON, então a CSP proíbe tudo; a página do Scalar
/// (só em Development) fica de fora, porque carrega scripts.
/// </summary>
internal static class HeadersSeguranca
{
    public static Task AplicarAsync(HttpContext http, Func<Task> proximo)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(proximo);
        var headers = http.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "same-origin";
        headers.XFrameOptions = "DENY";
        if (!http.Request.Path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase))
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        return proximo();
    }
}
