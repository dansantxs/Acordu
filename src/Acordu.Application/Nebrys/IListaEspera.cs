using Acordu.Domain.Entities;

namespace Acordu.Application.Nebrys;

/// <summary>
/// Entradas da lista de espera vistas pelas ações da área da Nebrys (F14-76, F14-77; design D12). Tabela global, fora dos
/// tenants (ADR 0014, item 7). Grava no próximo <c>SalvarAsync</c> da unidade de trabalho, com a trilha.
/// </summary>
public interface IListaEspera
{
    /// <summary>A entrada, rastreada para a gravação; nula se não existe (ou já foi descartada).</summary>
    Task<EntradaListaEspera?> ObterAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Apaga a entrada (F14-77); o convite que saiu dela fica, sem a entrada de origem.</summary>
    void Remover(EntradaListaEspera entrada);
}
