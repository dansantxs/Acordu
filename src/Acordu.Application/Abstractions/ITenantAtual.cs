using Acordu.Domain.Entities;

namespace Acordu.Application.Abstractions;

/// <summary>O <see cref="Tenant"/> do contexto, rastreado para edição (configurações do portal).</summary>
public interface ITenantAtual
{
    Task<Tenant> ObterAsync(CancellationToken cancellationToken = default);
}
