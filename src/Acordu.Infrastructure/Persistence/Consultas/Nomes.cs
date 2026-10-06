using Acordu.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>Nomes de pessoas (tabela global do Identity), só para Ids que já aparecem num registro visível.</summary>
internal static class Nomes
{
    public static async Task<Dictionary<Guid, string>> DeUsuariosAsync(
        AcorduDbContext db, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0)
            return [];
        return await db.Users.AsNoTracking()
            .Where(u => lista.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nome, cancellationToken);
    }

    public static Referencia? Pessoa(Guid? id, IReadOnlyDictionary<Guid, string> nomes) =>
        id is { } valor ? new Referencia(valor, nomes.GetValueOrDefault(valor, string.Empty)) : null;
}
