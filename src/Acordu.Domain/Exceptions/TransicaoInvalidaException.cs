namespace Acordu.Domain.Exceptions;

/// <summary>A operação não é permitida no status atual do agregado (máquinas de estado do modelo de domínio).</summary>
public sealed class TransicaoInvalidaException : DominioException
{
    public TransicaoInvalidaException()
    {
    }

    public TransicaoInvalidaException(string message)
        : base(message)
    {
    }

    public TransicaoInvalidaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
