using System.Text.RegularExpressions;

namespace Acordu.Domain.Services;

/// <summary>
/// Menções no texto (modelo §4.5.1): o texto guarda <c>@{usuarioId}</c> e o front mostra o nome. Aqui só se extraem os
/// Ids; quem pode ser mencionado (pessoa da equipe que vê o alvo) é regra da Application.
/// </summary>
public static partial class Mencoes
{
    /// <summary>Limite de pessoas mencionadas num mesmo texto.</summary>
    public const int Maximo = 20;

    /// <summary>Ids mencionados, sem repetição, na ordem em que aparecem; fora o <paramref name="autorId"/>.</summary>
    public static IReadOnlyList<Guid> Extrair(string? texto, Guid autorId)
    {
        if (string.IsNullOrEmpty(texto))
            return [];
        var ids = new List<Guid>();
        foreach (Match m in Token().Matches(texto))
        {
            var id = Guid.Parse(m.Groups[1].ValueSpan);
            if (id != autorId && id != Guid.Empty && !ids.Contains(id))
                ids.Add(id);
        }

        if (ids.Count > Maximo)
            throw new ArgumentException($"No máximo {Maximo} pessoas mencionadas por texto.", nameof(texto));
        return ids;
    }

    [GeneratedRegex(@"@\{([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\}")]
    private static partial Regex Token();
}
