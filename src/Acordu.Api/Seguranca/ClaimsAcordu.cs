using System.Security.Claims;
using System.Text.Json;
using Acordu.Domain.Enums;

namespace Acordu.Api.Seguranca;

/// <summary>Nomes das claims do access token (segurança §5.1) e leitura delas.</summary>
internal static class ClaimsAcordu
{
    public const string Usuario = "sub";
    public const string Tenant = "tenant_id";
    public const string Papel = "papel";
    public const string Permissoes = "permissoes";
    public const string Cliente = "cliente_id";
    public const string Escopo = "escopo_portal";

    /// <summary>Sessão de link (segurança §5.3): acesso que a abriu, solicitação e tipo.</summary>
    public const string AcessoPortal = "acesso_portal_id";
    public const string Solicitacao = "solicitacao_id";
    public const string Tipo = "tipo";
    public const string TipoLink = "LINK_SOLICITACAO";

    /// <summary>Mesmo formato do JSON e do banco: <c>TODAS_DO_CLIENTE</c>.</summary>
    public static string Texto(EscopoPortal escopo) => JsonNamingPolicy.SnakeCaseUpper.ConvertName(escopo.ToString());

    public static EscopoPortal? EscopoDe(string? texto) =>
        texto is null ? null : Enum.GetValues<EscopoPortal>().Single(e => Texto(e) == texto);

    public static Guid? IdDe(ClaimsPrincipal usuario, string tipo) =>
        Guid.TryParse(usuario.FindFirstValue(tipo), out var valor) ? valor : null;
}
