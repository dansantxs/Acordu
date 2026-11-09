using Acordu.Application.Common;

namespace Acordu.Application.Apontamentos;

/// <summary>Leituras de apontamentos e do cronômetro, sob os filtros do contexto.</summary>
public interface IConsultasApontamento
{
    Task<ResultadoPaginado<ApontamentoDto>> ListarAsync(
        FiltroApontamentos filtro, Paginacao paginacao, CancellationToken cancellationToken = default);

    Task<ApontamentoDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Id do cronômetro ativo do usuário (no máximo um, RF-APT-02).</summary>
    Task<Guid?> CronometroDoUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task<CronometroDto?> CronometroAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Todos os cronômetros ativos do tenant do contexto (F12-18).</summary>
    Task<IReadOnlyList<CronometroAtivo>> CronometrosAtivosAsync(CancellationToken cancellationToken = default);
}
