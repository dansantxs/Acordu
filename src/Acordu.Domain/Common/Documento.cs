namespace Acordu.Domain.Common;

/// <summary>Normalização e validação de CPF/CNPJ: guarda só dígitos e confere os dígitos verificadores.</summary>
internal static class Documento
{
    public static string? Normalizar(string? documento, string parametro)
    {
        if (string.IsNullOrWhiteSpace(documento))
            return null;

        var digitos = new string(documento.Where(char.IsAsciiDigit).ToArray());
        var semOutrosCaracteres = documento.All(c => char.IsAsciiDigit(c) || c is '.' or '-' or '/' or ' ');
        var valido = semOutrosCaracteres && digitos.Length switch
        {
            11 => CpfValido(digitos),
            14 => CnpjValido(digitos),
            _ => false,
        };

        return valido ? digitos : throw new ArgumentException("CPF ou CNPJ inválido.", parametro);
    }

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

    /// <summary>Dígito verificador módulo 11 sobre os <paramref name="tamanho"/> primeiros dígitos.</summary>
    private static int Digito(string d, int tamanho, Func<int, int> peso)
    {
        var soma = 0;
        for (var i = 0; i < tamanho; i++)
            soma += (d[i] - '0') * peso(i);

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
