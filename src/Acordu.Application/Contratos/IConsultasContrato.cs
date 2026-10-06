using Acordu.Application.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Contratos;

/// <summary>Leituras de contratos e consumo (read side §3). Nulo quando o contrato não existe ou não é visível no contexto.</summary>
public interface IConsultasContrato
{
    /// <param name="hoje">Data de hoje no fuso do tenant: limita o saldo acumulado do banco de horas no período corrente.</param>
    Task<ConsumoContratoDto?> ConsumoAsync(
        Guid contratoId, MesReferencia periodo, DateOnly hoje, CancellationToken cancellationToken = default);

    Task<ResultadoPaginado<ContratoResumoDto>> ListarAsync(
        Guid? clienteId, StatusContrato? status, Paginacao paginacao, CancellationToken cancellationToken = default);

    /// <summary>Contrato com SLA e o consumo do mês de <paramref name="hoje"/>.</summary>
    Task<ContratoDto?> DetalharAsync(Guid id, DateOnly hoje, CancellationToken cancellationToken = default);

    /// <summary>
    /// RF-CTR-02: existe outro contrato ATIVO do mesmo tipo para o cliente com vigência que cruza <paramref name="vigencia"/>?
    /// </summary>
    Task<bool> ExisteAtivoSobrepostoAsync(
        Guid clienteId, TipoContrato tipo, Periodo vigencia, Guid? ignorar, CancellationToken cancellationToken = default);

    /// <summary>Contratos ATIVOS do cliente vigentes em <paramref name="hoje"/> (para escolher o contrato da solicitação).</summary>
    Task<IReadOnlyList<Guid>> VigentesDoClienteAsync(Guid clienteId, DateOnly hoje, CancellationToken cancellationToken = default);
}
