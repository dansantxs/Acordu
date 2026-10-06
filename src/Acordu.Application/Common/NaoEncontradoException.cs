namespace Acordu.Application.Common;

/// <summary>
/// O recurso não existe para quem pergunta: inexistente, de outro tenant ou de outro cliente (convenções §4) → 404.
/// </summary>
public sealed class NaoEncontradoException : Exception
{
    public NaoEncontradoException()
        : base("Recurso não encontrado.")
    {
    }

    public NaoEncontradoException(string message)
        : base(message)
    {
    }

    public NaoEncontradoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
