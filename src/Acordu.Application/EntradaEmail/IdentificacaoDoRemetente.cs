using Acordu.Domain.Common;

namespace Acordu.Application.EntradaEmail;

/// <summary>
/// O "remetente conhecido" da spec do F15 (RF-SOL-11; design §1.2, "Identificação", e D11), em funções puras: o
/// <c>From</c> com um endereço só, normalizado como o e-mail dos contatos, que é o de <b>exatamente um</b> contato ativo de
/// cliente ativo da empresa, num e-mail que passou na conferência de autenticidade (<c>pass</c>). O resto (ninguém, mais
/// de um contato, contato ou cliente inativo, vários endereços no <c>From</c>, autenticidade <c>fail</c> ou <c>none</c>) é
/// remetente não identificado (F15-20, F15-24 a F15-26, no F15h).
/// </summary>
public static class IdentificacaoDoRemetente
{
    /// <summary>O veredito do adaptador que confirma que o e-mail saiu do domínio do remetente (D11).</summary>
    public const string AutenticidadeConfirmada = "pass";

    /// <summary>
    /// O e-mail do remetente, normalizado como o dos contatos, ou nulo quando o <c>From</c> não tem exatamente um endereço
    /// válido (vários endereços contam como desconhecido).
    /// </summary>
    public static string? EmailDoRemetente(IReadOnlyList<string> remetentes)
    {
        ArgumentNullException.ThrowIfNull(remetentes);
        if (remetentes.Count != 1)
            return null;
        try
        {
            return Email.Normalizar(remetentes[0], nameof(remetentes));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Só <c>pass</c>, aparado e sem diferenciar maiúsculas, confirma; <c>fail</c>, <c>none</c> ou nada, não.</summary>
    public static bool AutenticidadeOk(string? veredito) =>
        string.Equals(veredito?.Trim(), AutenticidadeConfirmada, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// O contato do remetente entre os <paramref name="candidatos"/> (os contatos da empresa com o e-mail dele), ou nulo
    /// quando não há exatamente um ativo de cliente ativo. Um contato inativo ao lado do ativo não atrapalha: o e-mail é
    /// único só entre os ativos do cliente (RF-CLI-02).
    /// </summary>
    public static ContatoDeEntrada? Escolher(IReadOnlyList<ContatoDeEntrada> candidatos)
    {
        ArgumentNullException.ThrowIfNull(candidatos);
        var ativos = candidatos.Where(c => c.ContatoAtivo && c.ClienteAtivo).Take(2).ToList();
        return ativos.Count == 1 ? ativos[0] : null;
    }
}
