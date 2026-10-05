using Acordu.Application.Abstractions;
using Acordu.Domain.Common;

namespace Acordu.Infrastructure.Persistence;

/// <summary>Repositório genérico de raízes; os filtros globais do contexto valem em toda consulta.</summary>
public sealed class Repositorio<T> : IRepositorio<T>
    where T : EntidadeBase
{
    private readonly AcorduDbContext _db;

    public Repositorio(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <summary>
    /// Procura primeiro no change tracker (acha raízes adicionadas e ainda não salvas, como num manipulador de evento na
    /// mesma transação) e depois no banco, com os filtros globais.
    /// </summary>
    public Task<T?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Set<T>().FindAsync([id], cancellationToken).AsTask();

    public void Adicionar(T entidade) => _db.Set<T>().Add(entidade);

    public void Remover(T entidade) => _db.Set<T>().Remove(entidade);
}
