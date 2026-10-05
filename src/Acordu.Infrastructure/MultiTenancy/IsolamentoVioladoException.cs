namespace Acordu.Infrastructure.MultiTenancy;

/// <summary>
/// Tentativa de gravar dado fora do tenant (ou do cliente) do contexto. Indica bug de orquestração, nunca entrada do
/// usuário: a gravação inteira é abortada.
/// </summary>
public sealed class IsolamentoVioladoException : Exception
{
    public IsolamentoVioladoException()
    {
    }

    public IsolamentoVioladoException(string message)
        : base(message)
    {
    }

    public IsolamentoVioladoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
