using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Contratos;

/// <summary>Consumo de contrato (read side §3). Nulo quando o contrato não existe ou não é visível no contexto.</summary>
public interface IConsultasContrato
{
    /// <param name="hoje">Data de hoje no fuso do tenant: limita o saldo acumulado do banco de horas no período corrente.</param>
    Task<ConsumoContratoDto?> ConsumoAsync(
        Guid contratoId, MesReferencia periodo, DateOnly hoje, CancellationToken cancellationToken = default);
}
