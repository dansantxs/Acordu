namespace Acordu.Infrastructure.MultiTenancy;

/// <summary>
/// Nomes dos filtros globais (filtros nomeados do EF Core 10). Ignorar um filtro só é permitido na Infrastructure, em
/// pontos revisados (convenções §1); ignorar só o de cliente nunca libera outro tenant.
/// </summary>
public static class FiltrosGlobais
{
    /// <summary>Isolamento entre prestadores: <c>TenantId</c> do contexto. Sempre ativo.</summary>
    public const string Tenant = "tenant";

    /// <summary>Isolamento entre clientes do prestador: ativo quando o contexto é de um contato (papel Cliente).</summary>
    public const string Cliente = "cliente";
}
