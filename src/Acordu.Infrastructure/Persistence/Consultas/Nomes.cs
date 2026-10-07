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

    /// <summary>
    /// Nomes dos autores de interações: usuários e, para quem respondeu pela sessão de link (sem conta, o autor é o
    /// contato), os contatos do cliente.
    /// </summary>
    public static async Task<Dictionary<Guid, string>> DeAutoresAsync(
        AcorduDbContext db, Guid clienteId, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var lista = ids.Distinct().ToList();
        var nomes = await DeUsuariosAsync(db, lista, cancellationToken);
        var faltando = lista.Where(id => !nomes.ContainsKey(id)).ToList();
        if (faltando.Count == 0)
            return nomes;

        var contatos = await db.Clientes.AsNoTracking()
            .Where(c => c.Id == clienteId)
            .SelectMany(c => c.Contatos)
            .Where(c => faltando.Contains(c.Id))
            .Select(c => new { c.Id, c.Nome })
            .ToListAsync(cancellationToken);
        foreach (var contato in contatos)
            nomes[contato.Id] = contato.Nome;
        return nomes;
    }

    public static Referencia? Pessoa(Guid? id, IReadOnlyDictionary<Guid, string> nomes) =>
        id is { } valor ? new Referencia(valor, nomes.GetValueOrDefault(valor, string.Empty)) : null;
}
