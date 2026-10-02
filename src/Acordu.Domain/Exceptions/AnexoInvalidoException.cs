namespace Acordu.Domain.Exceptions;

/// <summary>O anexo viola a política de anexos (tamanho, tipo ou quantidade).</summary>
public sealed class AnexoInvalidoException : DominioException
{
    public AnexoInvalidoException()
    {
    }

    public AnexoInvalidoException(string message)
        : base(message)
    {
    }

    public AnexoInvalidoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
