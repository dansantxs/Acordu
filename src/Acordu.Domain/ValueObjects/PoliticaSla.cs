using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;

namespace Acordu.Domain.ValueObjects;

/// <summary>
/// Política de SLA do contrato (RF-CTR-03): exatamente um <see cref="PrazoSla"/> por prioridade.
/// A igualdade é por valor sobre os prazos, independente da ordem em que foram informados.
/// </summary>
public sealed record PoliticaSla
{
    private readonly PrazoSla[] _prazos;

    public PoliticaSla(IEnumerable<PrazoSla> prazos)
    {
        ArgumentNullException.ThrowIfNull(prazos);
        var lista = prazos.ToArray();
        if (lista.Any(p => p is null))
            throw new ArgumentException("A política não pode conter prazos nulos.", nameof(prazos));

        var duplicada = lista.GroupBy(p => p.Prioridade).FirstOrDefault(g => g.Count() > 1);
        if (duplicada is not null)
            throw new SlaIncompletoException($"A prioridade {duplicada.Key} aparece mais de uma vez na política.");

        var faltantes = Enum.GetValues<Prioridade>().Except(lista.Select(p => p.Prioridade)).ToArray();
        if (faltantes.Length > 0)
            throw new SlaIncompletoException($"A política não define prazos para: {string.Join(", ", faltantes)}.");

        _prazos = [.. lista.OrderBy(p => p.Prioridade)];
    }

    /// <summary>Prazos ordenados por prioridade (da mais baixa para a mais alta).</summary>
    public IReadOnlyList<PrazoSla> Prazos => _prazos;

    public PrazoSla PrazosDe(Prioridade prioridade) =>
        Array.Find(_prazos, p => p.Prioridade == prioridade)
        ?? throw new ArgumentOutOfRangeException(nameof(prioridade), prioridade, "Prioridade inválida.");

    public bool Equals(PoliticaSla? other) =>
        other is not null && _prazos.SequenceEqual(other._prazos);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var prazo in _prazos)
            hash.Add(prazo);
        return hash.ToHashCode();
    }
}
