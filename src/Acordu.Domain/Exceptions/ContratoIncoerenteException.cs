namespace Acordu.Domain.Exceptions;

/// <summary>O contrato, ou uma operação sobre ele, contraria as regras do tipo de contrato (franquia, ajuste de saldo).</summary>
public sealed class ContratoIncoerenteException : DominioException
{
    public ContratoIncoerenteException()
    {
    }

    public ContratoIncoerenteException(string message)
        : base(message)
    {
    }

    public ContratoIncoerenteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
