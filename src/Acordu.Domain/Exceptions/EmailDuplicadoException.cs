namespace Acordu.Domain.Exceptions;

/// <summary>Já existe contato ativo do cliente com o mesmo e-mail.</summary>
public sealed class EmailDuplicadoException : DominioException
{
    public EmailDuplicadoException()
    {
    }

    public EmailDuplicadoException(string message)
        : base(message)
    {
    }

    public EmailDuplicadoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
