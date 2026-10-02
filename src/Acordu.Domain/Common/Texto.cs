namespace Acordu.Domain.Common;

/// <summary>Normalização de texto de entrada: apara espaços e valida obrigatoriedade e tamanho.</summary>
internal static class Texto
{
    /// <summary>Texto não vazio (após aparar) com no máximo <paramref name="maximo"/> caracteres.</summary>
    public static string Obrigatorio(string? valor, int maximo, string parametro)
    {
        var aparado = valor?.Trim();
        if (string.IsNullOrEmpty(aparado))
            throw new ArgumentException("O texto é obrigatório.", parametro);

        return ValidarTamanho(aparado, maximo, parametro);
    }

    /// <summary>Texto opcional: vazio ou só espaços vira <see langword="null"/>.</summary>
    public static string? Opcional(string? valor, int maximo, string parametro)
    {
        var aparado = valor?.Trim();
        return string.IsNullOrEmpty(aparado) ? null : ValidarTamanho(aparado, maximo, parametro);
    }

    private static string ValidarTamanho(string valor, int maximo, string parametro) =>
        valor.Length <= maximo
            ? valor
            : throw new ArgumentException($"O texto excede {maximo} caracteres.", parametro);
}
