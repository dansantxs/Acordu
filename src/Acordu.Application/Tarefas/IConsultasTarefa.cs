using Acordu.Application.Common;

namespace Acordu.Application.Tarefas;

/// <summary>Leituras de tarefas sob os filtros do contexto.</summary>
public interface IConsultasTarefa
{
    Task<ResultadoPaginado<TarefaResumoDto>> ListarAsync(FiltroTarefas filtro, Paginacao paginacao, CancellationToken cancellationToken = default);

    Task<TarefaDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default);
}
