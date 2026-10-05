using System.Text;

namespace Acordu.Infrastructure.Persistence.Conversores;

/// <summary>
/// Nome de enum no formato do contrato da API e do banco: PascalCase → SCREAMING_SNAKE (EmAndamento → EM_ANDAMENTO).
/// </summary>
internal static class EnumTexto<TEnum>
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> Nomes =
        Enum.GetValues<TEnum>().ToDictionary(v => v, v => ParaSnakeMaiusculo(v.ToString()));

    private static readonly Dictionary<string, TEnum> Valores =
        Nomes.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);

    public static string Texto(TEnum valor) =>
        Nomes.TryGetValue(valor, out var nome)
            ? nome
            : throw new ArgumentOutOfRangeException(nameof(valor), valor, $"Valor inválido de {typeof(TEnum).Name}.");

    public static TEnum Valor(string texto) =>
        Valores.TryGetValue(texto, out var valor)
            ? valor
            : throw new InvalidOperationException($"'{texto}' não é um valor conhecido de {typeof(TEnum).Name}.");

    private static string ParaSnakeMaiusculo(string nome)
    {
        var resultado = new StringBuilder(nome.Length + 8);
        for (var i = 0; i < nome.Length; i++)
        {
            if (i > 0 && char.IsUpper(nome[i]))
                resultado.Append('_');
            resultado.Append(char.ToUpperInvariant(nome[i]));
        }

        return resultado.ToString();
    }
}
