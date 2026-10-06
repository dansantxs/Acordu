namespace Acordu.Application.Common;

/// <summary>Já existe cronômetro ativo para o usuário (RF-APT-02) → 409 <c>cronometro-ativo</c>.</summary>
public sealed class CronometroAtivoException : Exception
{
    public CronometroAtivoException()
        : base("Já existe um cronômetro ativo. Pare ou descarte antes de iniciar outro.")
    {
    }

    public CronometroAtivoException(string message)
        : base(message)
    {
    }

    public CronometroAtivoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
