namespace Acordu.Domain.Common;

/// <summary>
/// Normalização e validação de CPF/CNPJ: guarda só os caracteres (sem pontuação) e confere os dígitos verificadores.
/// CPF só tem dígitos; o CNPJ pode ter letras nas 12 primeiras posições (alfanumérico, IN RFB 2.229/2024, desde julho
/// de 2026), guardadas em maiúsculas, com os dois verificadores numéricos.
/// </summary>
public static class Documento
{
    public static string? Normalizar(string? documento, string parametro)
    {
        if (string.IsNullOrWhiteSpace(documento))
            return null;

        var semOutrosCaracteres = documento.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '/' or ' ');
        var caracteres = new string(documento.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        var valido = semOutrosCaracteres && caracteres.Length switch
        {
            11 => caracteres.All(char.IsAsciiDigit) && CpfValido(caracteres),
            14 => FormatoCnpj(caracteres) && CnpjValido(caracteres),
            _ => false,
        };

        return valido ? caracteres : throw new ArgumentException("CPF ou CNPJ inválido.", parametro);
    }

    /// <summary>12 letras maiúsculas ou dígitos e os dois verificadores numéricos.</summary>
    private static bool FormatoCnpj(string d) =>
        d.Take(12).All(c => char.IsAsciiDigit(c) || char.IsAsciiLetterUpper(c)) && char.IsAsciiDigit(d[12]) && char.IsAsciiDigit(d[13]);

    private static bool CpfValido(string d)
    {
        if (d.Distinct().Count() == 1)
            return false;

        return Digito(d, 9, peso => 10 - peso) == d[9] - '0'
            && Digito(d, 10, peso => 11 - peso) == d[10] - '0';
    }

    private static bool CnpjValido(string d)
    {
        if (d.Distinct().Count() == 1)
            return false;

        int[] pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        return Digito(d, 12, i => pesos1[i]) == d[12] - '0'
            && Digito(d, 13, i => pesos2[i]) == d[13] - '0';
    }

    /// <summary>
    /// Dígito verificador módulo 11 sobre os <paramref name="tamanho"/> primeiros caracteres. O valor de cada um é o
    /// código ASCII menos 48: os dígitos valem o próprio número, e as letras de A a Z valem de 17 a 42 (regra da Receita
    /// para o CNPJ alfanumérico, que coincide com a antiga para os numéricos).
    /// </summary>
    private static int Digito(string d, int tamanho, Func<int, int> peso)
    {
        var soma = 0;
        for (var i = 0; i < tamanho; i++)
            soma += (d[i] - '0') * peso(i);

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
