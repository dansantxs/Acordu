using Acordu.Application.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Portal;

/// <summary>
/// Leituras do portal, sempre pelos filtros globais do contato (tenant e cliente) e, nas solicitações, pelo escopo:
/// PROPRIAS só enxerga aquelas em que é o contato. Solicitação fora do escopo é nula (404), como a de outro cliente.
/// <c>PodeAprovar</c> sai pelo status; a permissão é aplicada por <see cref="LeituraPortal"/>.
/// </summary>
public interface IConsultasPortal
{
    Task<PortalEuDto?> EuAsync(ContatoDoPortal contato, CancellationToken cancellationToken = default);

    Task<ResultadoPaginado<SolicitacaoPortalResumoDto>> ListarSolicitacoesAsync(
        ContatoDoPortal contato, IReadOnlyCollection<StatusExterno>? status, Paginacao paginacao, CancellationToken cancellationToken = default);

    Task<SolicitacaoPortalDto?> DetalharSolicitacaoAsync(ContatoDoPortal contato, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Só as EXTERNAS, em ordem cronológica; nulo se a solicitação não é visível.</summary>
    Task<IReadOnlyList<InteracaoPortalDto>?> InteracoesAsync(
        ContatoDoPortal contato, Guid solicitacaoId, CancellationToken cancellationToken = default);

    /// <summary>Contratos ATIVOS do cliente vigentes em <paramref name="hoje"/>.</summary>
    Task<IReadOnlyList<ContratoPortalDto>> ContratosAsync(DateOnly hoje, CancellationToken cancellationToken = default);

    /// <summary>Nulo se o contrato não é visível.</summary>
    Task<SaldoContratoPortal?> SaldoAsync(
        Guid contratoId, MesReferencia periodo, DateOnly hoje, CancellationToken cancellationToken = default);
}
