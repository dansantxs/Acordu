namespace Acordu.Application.Common;

/// <summary>
/// A lista que a pessoa confirmou não é mais a de agora (ex.: uma tarefa aberta a mais ou a menos, F11-31). Vira
/// <c>409 conflito-versao</c>, como a versão divergente: recarregar e confirmar de novo.
/// </summary>
public sealed class ListaDesatualizadaException : Exception
{
    public ListaDesatualizadaException()
        : base("A lista de tarefas abertas mudou. Confira e confirme de novo.")
    {
    }

    public ListaDesatualizadaException(string message)
        : base(message)
    {
    }

    public ListaDesatualizadaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
