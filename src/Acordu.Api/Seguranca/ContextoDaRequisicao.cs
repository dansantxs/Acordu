using Acordu.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Authorization;

namespace Acordu.Api.Seguranca;

/// <summary>
/// Define o <see cref="ContextoTenant"/> da requisição a partir do token já validado: tenant, usuário, papel, permissões
/// e, para contatos, cliente e escopo. Sem token, o contexto fica vazio (nada visível, nada gravável). Rotas públicas
/// (<see cref="IAllowAnonymous"/>) ignoram um token presente: registro e aceite de convite fixam o próprio tenant, e a SPA
/// pode mandar o Bearer de outra sessão aberta no navegador.
/// </summary>
internal static class ContextoDaRequisicao
{
    public static Task DefinirAsync(HttpContext http, Func<Task> proximo)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(proximo);
        var usuario = http.User;
        if (usuario.Identity?.IsAuthenticated == true
            && http.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is null
            && ClaimsAcordu.IdDe(usuario, ClaimsAcordu.Tenant) is { } tenant
            && ClaimsAcordu.IdDe(usuario, ClaimsAcordu.Usuario) is { } usuarioId)
        {
            http.RequestServices.GetRequiredService<ContextoTenant>().DefinirUsuario(
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
