namespace Acordu.Infrastructure.Email.Entrada;

/// <summary>O e-mail recebido passou do tamanho máximo durante a leitura (F15-89): a rota responde <c>413</c>.</summary>
public sealed class CorpoGrandeDemaisException : Exception
{
    public CorpoGrandeDemaisException()
        : base("O e-mail recebido passa do tamanho máximo.")
    {
    }

    public CorpoGrandeDemaisException(string message)
        : base(message)
    {
    }

    public CorpoGrandeDemaisException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
