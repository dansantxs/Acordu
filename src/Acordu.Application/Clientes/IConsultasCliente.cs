using Acordu.Application.Common;

namespace Acordu.Application.Clientes;

/// <summary>Leituras de clientes sob os filtros do contexto.</summary>
public interface IConsultasCliente
{
    /// <param name="busca">Trecho do nome ou do documento (sem diferenciar maiúsculas).</param>
    Task<ResultadoPaginado<ClienteResumoDto>> ListarAsync(
        string? busca, bool? ativo, Paginacao paginacao, CancellationToken cancellationToken = default);

    /// <summary>Nulo se não existe ou não é visível.</summary>
    Task<ClienteDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default);
}
