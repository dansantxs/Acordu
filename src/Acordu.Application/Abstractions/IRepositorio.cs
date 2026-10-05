using Acordu.Domain.Common;

namespace Acordu.Application.Abstractions;

/// <summary>
/// Acesso a uma raiz de agregado, sempre sob os filtros de tenant e cliente do contexto. Consultas específicas entram em
/// repositórios próprios junto com os casos de uso que precisarem delas.
/// </summary>
public interface IRepositorio<T>
    where T : EntidadeBase
{
    /// <summary>A raiz completa (com suas entidades filhas), ou nula se não existe ou não é visível no contexto.</summary>
    Task<T?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Adicionar(T entidade);

    void Remover(T entidade);
}
