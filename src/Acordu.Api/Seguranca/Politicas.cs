using Microsoft.AspNetCore.Authorization;

namespace Acordu.Api.Seguranca;

/// <summary>
/// Superfícies da API (ADR 0002): rotas internas só para a equipe (papel Cliente → 403) e rotas do portal só para
/// contatos. Por permissão: <see cref="RequirePermissao{TBuilder}"/>. A checagem por caso de uso fica na Application.
/// </summary>
internal static class Politicas
{
    public const string Equipe = "equipe";
    public const string Portal = "portal";

    public static void Registrar(AuthorizationOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        opcoes.AddPolicy(Equipe, p => p
            .RequireAuthenticatedUser()
            .RequireAssertion(c => !c.User.HasClaim(claim => claim.Type == ClaimsAcordu.Cliente)));
        opcoes.AddPolicy(Portal, p => p
            .RequireAuthenticatedUser()
            .RequireClaim(ClaimsAcordu.Cliente));
    }

    /// <summary>Exige a permissão do catálogo (<c>Domain.Acesso.Permissoes</c>) no token.</summary>
    public static TBuilder RequirePermissao<TBuilder>(this TBuilder builder, string permissao)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(p => p.RequireAuthenticatedUser().RequireClaim(ClaimsAcordu.Permissoes, permissao));
}
