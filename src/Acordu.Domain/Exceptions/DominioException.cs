namespace Acordu.Domain.Exceptions;

/// <summary>
/// Base das violações de regra de negócio. Input malformado continua sendo
/// <see cref="ArgumentException"/>; regra de negócio violada é sempre uma derivada desta.
/// </summary>
public abstract class DominioException : Exception
{
    protected DominioException()
    {
    }

    protected DominioException(string message)
        : base(message)
    {
    }

    protected DominioException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
