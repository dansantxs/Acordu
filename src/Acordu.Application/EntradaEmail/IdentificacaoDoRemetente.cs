using Acordu.Domain.Common;
using Acordu.Domain.Enums;

namespace Acordu.Application.EntradaEmail;

/// <summary>
/// O "remetente conhecido" da spec do F15 (RF-SOL-11; design §1.2, "Identificação", e D11), em funções puras: o
/// <c>From</c> com um endereço só, normalizado como o e-mail dos contatos, que é o de <b>exatamente um</b> contato ativo de
/// cliente ativo da empresa, num e-mail que passou na conferência de autenticidade (<c>pass</c>). O resto (ninguém, mais
/// de um contato, contato ou cliente inativo, vários endereços no <c>From</c>, autenticidade <c>fail</c> ou <c>none</c>) é
/// remetente não identificado, que vai à triagem com o motivo (<see cref="Classificar"/>; F15-20, F15-24 a F15-26, F15h).
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

    /// <summary>
    /// O contato do remetente conhecido ou, sem ele, o motivo da triagem (D10; F15-20, F15-24 a F15-26), nesta ordem:
    /// <list type="number">
    /// <item>nenhum contato com o e-mail (ou o <c>From</c> sem um endereço só: quem chama passa a lista vazia) é
    /// <see cref="MotivoTriagem.Desconhecido"/>, mesmo com a autenticidade falhando: o forjado de um desconhecido é só
    /// desconhecido;</item>
    /// <item>há contato com o e-mail, mas a autenticidade não é <c>pass</c>: <see cref="MotivoTriagem.RemetenteNaoConfirmado"/>,
    /// porque quem o remetente diz ser não vale (F15-26);</item>
    /// <item>contatos ativos de mais de um cliente ativo: <see cref="MotivoTriagem.MaisDeUmCliente"/>, com os clientes
    /// possíveis (F15-24);</item>
    /// <item>nenhum ativo de cliente ativo: <see cref="MotivoTriagem.ContatoInativo"/> (F15-25).</item>
    /// </list>
    /// </summary>
    public static Identificacao Classificar(bool autenticidadeOk, IReadOnlyList<ContatoDeEntrada> candidatos)
    {
        ArgumentNullException.ThrowIfNull(candidatos);
        if (candidatos.Count == 0)
            return Identificacao.NaTriagem(MotivoTriagem.Desconhecido);
        if (!autenticidadeOk)
            return Identificacao.NaTriagem(MotivoTriagem.RemetenteNaoConfirmado);

        var ativos = candidatos.Where(c => c.ContatoAtivo && c.ClienteAtivo).ToList();
        if (ativos.Count == 1)
            return new Identificacao(ativos[0], null, []);
        if (ativos.Count == 0)
            return Identificacao.NaTriagem(MotivoTriagem.ContatoInativo);
        var clientes = ativos.Select(c => c.ClienteId).Distinct().ToList();
        // Dois ativos do mesmo cliente não existem (o e-mail é único entre os ativos do cliente, RF-CLI-02); se aparecerem,
        // ninguém é escolhido sozinho.
        return clientes.Count >= 2
            ? new Identificacao(null, MotivoTriagem.MaisDeUmCliente, clientes)
            : Identificacao.NaTriagem(MotivoTriagem.Desconhecido);
    }
}

/// <summary>
/// O resultado da identificação do remetente (design do F15, §1.2 e D10): o <see cref="Contato"/> do remetente conhecido
/// ou o <see cref="Motivo"/> da triagem, com os clientes possíveis quando há mais de um (F15-24).
/// </summary>
/// <param name="Contato">O contato do remetente conhecido; nulo quando o e-mail vai à triagem.</param>
/// <param name="Motivo">Por que vai à triagem; nulo com o contato.</param>
/// <param name="ClientesPossiveis">Os clientes dos contatos ativos, só com <see cref="MotivoTriagem.MaisDeUmCliente"/>.</param>
public sealed record Identificacao(ContatoDeEntrada? Contato, MotivoTriagem? Motivo, IReadOnlyList<Guid> ClientesPossiveis)
{
    public static Identificacao NaTriagem(MotivoTriagem motivo) => new(null, motivo, []);
}
