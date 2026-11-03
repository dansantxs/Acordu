namespace Acordu.Infrastructure.Persistence.Outbox;

/// <summary>
/// Ids das mensagens de outbox gravadas (e commitadas) neste escopo. A Api as despacha no fim da própria requisição:
/// assim nada depende de trabalho em segundo plano, o que é obrigatório no Cloud Run (ADR 0010).
/// </summary>
public sealed class MensagensDaRequisicao
{
    private readonly List<Guid> _ids = [];

    public IReadOnlyList<Guid> Ids => _ids;

    internal void Adicionar(IEnumerable<Guid> ids) => _ids.AddRange(ids);

    /// <summary>Devolve e esquece as mensagens pendentes de despacho.</summary>
    public IReadOnlyList<Guid> Retirar()
    {
        var ids = _ids.ToList();
        _ids.Clear();
        return ids;
    }
}
