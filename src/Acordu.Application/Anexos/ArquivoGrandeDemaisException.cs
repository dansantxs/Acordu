namespace Acordu.Application.Anexos;

/// <summary>Arquivo acima do tamanho máximo da política → 413.</summary>
public sealed class ArquivoGrandeDemaisException : Exception
{
    public ArquivoGrandeDemaisException()
    {
    }

    public ArquivoGrandeDemaisException(string message)
        : base(message)
    {
    }

    public ArquivoGrandeDemaisException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
