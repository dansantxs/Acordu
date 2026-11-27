namespace Acordu.Application.EntradaEmail;

/// <summary>
/// Os descartes depois da empresa e antes da identificação (design do F15, D11 e D12; ADR 0016, item 9), na ordem do D12
/// (automático, laço, filtro). Função pura: o e-mail descartado fica só no registro de entrada, como <c>IGNORADO</c>, e
/// nada é criado nem respondido (F15-85, F15-86, F15-87, F15-88). A lista dos sinais é a do D12, a única fonte.
/// </summary>
public static class DescarteDeEntrada
{
    /// <summary>Valores de <c>X-Email-Filtro</c> que descartam (D11); qualquer outro, ou nenhum, deixa seguir.</summary>
    public const string FiltroSpam = "spam";

    public const string FiltroVirus = "virus";

    /// <summary>Partes locais de remetente de sistema de entrega (D12), sem diferenciar maiúsculas.</summary>
    private static readonly HashSet<string> LocaisDeSistema = new(StringComparer.OrdinalIgnoreCase)
    {
        "mailer-daemon", "postmaster", "noreply", "no-reply",
    };

    private static readonly HashSet<string> PrecedenciasEmMassa = new(StringComparer.OrdinalIgnoreCase)
    {
        "bulk", "junk", "list",
    };

    /// <summary>O descarte (<see cref="DesfechoEntrada.Automatica"/>, <see cref="DesfechoEntrada.Laco"/> ou
    /// <see cref="DesfechoEntrada.Filtrada"/>), ou nulo quando o e-mail segue para a identificação.</summary>
    public static DesfechoEntrada? Avaliar(MensagemDeEntrada mensagem, OpcoesEmailEntrada opcoes)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(opcoes);
        if (Automatico(mensagem))
            return DesfechoEntrada.Automatica;
        if (Laco(mensagem, opcoes))
            return DesfechoEntrada.Laco;
        if (Filtrado(mensagem.Filtro))
            return DesfechoEntrada.Filtrada;
        return null;
    }

    /// <summary>
    /// Resposta automática, aviso de entrega ou mensagem em massa (F15-85, F15-86): envelope vazio; remetente
    /// <c>mailer-daemon</c>, <c>postmaster</c> ou <c>noreply</c> no envelope ou no <c>From</c>; <c>Auto-Submitted</c>
    /// diferente de <c>no</c>; <c>Precedence</c> bulk, junk ou list; <c>List-Id</c> ou <c>List-Unsubscribe</c>;
    /// <c>X-Autoreply</c> ou <c>X-Autorespond</c>; corpo <c>multipart/report</c>.
    /// </summary>
    public static bool Automatico(MensagemDeEntrada mensagem)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        if (mensagem.RemetenteDoEnvelope is { } envelope)
        {
            var semSinais = envelope.Trim().TrimStart('<').TrimEnd('>').Trim();
            if (semSinais.Length == 0 || LocaisDeSistema.Contains(ParteLocal(semSinais)))
                return true;
        }

        if (mensagem.Remetentes.Any(r => LocaisDeSistema.Contains(ParteLocal(r))))
            return true;

        if (mensagem.Sinais is not { } sinais)
            return false;
        return AutoSubmittedAutomatico(sinais.AutoSubmitted)
            || (sinais.Precedence is { } precedencia && PrecedenciasEmMassa.Contains(precedencia.Trim()))
            || sinais.ListaDeDistribuicao
            || sinais.RespostaAutomatica
            || string.Equals(sinais.TipoDoCorpo?.Trim(), "multipart/report", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Laço (F15-88): o remetente (do <c>From</c> ou do envelope) é um endereço do próprio Acordu, do domínio de entrada
    /// ou do domínio do remetente dos avisos.
    /// </summary>
    public static bool Laco(MensagemDeEntrada mensagem, OpcoesEmailEntrada opcoes)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(opcoes);
        IEnumerable<string> enderecos = mensagem.RemetenteDoEnvelope is { } envelope
            ? mensagem.Remetentes.Append(envelope)
            : mensagem.Remetentes;
        return enderecos.Select(Dominio).Any(d => d is not null && (d == opcoes.Dominio || d == opcoes.DominioDosAvisos));
    }

    /// <summary>Spam ou vírus pelo filtro do provedor (F15-87), sem diferenciar maiúsculas.</summary>
    public static bool Filtrado(string? filtro)
    {
        var valor = filtro?.Trim();
        return string.Equals(valor, FiltroSpam, StringComparison.OrdinalIgnoreCase)
            || string.Equals(valor, FiltroVirus, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>"Diferente de <c>no</c>" (RFC 3834), olhando só o valor antes de um ";"; vazio conta como ausente.</summary>
    private static bool AutoSubmittedAutomatico(string? autoSubmitted)
    {
        var valor = autoSubmitted?.Split(';', 2)[0].Trim();
        return !string.IsNullOrEmpty(valor) && !string.Equals(valor, "no", StringComparison.OrdinalIgnoreCase);
    }

    private static string ParteLocal(string endereco)
    {
        var limpo = endereco.Trim().TrimStart('<').TrimEnd('>');
        var arroba = limpo.LastIndexOf('@');
        return arroba < 0 ? limpo : limpo[..arroba];
    }

    /// <summary>O domínio depois do último "@", aparado e em minúsculas; nulo sem "@".</summary>
    private static string? Dominio(string endereco)
    {
        var limpo = endereco.Trim().TrimStart('<').TrimEnd('>').Trim();
        var arroba = limpo.LastIndexOf('@');
        if (arroba < 0 || arroba == limpo.Length - 1)
            return null;
        return limpo[(arroba + 1)..].TrimEnd('.').ToLowerInvariant();
    }
}
