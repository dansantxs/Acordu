namespace Acordu.Domain.Exceptions;

/// <summary>Regra do acesso de contatos ao portal violada (já tem acesso, e-mail já vinculado ao tenant, convite usado).</summary>
public sealed class AcessoPortalException : DominioException
{
    public AcessoPortalException()
    {
    }

    public AcessoPortalException(string message)
        : base(message)
    {
    }

    public AcessoPortalException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
