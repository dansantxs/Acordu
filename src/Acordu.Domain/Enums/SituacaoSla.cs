namespace Acordu.Domain.Enums;

/// <summary>
/// Situação do SLA num instante (ADR 0003, read side §5). CUMPRIDO só existe depois de resolvida;
/// SEM_SLA quando a solicitação não tem contrato.
/// </summary>
public enum SituacaoSla
{
    SemSla = 1,
    NoPrazo = 2,
    EmRisco = 3,
    Violado = 4,
    Cumprido = 5,
}
