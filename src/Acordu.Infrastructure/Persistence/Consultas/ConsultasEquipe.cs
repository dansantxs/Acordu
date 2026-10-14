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
        [.. (await Equipe(null).ToListAsync(cancellationToken)).Select(Dto)];

    public async Task<UsuarioEquipeDto?> UsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        await Equipe(usuarioId).SingleOrDefaultAsync(cancellationToken) is { } linha ? Dto(linha) : null;

    public async Task<IReadOnlyList<ConviteEquipeDto>> ConvitesPendentesAsync(CancellationToken cancellationToken = default) =>
        await (from c in _db.ConvitesEquipe.AsNoTracking()
               join p in _db.Papeis on c.PapelId equals p.Id
               where c.AceitoEm == null && c.RevogadoEm == null
               orderby c.CriadoEm descending, c.Id descending
               select new ConviteEquipeDto(c.Id, c.Email, new Referencia(p.Id, p.Nome), c.CriadoEm, c.ExpiraEm))
            .ToListAsync(cancellationToken);

    // A capacidade sai do banco convertida (Duracao) e vira minutos aqui; nula só em vínculo anterior à coluna.
    private static UsuarioEquipeDto Dto(LinhaEquipe l) =>
        new(l.UsuarioId, l.Nome, l.Email, l.Papel, l.Dono, (l.Capacidade ?? Domain.Entities.Vinculo.CapacidadePadrao).Minutos);

    private IQueryable<LinhaEquipe> Equipe(Guid? usuarioId) =>
        from v in _db.Vinculos.AsNoTracking()
        join p in _db.Papeis on v.PapelId equals p.Id
        join u in _db.Users on v.UsuarioId equals u.Id
        where v.ClienteId == null && (usuarioId == null || v.UsuarioId == usuarioId)
        orderby u.Nome, u.Id
        select new LinhaEquipe(
            u.Id, u.Nome, u.Email ?? string.Empty, new Referencia(p.Id, p.Nome), p.Padrao && p.Nome == Domain.Entities.Papel.NomeDono,
            v.CapacidadeSemanal);

    private sealed record LinhaEquipe(Guid UsuarioId, string Nome, string Email, Referencia Papel, bool Dono, Domain.ValueObjects.Duracao? Capacidade);
}
