using Acordu.Application.Equipe;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>Convites pendentes de um e-mail no tenant do contexto, rastreados.</summary>
public sealed class ConvitesEquipe : IConvitesEquipe
{
    private readonly AcorduDbContext _db;

    public ConvitesEquipe(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<ConviteEquipe>> PendentesAsync(string email, CancellationToken cancellationToken = default) =>
        await _db.ConvitesEquipe
            .Where(c => c.Email == email && c.AceitoEm == null && c.RevogadoEm == null)
            .ToListAsync(cancellationToken);
}
