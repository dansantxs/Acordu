namespace Acordu.Application.Common;

/// <summary>Limite de uso de um caso de uso (ex.: sugestões por hora) → 429 <c>limite-requisicoes</c> com a mensagem.</summary>
public sealed class LimiteExcedidoException : Exception
{
    public LimiteExcedidoException()
        : base("Muitas requisições em pouco tempo. Tente de novo mais tarde.")
    {
    }

    public LimiteExcedidoException(string message)
        : base(message)
    {
    }

    public LimiteExcedidoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
