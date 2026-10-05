namespace Acordu.Application.Abstractions;

/// <summary>
/// Fecha um caso de uso: despacha os eventos de domínio para os manipuladores internos (na mesma transação), grava tudo
/// — inclusive a outbox dos efeitos externos — e faz o commit. Falha em qualquer etapa desfaz tudo.
/// </summary>
public interface IUnitOfWork
{
    Task SalvarAsync(CancellationToken cancellationToken = default);
}
