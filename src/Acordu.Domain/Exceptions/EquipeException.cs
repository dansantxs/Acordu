namespace Acordu.Domain.Exceptions;

/// <summary>
/// Regra da gestão da equipe violada: papel de cliente num convite ou troca de papel, e-mail já vinculado ao tenant,
/// último Dono removido ou rebaixado (segurança §3.3).
/// </summary>
public sealed class EquipeException : DominioException
{
    public EquipeException()
    {
    }

    public EquipeException(string message)
        : base(message)
    {
    }

    public EquipeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
