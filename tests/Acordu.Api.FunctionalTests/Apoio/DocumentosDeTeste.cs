using System.Security.Cryptography;
using System.Text;

namespace Acordu.Api.FunctionalTests.Apoio;

/// <summary>
/// CPF e CNPJ válidos e novos para o cadastro (F14g: um teste por documento; o banco é o mesmo entre os testes), e o
/// registro global do documento lido ou gravado direto no banco (a liberação só nasce na área da Nebrys, no F14l).
/// </summary>
internal static class DocumentosDeTeste
{
    private const string Alfanumericos = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>CNPJ numérico aleatório, sem pontuação (10¹² combinações: não repete entre os testes).</summary>
    public static string Novo() => Cnpj(new string([.. Enumerable.Range(0, 12).Select(_ => (char)('0' + RandomNumberGenerator.GetInt32(10)))]));

    /// <summary>CPF aleatório, sem pontuação.</summary>
    public static string NovoCpf()
    {
        string base9;
        do
            base9 = new string([.. Enumerable.Range(0, 9).Select(_ => (char)('0' + RandomNumberGenerator.GetInt32(10)))]);
        while (base9.Distinct().Count() == 1);
        var d1 = Digito(base9, i => 10 - i);
        var d2 = Digito(base9 + d1, i => 11 - i);
        return $"{base9}{d1}{d2}";
    }

    /// <summary>CNPJ alfanumérico aleatório (IN RFB 2.229/2024), em maiúsculas e sem pontuação.</summary>
    public static string NovoAlfanumerico() =>
        Cnpj(new string([.. Enumerable.Range(0, 12).Select(_ => Alfanumericos[RandomNumberGenerator.GetInt32(Alfanumericos.Length)])]));

    /// <summary>CPF com a máscara (000.000.000-00).</summary>
    public static string ComPontuacaoCpf(string cpf) => $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";

    /// <summary>CNPJ com a máscara (00.000.000/0000-00), na caixa dada.</summary>
    public static string ComPontuacaoCnpj(string cnpj) => $"{cnpj[..2]}.{cnpj[2..5]}.{cnpj[5..8]}/{cnpj[8..12]}-{cnpj[12..]}";

    /// <summary>O mesmo dígito verificador inválido: troca o último.</summary>
    public static string ComDigitoErrado(string documento) =>
        documento[..^1] + (char)('0' + (((documento[^1] - '0') + 1) % 10));

    /// <summary>O HMAC que a API grava (mesma chave da fixture, documento já normalizado).</summary>
    public static string Hash(string documentoNormalizado) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(ApiFixture.ChaveDocumentos), Encoding.UTF8.GetBytes(documentoNormalizado)));

    private static string Cnpj(string base12)
    {
        int[] pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        var d1 = Digito(base12, i => pesos1[i]);
        var d2 = Digito(base12 + d1, i => pesos2[i]);
        return $"{base12}{d1}{d2}";
    }

    private static int Digito(string d, Func<int, int> peso)
    {
        var soma = 0;
        for (var i = 0; i < d.Length; i++)
            soma += (d[i] - '0') * peso(i);
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
