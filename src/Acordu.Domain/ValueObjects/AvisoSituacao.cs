using Acordu.Domain.Enums;

namespace Acordu.Domain.ValueObjects;

/// <summary>
/// O aviso de situação devido a uma empresa num instante do diário (<c>Tenant.AvisoDeSituacaoDevido</c>, design do F14,
/// D7): o tipo, a data de referência (o fim do teste, ou a data prevista do expurgo), a antecedência configurada que o
/// tornou devido (nula no vencimento) e as marcas a gravar: a dele e as dos avisos dispensados junto (F14-103).
/// </summary>
public sealed record AvisoSituacao(
    TipoAvisoSituacao Tipo, DateTimeOffset Referencia, int? DiasAntes, IReadOnlyList<string> Marcas)
{
    /// <summary>Igualdade por valor também da lista de marcas.</summary>
    public bool Equals(AvisoSituacao? outro) =>
        outro is not null
        && Tipo == outro.Tipo
        && Referencia == outro.Referencia
        && DiasAntes == outro.DiasAntes
        && Marcas.SequenceEqual(outro.Marcas);

    public override int GetHashCode() => HashCode.Combine(Tipo, Referencia, DiasAntes, string.Join('|', Marcas));
}
