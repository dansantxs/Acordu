namespace Acordu.Application.Abstractions.Eventos;

/// <summary>
/// Quem causou o evento sendo entregue a um <see cref="IManipuladorPosCommit{TEvento}"/>: o usuário da requisição que
/// gravou a outbox (nulo em job ou ação sem usuário). Vale também nas novas tentativas, porque fica gravado na mensagem.
/// </summary>
public interface IOrigemDoEvento
{
    Guid? AutorId { get; }
}
