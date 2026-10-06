namespace Acordu.Application.Common;

/// <summary>Quem age não tem a permissão exigida pelo caso de uso (segurança §3) → 403.</summary>
public sealed class AcessoNegadoException : Exception
{
    public AcessoNegadoException()
        : base("Sem permissão para esta operação.")
    {
    }

    public AcessoNegadoException(string message)
        : base(message)
    {
    }

    public AcessoNegadoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
