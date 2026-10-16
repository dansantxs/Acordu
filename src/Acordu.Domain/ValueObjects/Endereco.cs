using System.Collections.Frozen;
using Acordu.Domain.Common;

namespace Acordu.Domain.ValueObjects;

/// <summary>
/// Endereço brasileiro de um cliente (decidido em 16/10). CEP, logradouro, cidade e UF são obrigatórios quando há
/// endereço; número, complemento e bairro, opcionais ("s/n" cabe no número). Os nomes dos parâmetros são os campos do
/// JSON, para a Application devolver o erro no campo certo (<c>endereco.cep</c>).
/// </summary>
public sealed record Endereco
{
    private static readonly FrozenSet<string> Ufs = new[]
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    }.ToFrozenSet(StringComparer.Ordinal);

    public Endereco(string cep, string logradouro, string? numero, string? complemento, string? bairro, string cidade, string uf)
    {
        Cep = NormalizarCep(cep);
        Logradouro = Texto.Obrigatorio(logradouro, LimitesTexto.Logradouro, nameof(logradouro));
        Numero = Texto.Opcional(numero, LimitesTexto.NumeroEndereco, nameof(numero));
        Complemento = Texto.Opcional(complemento, LimitesTexto.ComplementoEndereco, nameof(complemento));
        Bairro = Texto.Opcional(bairro, LimitesTexto.Bairro, nameof(bairro));
        Cidade = Texto.Obrigatorio(cidade, LimitesTexto.Cidade, nameof(cidade));
        var siglaUf = uf?.Trim().ToUpperInvariant();
        Uf = siglaUf is not null && Ufs.Contains(siglaUf)
            ? siglaUf
            : throw new ArgumentException("UF inválida.", nameof(uf));
    }

    /// <summary>Só os 8 dígitos.</summary>
    public string Cep { get; }

    public string Logradouro { get; }

    public string? Numero { get; }

    public string? Complemento { get; }

    public string? Bairro { get; }

    public string Cidade { get; }

    /// <summary>Sigla em maiúsculas.</summary>
    public string Uf { get; }

    private static string NormalizarCep(string? cep)
    {
        var valor = cep?.Trim() ?? "";
        var digitos = new string(valor.Where(char.IsAsciiDigit).ToArray());
        var formatoAceito = valor.All(c => char.IsAsciiDigit(c) || c is '-' or '.' or ' ');
        return formatoAceito && digitos.Length == 8
            ? digitos
            : throw new ArgumentException("CEP inválido: informe os 8 dígitos.", nameof(cep));
    }
}
