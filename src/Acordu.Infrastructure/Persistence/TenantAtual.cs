using Acordu.Application.Abstractions;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>O tenant do contexto, pelo filtro de tenant (outro tenant nunca aparece).</summary>
public sealed class TenantAtual : ITenantAtual
{
    private readonly AcorduDbContext _db;

    public TenantAtual(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task<Tenant> ObterAsync(CancellationToken cancellationToken = default) => _db.Tenants.SingleAsync(cancellationToken);
}
