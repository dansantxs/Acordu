using Acordu.Application.Common;
using Acordu.Application.Equipe;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>Equipe (vínculos sem cliente) e convites pendentes do tenant do contexto.</summary>
public sealed class ConsultasEquipe : IConsultasEquipe
{
    private readonly AcorduDbContext _db;

    public ConsultasEquipe(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<UsuarioEquipeDto>> UsuariosAsync(CancellationToken cancellationToken = default) =>
        await Equipe(null).ToListAsync(cancellationToken);

    public Task<UsuarioEquipeDto?> UsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        Equipe(usuarioId).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ConviteEquipeDto>> ConvitesPendentesAsync(CancellationToken cancellationToken = default) =>
        await (from c in _db.ConvitesEquipe.AsNoTracking()
               join p in _db.Papeis on c.PapelId equals p.Id
               where c.AceitoEm == null && c.RevogadoEm == null
               orderby c.CriadoEm descending, c.Id descending
               select new ConviteEquipeDto(c.Id, c.Email, new Referencia(p.Id, p.Nome), c.CriadoEm, c.ExpiraEm))
            .ToListAsync(cancellationToken);

    private IQueryable<UsuarioEquipeDto> Equipe(Guid? usuarioId) =>
        from v in _db.Vinculos.AsNoTracking()
        join p in _db.Papeis on v.PapelId equals p.Id
        join u in _db.Users on v.UsuarioId equals u.Id
        where v.ClienteId == null && (usuarioId == null || v.UsuarioId == usuarioId)
        orderby u.Nome, u.Id
        select new UsuarioEquipeDto(
            u.Id, u.Nome, u.Email ?? string.Empty, new Referencia(p.Id, p.Nome), p.Padrao && p.Nome == Domain.Entities.Papel.NomeDono);
}
