namespace Acordu.Application.Dashboard;

/// <summary>Uma consulta por bloco (read side §6), no tenant do contexto.</summary>
public interface IConsultasDashboard
{
    Task<FilaDto> FilaAsync(CancellationToken cancellationToken = default);

    /// <param name="desde">Início do período (instante, inclusive), para o cumprimento.</param>
    /// <param name="antes">Fim do período (instante, exclusivo).</param>
    Task<SlaDashboardDto> SlaAsync(DateTimeOffset agora, DateTimeOffset desde, DateTimeOffset antes, CancellationToken cancellationToken = default);

    Task<HorasDashboardDto> HorasAsync(DateOnly de, DateOnly ate, int clientes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContratoDashboardDto>> ContratosAsync(DateOnly hoje, CancellationToken cancellationToken = default);

    Task<int> TarefasVencidasAsync(DateOnly hoje, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProximaRecorrenteDto>> ProximasRecorrentesAsync(int quantidade, CancellationToken cancellationToken = default);
}
