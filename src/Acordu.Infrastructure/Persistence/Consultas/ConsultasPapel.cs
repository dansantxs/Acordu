using Acordu.Application.Papeis;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>Papéis do tenant do contexto, com a contagem de vínculos.</summary>
public sealed class ConsultasPapel : IConsultasPapel
{
    private static readonly string[] OrdemPadrao = [Papel.NomeDono, Papel.NomeMembro, Papel.NomeLeitor, Papel.NomeCliente];

    private readonly AcorduDbContext _db;

    public ConsultasPapel(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<PapelDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var papeis = await _db.Papeis.AsNoTracking().ToListAsync(cancellationToken);
        var pessoas = await _db.Vinculos.AsNoTracking()
            .GroupBy(v => v.PapelId)
            .Select(g => new { PapelId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(g => g.PapelId, g => g.Total, cancellationToken);
        return [.. papeis
            .OrderBy(p => p.Padrao ? Array.IndexOf(OrdemPadrao, p.Nome) : int.MaxValue)
            .ThenBy(p => p.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => Dto(p, pessoas.GetValueOrDefault(p.Id)))];
    }

    public async Task<PapelDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var papel = await _db.Papeis.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        return papel is null ? null : Dto(papel, await _db.Vinculos.CountAsync(v => v.PapelId == id, cancellationToken));
    }

    public Task<bool> NomeEmUsoAsync(string nome, Guid? ignorar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nome);
        // ILIKE sem curingas: igualdade sem diferenciar maiúsculas.
        var padrao = nome.Trim().Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
        return _db.Papeis.AnyAsync(
            p => (ignorar == null || p.Id != ignorar) && EF.Functions.ILike(p.Nome, padrao), cancellationToken);
    }

    private static PapelDto Dto(Papel papel, int pessoas) =>
        new(papel.Id, papel.Nome, papel.Tipo, papel.Padrao, [.. papel.Permissoes], pessoas);
}
