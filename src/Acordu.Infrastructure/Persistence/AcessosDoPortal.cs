using Acordu.Application.AcessosPortal;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>Consultas do acesso ao portal, rastreadas e sob os filtros do contexto (tenant da equipe).</summary>
public sealed class AcessosDoPortal : IAcessosDoPortal
{
    private readonly AcorduDbContext _db;

    public AcessosDoPortal(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<ConvitePortal>> ConvitesPendentesAsync(Guid contatoId, CancellationToken cancellationToken = default) =>
        await _db.ConvitesPortal
            .Where(c => c.ContatoId == contatoId && c.AceitoEm == null && c.RevogadoEm == null)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AcessoPortal>> LinksNaoRevogadosAsync(Guid contatoId, CancellationToken cancellationToken = default) =>
        await _db.AcessosPortal
            .Where(a => a.ContatoId == contatoId && a.RevogadoEm == null)
            .ToListAsync(cancellationToken);
}
