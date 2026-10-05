using Acordu.Application.Abstractions;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>Quem age nos testes: sistema (sem tenant), equipe do prestador ou contato de um cliente.</summary>
internal sealed record Quem(Guid? TenantId, bool EhCliente, Guid? ClienteId) : ITenantContext
{
    public static Quem Sistema() => new(null, false, null);

    public static Quem Equipe(Guid tenantId) => new(tenantId, false, null);

    public static Quem DoCliente(Guid tenantId, Guid clienteId) => new(tenantId, true, clienteId);
}
