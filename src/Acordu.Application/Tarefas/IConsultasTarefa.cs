using Acordu.Application.Common;

namespace Acordu.Application.Tarefas;

/// <summary>Leituras de tarefas sob os filtros do contexto.</summary>
public interface IConsultasTarefa
{
    Task<ResultadoPaginado<TarefaResumoDto>> ListarAsync(FiltroTarefas filtro, Paginacao paginacao, CancellationToken cancellationToken = default);

    Task<TarefaDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Ids das tarefas A_FAZER ou FAZENDO da solicitação (F11c), sob os filtros do contexto.</summary>
    Task<IReadOnlyList<Guid>> AbertasDaSolicitacaoAsync(Guid solicitacaoId, CancellationToken cancellationToken = default);
}
