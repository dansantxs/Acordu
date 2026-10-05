using Acordu.Application.Abstractions;

namespace Acordu.Infrastructure.MultiTenancy;

/// <summary>
/// Implementação única de <see cref="ITenantContext"/>, por escopo. Começa vazia (sem tenant: nada visível, nada
/// gravável) e é definida uma única vez — pela Api a partir do token, ou por um job para o tenant que está processando.
/// </summary>
public sealed class ContextoTenant : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public bool EhCliente { get; private set; }

    public Guid? ClienteId { get; private set; }

    /// <summary>Equipe do prestador (ou o sistema agindo dentro de um tenant).</summary>
    public void DefinirEquipe(Guid tenantId) => Definir(tenantId, null);

    /// <summary>Contato de um cliente (papel Cliente): ativa o isolamento por cliente.</summary>
    public void DefinirCliente(Guid tenantId, Guid clienteId)
    {
        if (clienteId == Guid.Empty)
            throw new ArgumentException("O ClienteId não pode ser vazio.", nameof(clienteId));
        Definir(tenantId, clienteId);
    }

    private void Definir(Guid tenantId, Guid? clienteId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("O TenantId não pode ser vazio.", nameof(tenantId));
        if (TenantId is not null)
            throw new InvalidOperationException("O contexto de tenant já foi definido neste escopo.");

        TenantId = tenantId;
        EhCliente = clienteId is not null;
        ClienteId = clienteId;
    }
}
