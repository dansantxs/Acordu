namespace Acordu.Domain.Exceptions;

/// <summary>A data cai num período de apuração já fechado do contrato (RF-APT-05).</summary>
public sealed class PeriodoFechadoException : DominioException
{
    public PeriodoFechadoException()
    {
    }

    public PeriodoFechadoException(string message)
        : base(message)
    {
    }

    public PeriodoFechadoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
