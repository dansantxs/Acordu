using Acordu.Domain.Common;

namespace Acordu.Application.Nebrys;

/// <summary>
/// Quem é operador da Nebrys (ADR 0014, item 1): os e-mails de <c>Nebrys__Operadores</c>, que em produção vem do secret
/// <c>API_NEBRYS_OPERADORES</c> do GitHub. Separados por vírgula ou ponto e vírgula, normalizados como o cadastro
/// (<see cref="Email.Normalizar"/>). Sem a variável, ninguém é operador e a área inteira responde <c>404</c>. Um item
/// inválido impede a subida, para um operador digitado errado não ficar sem acesso em silêncio.
/// </summary>
public sealed record OperadoresNebrys
{
    public const string Chave = "Nebrys__Operadores";

    private readonly HashSet<string> _emails;

    public OperadoresNebrys(IEnumerable<string>? emails = null)
    {
        _emails = new HashSet<string>(
            (emails ?? []).Select(e => Email.Normalizar(e, nameof(emails))), StringComparer.Ordinal);
    }

    /// <summary>Os e-mails, normalizados.</summary>
    public IReadOnlyCollection<string> Emails => _emails;

    /// <summary>Sem nenhum operador, a área não abre para ninguém.</summary>
    public bool Vazia => _emails.Count == 0;

    /// <summary>O e-mail (de uma conta) está na lista, comparado depois de normalizado.</summary>
    public bool Contem(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        try
        {
            return _emails.Contains(Email.Normalizar(email, nameof(email)));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Ausente ou vazia: ninguém. Item que não é e-mail: a API não sobe.</summary>
    public static OperadoresNebrys DaConfiguracao(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return new OperadoresNebrys();
        var itens = valor.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        try
        {
            return new OperadoresNebrys(itens);
        }
        catch (ArgumentException)
        {
            // Sem o e-mail na mensagem: ela vai para o log (convenções §5).
            throw new InvalidOperationException($"{Chave} precisa ser uma lista de e-mails separados por vírgula.");
        }
    }
}
