namespace Acordu.Domain.ValueObjects;

/// <summary>
/// Quando saem os avisos de situação (decisão 3 do G2 do F14; configuração <c>Empresas__AvisosAntesDoFim</c>,
/// <c>Empresas__AvisoAntesDoExpurgo</c> e <c>Empresas__DiasAteExpurgo</c>): dias antes do fim do teste (mais de um, ex.:
/// 7 e 1), dias antes do expurgo e o prazo do só leitura até o expurgo, de onde sai a data dele.
/// </summary>
public sealed record PrazosAvisosSituacao
{
    public PrazosAvisosSituacao(IEnumerable<int> diasAntesDoFim, int diasAntesDoExpurgo, TimeSpan prazoAteExpurgo)
    {
        ArgumentNullException.ThrowIfNull(diasAntesDoFim);
        var dias = diasAntesDoFim.Distinct().Order().ToArray();
        if (dias.Length == 0 || dias[0] <= 0)
            throw new ArgumentOutOfRangeException(nameof(diasAntesDoFim), "Os avisos antes do fim do teste precisam de ao menos um dia, todos maiores que zero.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diasAntesDoExpurgo);
        if (prazoAteExpurgo <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(prazoAteExpurgo), prazoAteExpurgo, "O prazo até o expurgo precisa ser positivo.");

        DiasAntesDoFim = dias;
        DiasAntesDoExpurgo = diasAntesDoExpurgo;
        PrazoAteExpurgo = prazoAteExpurgo;
    }

    /// <summary>Em ordem crescente, sem repetição: o primeiro é o aviso mais próximo do fim.</summary>
    public IReadOnlyList<int> DiasAntesDoFim { get; }

    public int DiasAntesDoExpurgo { get; }

    public TimeSpan PrazoAteExpurgo { get; }

    /// <summary>Igualdade por valor também da lista (o record compararia a referência).</summary>
    public bool Equals(PrazosAvisosSituacao? outro) =>
        outro is not null
        && DiasAntesDoFim.SequenceEqual(outro.DiasAntesDoFim)
        && DiasAntesDoExpurgo == outro.DiasAntesDoExpurgo
        && PrazoAteExpurgo == outro.PrazoAteExpurgo;

    public override int GetHashCode() =>
        HashCode.Combine(string.Join(',', DiasAntesDoFim), DiasAntesDoExpurgo, PrazoAteExpurgo);
}
