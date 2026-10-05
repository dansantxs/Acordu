using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Portal.Extrato;

/// <summary>
/// Extrato do contrato para o portal (RF-POR-07, read side §4). Nulo quando o contrato não é visível no contexto.
/// O total é sempre a soma das linhas exibidas.
/// </summary>
public interface IConsultaExtratoPortal
{
    Task<ExtratoContratoDto?> ExtratoAsync(Guid contratoId, MesReferencia periodo, CancellationToken cancellationToken = default);
}

/// <summary>DTO exclusivo do portal: só apontamentos faturáveis, sem campos internos.</summary>
public sealed record ExtratoContratoDto(Guid ContratoId, MesReferencia Periodo, int ConsumidoMinutos, IReadOnlyList<LinhaExtratoDto> Linhas);

/// <summary>
/// Linha do extrato. Apontamento visível: título da solicitação (ou da tarefa visível, sem solicitação) e descrição
/// externa (ou a interna). Faturável não visível: linha genérica "Atendimento", para o total continuar conferível.
/// </summary>
public sealed record LinhaExtratoDto(DateOnly Data, int? SolicitacaoNumero, string? Titulo, string Descricao, int DuracaoMinutos);
