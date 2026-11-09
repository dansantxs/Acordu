namespace Acordu.Domain.Exceptions;

/// <summary>Projeto de outro cliente que a tarefa (F11-24): a tarefa só muda para um projeto do mesmo cliente; tarefa sem cliente não entra em projeto.</summary>
public sealed class ProjetoDeOutroClienteException : DominioException
{
    public ProjetoDeOutroClienteException()
        : base("O projeto é de outro cliente: a tarefa só muda para um projeto do mesmo cliente.")
    {
    }

    public ProjetoDeOutroClienteException(string message)
        : base(message)
    {
    }

    public ProjetoDeOutroClienteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
