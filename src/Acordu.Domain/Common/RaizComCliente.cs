namespace Acordu.Domain.Common;

/// <summary>
/// Raiz de agregado ligada a um cliente do prestador. O <see cref="ClienteId"/> é desnormalizado
/// de propósito: alimenta o segundo filtro global, que isola os clientes entre si no portal (ADR 0002).
/// Definido na criação e imutável.
/// </summary>
public abstract class RaizComCliente : EntidadeBase
{
    protected RaizComCliente(Guid id, Guid tenantId, Guid clienteId)
        : base(id, tenantId)
    {
        if (clienteId == Guid.Empty)
            throw new ArgumentException("O ClienteId não pode ser vazio.", nameof(clienteId));

        ClienteId = clienteId;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    protected RaizComCliente()
    {
    }

    public Guid ClienteId { get; private set; }
}
