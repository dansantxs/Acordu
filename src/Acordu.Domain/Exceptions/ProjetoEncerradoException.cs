namespace Acordu.Domain.Exceptions;

/// <summary>Projeto CONCLUIDO ou CANCELADO não recebe tarefa (F11-25).</summary>
public sealed class ProjetoEncerradoException : DominioException
{
    public ProjetoEncerradoException()
        : base("Projeto concluído ou cancelado não recebe tarefas.")
    {
    }

    public ProjetoEncerradoException(string message)
        : base(message)
    {
    }

    public ProjetoEncerradoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
