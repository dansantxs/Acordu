using Acordu.Application.Categorias;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>Categorias do tenant, pelos filtros globais.</summary>
public sealed class ConsultasCategoria : IConsultasCategoria
{
    private readonly AcorduDbContext _db;

    public ConsultasCategoria(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<CategoriaDto>> ListarAsync(bool? ativa, CancellationToken cancellationToken = default) =>
        await _db.Categorias.AsNoTracking()
            .Where(c => ativa == null || c.Ativa == ativa)
            .OrderBy(c => c.Nome)
            .Select(c => new CategoriaDto(c.Id, c.Nome, c.Ativa))
            .ToListAsync(cancellationToken);

    public Task<CategoriaDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Categorias.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoriaDto(c.Id, c.Nome, c.Ativa))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> NomeEmUsoAsync(string nome, Guid? ignorar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nome);
        // ILIKE sem curingas: igualdade sem diferenciar maiúsculas.
        var padrao = nome.Trim().Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
        return _db.Categorias.AnyAsync(
            c => (ignorar == null || c.Id != ignorar) && EF.Functions.ILike(c.Nome, padrao), cancellationToken);
    }
}
