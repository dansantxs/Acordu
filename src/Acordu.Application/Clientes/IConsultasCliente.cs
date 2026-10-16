using Acordu.Application.Common;

namespace Acordu.Application.Clientes;

/// <summary>Leituras de clientes sob os filtros do contexto.</summary>
public interface IConsultasCliente
{
    /// <param name="busca">Trecho do nome, da razão social ou do documento (sem diferenciar maiúsculas).</param>
    /// <param name="etiqueta">Etiqueta exata (como aparece em <see cref="EtiquetasAsync"/>).</param>
    Task<ResultadoPaginado<ClienteResumoDto>> ListarAsync(
        string? busca,
        bool? ativo,
        string? etiqueta,
        Guid? responsavelId,
        Paginacao paginacao,
        CancellationToken cancellationToken = default);

    /// <summary>Etiquetas em uso nos clientes do tenant, em ordem alfabética (para sugerir e filtrar).</summary>
    Task<IReadOnlyList<string>> EtiquetasAsync(CancellationToken cancellationToken = default);

    /// <summary>Nulo se não existe ou não é visível.</summary>
    Task<ClienteDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default);
}
