namespace Acordu.Domain.Common;

/// <summary>
/// Raiz de agregado com identidade e tenant. O <see cref="TenantId"/> é definido na criação e
/// nunca muda: é a chave do isolamento multi-tenant (ADR 0001).
/// </summary>
public abstract class EntidadeBase
{
    private readonly List<IEventoDominio> _eventos = [];

    protected EntidadeBase(Guid id, Guid tenantId)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("O Id não pode ser vazio.", nameof(id));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("O TenantId não pode ser vazio.", nameof(tenantId));

        Id = id;
        TenantId = tenantId;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    protected EntidadeBase()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Eventos registrados e ainda não despachados.</summary>
    public IReadOnlyList<IEventoDominio> Eventos => _eventos;

    /// <summary>Chamado pela Infrastructure depois de despachar os eventos.</summary>
    public void LimparEventos() => _eventos.Clear();

    protected void Registrar(IEventoDominio evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        _eventos.Add(evento);
    }
}
