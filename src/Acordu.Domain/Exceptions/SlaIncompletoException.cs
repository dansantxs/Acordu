namespace Acordu.Domain.Exceptions;

/// <summary>A política de SLA não define prazos válidos para todas as prioridades.</summary>
public sealed class SlaIncompletoException : DominioException
{
    public SlaIncompletoException()
    {
    }

    public SlaIncompletoException(string message)
        : base(message)
    {
    }

    public SlaIncompletoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
