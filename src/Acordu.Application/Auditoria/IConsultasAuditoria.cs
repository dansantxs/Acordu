using Acordu.Application.Common;

namespace Acordu.Application.Auditoria;

/// <summary>Leitura da trilha do tenant do contexto, mais recentes primeiro.</summary>
public interface IConsultasAuditoria
{
    Task<ResultadoPaginado<RegistroAuditoriaDto>> ListarAsync(
        FiltroAuditoria filtro, Paginacao paginacao, CancellationToken cancellationToken = default);
}
