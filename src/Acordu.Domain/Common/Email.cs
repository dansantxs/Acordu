namespace Acordu.Domain.Common;

/// <summary>
/// E-mail aparado, em minúsculas e com formato mínimo válido (contatos, convites e cadastro). Antes do @ só ASCII:
/// endereço com acento ("sérgio@…") exige SMTPUTF8, que o envio não usa e a maioria dos provedores recusa; o e-mail
/// nunca chegaria. Domínio com acento (IDN) continua aceito.
/// </summary>
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
        if (!valido)
            throw new ArgumentException("E-mail inválido.", parametro);
        return valor.AsSpan(0, arroba).ContainsAnyExceptInRange((char)0x21, (char)0x7E)
            ? throw new ArgumentException("E-mail inválido: antes do @, use letras sem acento.", parametro)
            : valor;
    }
}
