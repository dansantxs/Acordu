using Acordu.Domain.Common;
using Acordu.Domain.Entities;

namespace Acordu.Domain.ValueObjects;

/// <summary>Molde das tarefas geradas por uma recorrência (RF-REC-01). Igualdade por valor, inclusive da checklist.</summary>
public sealed record ModeloTarefa
{
    private readonly string[] _checklist;

    public ModeloTarefa(
        string titulo,
        string? descricao,
        Guid? responsavelId,
        IEnumerable<string> checklist,
        Duracao? estimativa,
        bool visivelCliente)
    {
        ArgumentNullException.ThrowIfNull(checklist);
        var itens = checklist.Select(ItemChecklist.ValidarTexto).ToArray();
        if (itens.Length > Tarefa.MaximoItensChecklist)
            throw new ArgumentException($"A checklist aceita no máximo {Tarefa.MaximoItensChecklist} itens.", nameof(checklist));
        if (estimativa is { EhZero: true })
            throw new ArgumentException("A estimativa, se informada, deve ser maior que zero.", nameof(estimativa));

        Titulo = Texto.Obrigatorio(titulo, LimitesTexto.Titulo, nameof(titulo));
        Descricao = Texto.Opcional(descricao, LimitesTexto.Descricao, nameof(descricao));
        ResponsavelId = Ids.Opcional(responsavelId, nameof(responsavelId));
        _checklist = itens;
        Estimativa = estimativa;
        VisivelCliente = visivelCliente;
    }

    public string Titulo { get; }

    public string? Descricao { get; }

    public Guid? ResponsavelId { get; }

    public IReadOnlyList<string> Checklist => _checklist;

    public Duracao? Estimativa { get; }

    public bool VisivelCliente { get; }

    public bool Equals(ModeloTarefa? other) =>
        other is not null
        && Titulo == other.Titulo
        && Descricao == other.Descricao
        && ResponsavelId == other.ResponsavelId
        && Estimativa == other.Estimativa
        && VisivelCliente == other.VisivelCliente
        && _checklist.SequenceEqual(other._checklist);

    public override int GetHashCode() => HashCode.Combine(Titulo, Descricao, ResponsavelId, Estimativa, VisivelCliente, _checklist.Length);
}
