using Acordu.Domain.Entities;

namespace Acordu.Application.ListaEspera;

/// <summary>
/// Gravação das entradas que chegam pela página pública (F14-52, F14-53; design do F14, D12). Tabela global, fora dos
/// tenants (ADR 0014, item 7): só esta feature, a área da Nebrys e as consultas dela usam o tipo (teste de arquitetura).
/// </summary>
public interface IInscricoesListaEspera
{
    /// <summary>
    /// Grava a entrada se o e-mail dela ainda não está na lista; se já está, não grava nem muda a que existe (o índice único
    /// do e-mail decide, inclusive entre dois envios ao mesmo tempo). Quem chama não distingue os dois caminhos (F14-53).
    /// </summary>
    Task GravarSeNovaAsync(EntradaListaEspera entrada, CancellationToken cancellationToken = default);
}
