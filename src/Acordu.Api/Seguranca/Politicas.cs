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

    /// <summary>Sessão de uma pessoa com conta (tem <c>sub</c>): exclui a sessão de link, que não tem usuário.</summary>
    public const string Usuario = "usuario";

    public static void Registrar(AuthorizationOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        opcoes.AddPolicy(Equipe, p => p
            .RequireAuthenticatedUser()
            .RequireAssertion(c => !c.User.HasClaim(claim => claim.Type == ClaimsAcordu.Cliente)));
        opcoes.AddPolicy(Portal, p => p
            .RequireAuthenticatedUser()
            .RequireClaim(ClaimsAcordu.Cliente));
        opcoes.AddPolicy(Usuario, p => p
            .RequireAuthenticatedUser()
            .RequireClaim(ClaimsAcordu.Usuario));
    }

    /// <summary>Rota do portal liberada também à sessão de link (✓L no contrato §12); as demais respondem 403 a ela.</summary>
    public static TBuilder PermitirLink<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new PermiteLink());

    /// <summary>Exige a permissão do catálogo (<c>Domain.Acesso.Permissoes</c>) no token.</summary>
    public static TBuilder RequirePermissao<TBuilder>(this TBuilder builder, string permissao)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(p => p.RequireAuthenticatedUser().RequireClaim(ClaimsAcordu.Permissoes, permissao));
}

/// <summary>Marca de rota do portal acessível pela sessão de link.</summary>
internal sealed class PermiteLink;
