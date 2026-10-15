using Acordu.Application.Common;
using Acordu.Domain.Enums;

namespace Acordu.Application.Abstractions;

/// <summary>
/// Quem segue cada solicitação ou tarefa (RF-COL-03), no tenant do contexto. Fora dos agregados, como o sino: seguir é
/// idempotente e grava na hora.
/// </summary>
public interface ISeguidores
{
    Task SeguirAsync(AlvoColaboracao alvo, Guid alvoId, Guid usuarioId, CancellationToken cancellationToken = default);

    Task DeixarAsync(AlvoColaboracao alvo, Guid alvoId, Guid usuarioId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> DoAlvoAsync(AlvoColaboracao alvo, Guid alvoId, CancellationToken cancellationToken = default);

    /// <summary>Com os nomes, para a tela; por nome.</summary>
    Task<IReadOnlyList<Referencia>> ListarAsync(AlvoColaboracao alvo, Guid alvoId, CancellationToken cancellationToken = default);
}
