using Acordu.Application.Common;

namespace Acordu.Application.Solicitacoes;

/// <summary>Leituras de solicitações e da linha do tempo, sob os filtros do contexto.</summary>
public interface IConsultasSolicitacao
{
    /// <summary>
    /// Fila filtrada. <c>situacaoSla</c> aceita EM_RISCO e VIOLADO, resolvidos no banco pelos instantes persistidos
    /// (read side §5); <c>ordem</c>: <c>abertaEm</c>, <c>numero</c>, <c>prioridade</c>, <c>prazoResolucao</c> (prefixo
    /// <c>-</c> para decrescente).
    /// </summary>
    Task<ResultadoPaginado<SolicitacaoResumoDto>> ListarAsync(
        FiltroSolicitacoes filtro, Paginacao paginacao, CancellationToken cancellationToken = default);

    Task<SolicitacaoDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Linha do tempo em ordem de registro; nulo se a solicitação não é visível.</summary>
    Task<IReadOnlyList<InteracaoDto>?> InteracoesAsync(Guid solicitacaoId, CancellationToken cancellationToken = default);
}
