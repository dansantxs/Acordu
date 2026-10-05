using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Contratos;

/// <summary>
/// Consumo de um contrato no período (read side §3). Minutos inteiros. Por tipo:
/// MENSAL_FIXO: saldo e percentual do mês, nulos sem franquia (franquia não acumula entre meses);
/// BANCO_HORAS: consumo do mês para detalhe, mas saldo e percentual acumulados desde o início do contrato;
/// AVULSO: só consumido e não faturável.
/// </summary>
public sealed record ConsumoContratoDto(
    Guid ContratoId,
    TipoContrato Tipo,
    MesReferencia Periodo,
    int? FranquiaMinutos,
    int ConsumidoMinutos,
    int NaoFaturavelMinutos,
    int AjustesMinutos,
    int? ConsumidoAcumuladoMinutos,
    int? SaldoMinutos,
    int? Percentual);
