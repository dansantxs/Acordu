using Acordu.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>Equipe do tenant do contexto (vínculos sem cliente), com o e-mail da conta.</summary>
public sealed class PessoasDaEquipe : IPessoasDaEquipe
{
    private readonly AcorduDbContext _db;

    public PessoasDaEquipe(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<PessoaDaEquipe>> ComPermissaoAsync(string permissao, CancellationToken cancellationToken = default)
    {
        // Poucos papéis por tenant: a permissão (text[]) é conferida em memória.
        var papeis = (await _db.Papeis.AsNoTracking().ToListAsync(cancellationToken))
            .Where(p => p.Possui(permissao))
            .Select(p => p.Id)
            .ToList();
        return await (from v in _db.Vinculos.AsNoTracking()
                      join u in _db.Users on v.UsuarioId equals u.Id
                      where v.ClienteId == null && papeis.Contains(v.PapelId) && u.Email != null
                      orderby u.Nome
                      select new PessoaDaEquipe(u.Id, u.Nome, u.Email!))
            .ToListAsync(cancellationToken);
    }

    public Task<PessoaDaEquipe?> ObterAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        (from v in _db.Vinculos.AsNoTracking()
         join u in _db.Users on v.UsuarioId equals u.Id
         where v.UsuarioId == usuarioId && v.ClienteId == null && u.Email != null
         select new PessoaDaEquipe(u.Id, u.Nome, u.Email!))
            .SingleOrDefaultAsync(cancellationToken);
}
