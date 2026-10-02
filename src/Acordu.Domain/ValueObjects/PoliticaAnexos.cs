namespace Acordu.Domain.ValueObjects;

/// <summary>
/// Limites de anexo vindos da configuração (contrato da API: até 5 por interação, 20 MB cada). Tipos permitidos são
/// content types comparados sem diferenciar maiúsculas.
/// </summary>
public sealed record PoliticaAnexos
{
    public PoliticaAnexos(long tamanhoMaximoBytes, IEnumerable<string> tiposPermitidos, int maximoPorInteracao)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tamanhoMaximoBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximoPorInteracao);
        ArgumentNullException.ThrowIfNull(tiposPermitidos);
        var tipos = tiposPermitidos.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).ToHashSet();
        if (tipos.Count == 0)
            throw new ArgumentException("Informe ao menos um tipo de arquivo permitido.", nameof(tiposPermitidos));

        TamanhoMaximoBytes = tamanhoMaximoBytes;
        TiposPermitidos = tipos;
        MaximoPorInteracao = maximoPorInteracao;
    }

    public long TamanhoMaximoBytes { get; }

    public IReadOnlySet<string> TiposPermitidos { get; }

    public int MaximoPorInteracao { get; }

    public bool Permite(string tipoConteudo) => TiposPermitidos.Contains(tipoConteudo.Trim().ToLowerInvariant());

    public bool Equals(PoliticaAnexos? other) =>
        other is not null
        && TamanhoMaximoBytes == other.TamanhoMaximoBytes
        && MaximoPorInteracao == other.MaximoPorInteracao
        && TiposPermitidos.SetEquals(other.TiposPermitidos);

    public override int GetHashCode() => HashCode.Combine(TamanhoMaximoBytes, MaximoPorInteracao, TiposPermitidos.Count);
}
