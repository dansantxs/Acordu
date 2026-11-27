using System.Text.RegularExpressions;

namespace Acordu.Application.Empresas;

/// <summary>
/// Quem fornece o Acordu, como aparece nos textos (ADR 0017): o nome (<c>Fornecedor__Nome</c>, padrão "Nebrys"), o artigo
/// que o acompanha (<c>Fornecedor__Artigo</c>: <c>a</c>, o padrão, <c>o</c> ou <c>nenhum</c>) e o canal para falar com
/// ele (<c>Fornecedor__Contato</c>; decisão 14 do G2 do F14, design D15). Em produção, as variáveis <c>NOME_FORNECEDOR</c>,
/// <c>ARTIGO_FORNECEDOR</c> e <c>CONTATO_FORNECEDOR</c> do GitHub, as mesmas do SPA (<c>VITE_*_FORNECEDOR</c>).
/// Nenhum texto visível escreve o nome fixo: usa <see cref="Texto"/> com os marcadores <c>{{fornecedor}}</c> (só o nome) e
/// <c>{{a fornecedor}}</c>, <c>{{A fornecedor}}</c>, <c>{{da fornecedor}}</c>, <c>{{à fornecedor}}</c>,
/// <c>{{na fornecedor}}</c> e <c>{{pela fornecedor}}</c>, escritos no feminino e trocados pelo artigo configurado.
/// </summary>
public sealed partial record OpcoesFornecedor
{
    public const string NomePadrao = "Nebrys";

    private static readonly Dictionary<string, (string Feminino, string Masculino, string Nenhum)> Formas = new(StringComparer.Ordinal)
    {
        ["a"] = ("a ", "o ", ""),
        ["da"] = ("da ", "do ", "de "),
        ["à"] = ("à ", "ao ", "a "),
        ["na"] = ("na ", "no ", "em "),
        ["pela"] = ("pela ", "pelo ", "por "),
    };

    public OpcoesFornecedor(string? nome = null, string? artigo = null, string? contato = null)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? NomePadrao : nome.Trim();
        Artigo = (artigo?.Trim().ToLowerInvariant()) switch
        {
            null or "" or "a" => ArtigoFornecedor.Feminino,
            "o" => ArtigoFornecedor.Masculino,
            "nenhum" => ArtigoFornecedor.Nenhum,
            _ => throw new InvalidOperationException(
                $"Fornecedor__Artigo inválido: \"{artigo}\". Use \"a\", \"o\" ou \"nenhum\"."),
        };
        Contato = string.IsNullOrWhiteSpace(contato) ? null : contato.Trim();
    }

    public string Nome { get; }

    public ArtigoFornecedor Artigo { get; }

    public string? Contato { get; }

    /// <summary>"fale com a Nebrys: {canal}", ou só "fale com a Nebrys" sem o canal na configuração.</summary>
    public string FaleComOFornecedor =>
        Contato is null ? Texto("fale com {{a fornecedor}}") : Texto("fale com {{a fornecedor}}: ") + Contato;

    /// <summary>Troca os marcadores do fornecedor no modelo. Marcador desconhecido é defeito do texto.</summary>
    public string Texto(string modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        return Marcador().Replace(modelo, m =>
        {
            var forma = m.Groups["forma"].Value;
            if (forma.Length == 0)
                return Nome;
            var maiuscula = char.IsUpper(forma[0]);
            if (!Formas.TryGetValue(forma.ToLowerInvariant(), out var formas))
                throw new FormatException($"Marcador do fornecedor desconhecido: \"{m.Value}\".");
            var prefixo = Artigo switch
            {
                ArtigoFornecedor.Masculino => formas.Masculino,
                ArtigoFornecedor.Nenhum => formas.Nenhum,
                _ => formas.Feminino,
            };
            if (maiuscula && prefixo.Length > 0)
                prefixo = char.ToUpperInvariant(prefixo[0]) + prefixo[1..];
            return prefixo + Nome;
        });
    }

    [GeneratedRegex(@"\{\{(?:(?<forma>[^\s{}]+) )?fornecedor\}\}")]
    private static partial Regex Marcador();
}

/// <summary>O artigo que acompanha o nome do fornecedor nos textos ("a Nebrys", "o Fulano", "Fulano").</summary>
public enum ArtigoFornecedor
{
    Feminino,
    Masculino,
    Nenhum,
}
