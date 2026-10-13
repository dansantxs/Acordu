using Acordu.Application.Common;
using Acordu.Domain.Enums;

namespace Acordu.Application.Recorrencias;

/// <summary>Leituras de recorrências. Nulo quando não existe ou não é visível no contexto.</summary>
public interface IConsultasRecorrencia
{
    Task<ResultadoPaginado<RecorrenciaResumoDto>> ListarAsync(
        Guid? clienteId, StatusRecorrencia? status, Paginacao paginacao, CancellationToken cancellationToken = default);

    Task<RecorrenciaDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default);
}
