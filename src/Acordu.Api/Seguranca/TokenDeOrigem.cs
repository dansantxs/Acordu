using System.Security.Cryptography;
using System.Text;

namespace Acordu.Api.Seguranca;

/// <summary>
/// Borda obrigatória (ADR 0010 §3): com <c>Proxy__TokenOrigem</c> configurado, só passa requisição que traz o
/// <c>X-Origem-Token</c> certo, que o Worker de <c>api.acordu.app.br</c> acrescenta. Sem ele, 404 indistinto, antes de
/// cabeçalhos de proxy, autenticação e rate limit: a URL <c>*.run.app</c> não serve de atalho para pular a borda nem para
/// forjar o IP. O cabeçalho é retirado depois da conferência, para não chegar a logs nem a código adiante.
/// </summary>
internal static class TokenDeOrigem
{
    public const string Cabecalho = "X-Origem-Token";

    public static string? Configurado(IConfiguration configuracao)
    {
        var token = configuracao["Proxy:TokenOrigem"];
        return string.IsNullOrEmpty(token) ? null : token;
    }

    public static void UseTokenDeOrigem(this WebApplication app)
    {
        if (Configurado(app.Configuration) is not { } esperado)
            return;

        var bytesEsperados = Encoding.UTF8.GetBytes(esperado);
        app.Use(async (http, proximo) =>
        {
            var recebido = http.Request.Headers[Cabecalho].ToString();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recebido), bytesEsperados))
            {
                http.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            http.Request.Headers.Remove(Cabecalho);
            await proximo(http);
        });
    }
}
