namespace Acordu.Application.Abstractions.Eventos;

/// <summary>
/// Quem causou o evento sendo entregue a um <see cref="IManipuladorPosCommit{TEvento}"/>: o usuário da requisição que
/// gravou a outbox (nulo em job ou ação sem usuário). Vale também nas novas tentativas, porque fica gravado na mensagem.
/// </summary>
public interface IOrigemDoEvento
{
    Guid? AutorId { get; }

    /// <summary>Id da mensagem da outbox: identifica a ocorrência do evento (idempotência de quem grava por evento).</summary>
    Guid? MensagemId { get; }

    /// <summary>Quando o evento foi gravado (commit do caso de uso).</summary>
    DateTimeOffset? OcorridoEm { get; }
}
