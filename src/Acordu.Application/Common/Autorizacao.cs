using Acordu.Application.Abstractions;

namespace Acordu.Application.Common;

/// <summary>Checagem de permissão por caso de uso (convenções §5): a rota filtra primeiro, a Application decide.</summary>
public static class Autorizacao
{
    public static void Exigir(this ITenantContext contexto, string permissao)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        if (!contexto.Permissoes.Contains(permissao))
            throw new AcessoNegadoException();
    }

    /// <summary>Usuário autenticado do contexto (autor de ajustes, apontamentos...).</summary>
    public static Guid Usuario(this ITenantContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        return contexto.UsuarioId ?? throw new AcessoNegadoException("Operação exige um usuário autenticado.");
    }
}
