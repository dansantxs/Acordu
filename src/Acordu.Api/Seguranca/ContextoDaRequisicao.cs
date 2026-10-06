using Acordu.Infrastructure.MultiTenancy;

namespace Acordu.Api.Seguranca;

/// <summary>
/// Define o <see cref="ContextoTenant"/> da requisição a partir do token já validado: tenant, usuário, papel, permissões
/// e, para contatos, cliente e escopo. Sem token, o contexto fica vazio (nada visível, nada gravável).
/// </summary>
internal static class ContextoDaRequisicao
{
    public static Task DefinirAsync(HttpContext http, Func<Task> proximo)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(proximo);
        var usuario = http.User;
        if (usuario.Identity?.IsAuthenticated == true
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
