namespace Acordu.Domain.Exceptions;

/// <summary>O calendário de horas úteis não pode ser usado para calcular SLA.</summary>
public sealed class CalendarioInvalidoException : DominioException
{
    public CalendarioInvalidoException()
    {
    }

    public CalendarioInvalidoException(string message)
        : base(message)
    {
    }

    public CalendarioInvalidoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
