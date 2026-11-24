using System.Collections.Frozen;
using System.Globalization;
using Acordu.Application.Cadastro;

namespace Acordu.Infrastructure.Seguranca;

/// <summary>
/// <see cref="IDominiosDescartaveis"/> sobre o arquivo versionado <c>dominios-descartaveis.txt</c> (design do F14, D9),
/// embutido no assembly e lido uma vez (singleton): sem rede nem dependência em runtime. Origem, commit e licença da lista
/// ficam no cabeçalho do arquivo; atualizar é trocar o arquivo num PR <c>melhoria</c>.
/// </summary>
public sealed class DominiosDescartaveis : IDominiosDescartaveis
{
    /// <summary>Nome do recurso embutido (o <c>LogicalName</c> do <c>.csproj</c>).</summary>
    public const string Recurso = "Acordu.Infrastructure.Seguranca.dominios-descartaveis.txt";

    private readonly FrozenSet<string> _dominios;

    /// <param name="linhas">Um domínio por linha; linhas vazias e as que começam por <c>#</c> são ignoradas.</param>
    public DominiosDescartaveis(IEnumerable<string> linhas)
    {
        ArgumentNullException.ThrowIfNull(linhas);
        _dominios = linhas
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(Ascii)
            .ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>Quantos domínios a lista tem.</summary>
    public int Total => _dominios.Count;

    /// <summary>A lista versionada, do recurso embutido. Sem o recurso, a Api não sobe (erro de build, não de ambiente).</summary>
    public static DominiosDescartaveis DaListaVersionada()
    {
        using var arquivo = typeof(DominiosDescartaveis).Assembly.GetManifestResourceStream(Recurso)
            ?? throw new InvalidOperationException($"Recurso {Recurso} ausente do assembly da Infrastructure.");
        using var leitor = new StreamReader(arquivo);
        var linhas = new List<string>();
        while (leitor.ReadLine() is { } linha)
            linhas.Add(linha);
        return new DominiosDescartaveis(linhas);
    }

    public bool EhDescartavel(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        var arroba = email.LastIndexOf('@');
        if (arroba < 0)
            return false;
        // O domínio e cada pai dele, parando antes do sufixo final (a lista não tem TLD solto).
        var dominio = Ascii(email[(arroba + 1)..].Trim());
        while (dominio.Contains('.', StringComparison.Ordinal))
        {
            if (_dominios.Contains(dominio))
                return true;
            dominio = dominio[(dominio.IndexOf('.', StringComparison.Ordinal) + 1)..];
        }

        return false;
    }

    /// <summary>
    /// Minúsculo, sem ponto no fim e, com acento (IDN), na forma ASCII (<c>xn--</c>), que é como a lista guarda esses
    /// domínios. O que a conversão não aceita fica como veio: só não vai coincidir com a lista.
    /// </summary>
    private static string Ascii(string dominio)
    {
        var limpo = dominio.TrimEnd('.').ToLowerInvariant();
        if (limpo.Length == 0 || !limpo.Any(c => c > 0x7F))
            return limpo;
        try
        {
            return new IdnMapping().GetAscii(limpo);
        }
        catch (ArgumentException)
        {
            return limpo;
        }
    }
}
