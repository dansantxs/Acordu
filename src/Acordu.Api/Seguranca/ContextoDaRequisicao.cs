using Acordu.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Authorization;

namespace Acordu.Api.Seguranca;

/// <summary>
/// Define o <see cref="ContextoTenant"/> da requisição a partir do token já validado: tenant, usuário, papel, permissões
/// e, para contatos, cliente e escopo. Sem token, o contexto fica vazio (nada visível, nada gravável). Rotas públicas
/// (<see cref="IAllowAnonymous"/>) ignoram um token presente: registro e aceite de convite fixam o próprio tenant, e a SPA
/// pode mandar o Bearer de outra sessão aberta no navegador. Token de link (<c>tipo = LINK_SOLICITACAO</c>) define a
/// sessão de link, sem usuário.
/// </summary>
internal static class ContextoDaRequisicao
{
    public static Task DefinirAsync(HttpContext http, Func<Task> proximo)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(proximo);
        var usuario = http.User;
        if (usuario.Identity?.IsAuthenticated != true
            || http.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || ClaimsAcordu.IdDe(usuario, ClaimsAcordu.Tenant) is not { } tenant)
        {
            return proximo();
        }

        var contexto = http.RequestServices.GetRequiredService<ContextoTenant>();
        if (usuario.FindFirst(ClaimsAcordu.Tipo)?.Value == ClaimsAcordu.TipoLink)
        {
            if (ClaimsAcordu.IdDe(usuario, ClaimsAcordu.Cliente) is { } cliente
                && ClaimsAcordu.IdDe(usuario, ClaimsAcordu.AcessoPortal) is { } acesso
                && ClaimsAcordu.IdDe(usuario, ClaimsAcordu.Solicitacao) is { } solicitacao)
            {
                contexto.DefinirLink(
                    tenant, cliente, acesso, solicitacao, usuario.FindAll(ClaimsAcordu.Permissoes).Select(c => c.Value));
            }
        }
        else if (ClaimsAcordu.IdDe(usuario, ClaimsAcordu.Usuario) is { } usuarioId)
        {
            contexto.DefinirUsuario(
                tenant,
                usuarioId,
                usuario.FindFirst(ClaimsAcordu.Papel)?.Value ?? string.Empty,
                usuario.FindAll(ClaimsAcordu.Permissoes).Select(c => c.Value),
                ClaimsAcordu.IdDe(usuario, ClaimsAcordu.Cliente),
                ClaimsAcordu.EscopoDe(usuario.FindFirst(ClaimsAcordu.Escopo)?.Value));
        }

        return proximo();
    }
}
