namespace Acordu.Domain.Common;

/// <summary>
/// Autores fixos do que o próprio Acordu escreve numa conversa, a <b>nota do sistema</b> (design do F15, D15; ADR 0016,
/// item 10): um Id constante por origem, sem conta nem vínculo, que nunca é de uma pessoa da equipe nem de um contato e não
/// entra em listas de pessoas. Hoje só a entrada por e-mail (os anexos recusados, F15-15); o WhatsApp (#184) ganha o seu.
/// </summary>
public static class AutoresDoSistema
{
    /// <summary>A entrada por e-mail (RF-SOL-11): a nota dos anexos recusados de um e-mail recebido.</summary>
    public static readonly Guid EntradaEmail = new("acd00000-0000-4000-8000-00000000e001");

    /// <summary>O nome que a conversa da equipe mostra no lugar do nome de uma pessoa (D15).</summary>
    public const string NomeEntradaEmail = "Acordu (e-mail recebido)";

    /// <summary>O Id é de um autor do sistema.</summary>
    public static bool Eh(Guid autorId) => autorId == EntradaEmail;

    /// <summary>O nome fixo do autor do sistema, ou nulo quando o Id não é de um.</summary>
    public static string? Nome(Guid autorId) => autorId == EntradaEmail ? NomeEntradaEmail : null;
}
