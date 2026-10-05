namespace Acordu.Application.Abstractions;

/// <summary>
/// Número de protocolo sequencial por tenant (RF-SOL-02, modelo §3.5). Reserva o número na transação da unidade de
/// trabalho: o commit confirma, o rollback devolve — sem buracos nem duplicatas.
/// </summary>
public interface IGeradorProtocolo
{
    Task<int> ProximoNumeroSolicitacaoAsync(CancellationToken cancellationToken = default);
}
