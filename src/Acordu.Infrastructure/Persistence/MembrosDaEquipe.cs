using Acordu.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>Equipe = vínculo no tenant do contexto sem cliente (papel interno).</summary>
public sealed class MembrosDaEquipe : IMembrosDaEquipe
{
    private readonly AcorduDbContext _db;

    public MembrosDaEquipe(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task<bool> EhDaEquipeAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        _db.Vinculos.AnyAsync(v => v.UsuarioId == usuarioId && v.ClienteId == null, cancellationToken);
}
