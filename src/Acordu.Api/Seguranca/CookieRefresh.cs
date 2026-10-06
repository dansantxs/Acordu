namespace Acordu.Api.Seguranca;

/// <summary>
/// O refresh token viaja só em cookie HttpOnly, Secure, SameSite=Strict e restrito às rotas de auth: fora do alcance de
/// JavaScript (XSS) e de outros sites (CSRF). SPA e API ficam no mesmo site (deploy §1), então o cookie é first-party.
/// </summary>
internal static class CookieRefresh
{
    public const string Nome = "acordu_refresh";
    public const string Caminho = "/api/v1/auth";

    public static void Definir(HttpResponse resposta, string token, DateTimeOffset expiraEm) =>
        resposta.Cookies.Append(Nome, token, Opcoes(expiraEm));

    public static string? Ler(HttpRequest requisicao) => requisicao.Cookies[Nome];

    public static void Limpar(HttpResponse resposta) => resposta.Cookies.Delete(Nome, Opcoes(null));

    private static CookieOptions Opcoes(DateTimeOffset? expiraEm) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Caminho,
        Expires = expiraEm,
        IsEssential = true,
    };
}
