namespace Acordu.Domain.Exceptions;

/// <summary>
/// Solicitação FECHADA ou CANCELADA: a conversa com o cliente acabou (F11-06, F11-08). Resposta ao cliente e anexo são
/// recusados; nota interna sem anexo continua (F11-07).
/// </summary>
public sealed class SolicitacaoEncerradaException : DominioException
{
    public SolicitacaoEncerradaException()
        : base("Esta solicitação foi encerrada. Para continuar, abra uma nova.")
    {
    }

    public SolicitacaoEncerradaException(string message)
        : base(message)
    {
    }

    public SolicitacaoEncerradaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
