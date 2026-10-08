namespace Acordu.Domain.Common;

/// <summary>E-mail aparado, em minúsculas e com formato mínimo válido (contatos e convites).</summary>
public static class Email
{
    public static string Normalizar(string email, string parametro)
    {
        var valor = Texto.Obrigatorio(email, LimitesTexto.Email, parametro).ToLowerInvariant();
        var arroba = valor.IndexOf('@', StringComparison.Ordinal);
        var valido = arroba > 0
            && arroba == valor.LastIndexOf('@')
            && valor.IndexOf('.', arroba) > arroba + 1
            && !valor.EndsWith('.')
            && !valor.Any(char.IsWhiteSpace);
        return valido ? valor : throw new ArgumentException("E-mail inválido.", parametro);
    }
}
