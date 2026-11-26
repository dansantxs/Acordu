using Acordu.Domain.Enums;

namespace Acordu.Application.Notificacoes.Canais;

/// <summary>
/// Escolhe o canal do aviso pela origem da solicitação (RF-NOT-06; design do F15, D7; ADR 0016, item 7): INTERNA, PORTAL
/// e EMAIL → e-mail; WHATSAPP → WhatsApp quando houver a implementação e ela conseguir entregar, senão e-mail. O e-mail é o
/// padrão e a reserva, e precisa estar entre os canais.
/// </summary>
public sealed class SeletorDeCanal
{
    private readonly Dictionary<CanalAviso, ICanalDeAviso> _canais = [];
    private readonly ICanalDeAviso _email;

    public SeletorDeCanal(IEnumerable<ICanalDeAviso> canais)
    {
        ArgumentNullException.ThrowIfNull(canais);
        foreach (var canal in canais)
        {
            if (!_canais.TryAdd(canal.Canal, canal))
                throw new ArgumentException($"Canal de aviso repetido: {canal.Canal}.", nameof(canais));
        }

        _email = _canais.GetValueOrDefault(CanalAviso.Email)
            ?? throw new ArgumentException("O canal de e-mail é obrigatório.", nameof(canais));
    }

    /// <summary>O canal preferido para a origem, antes de saber se ele existe.</summary>
    public static CanalAviso CanalDaOrigem(OrigemSolicitacao origem) =>
        origem == OrigemSolicitacao.Whatsapp ? CanalAviso.Whatsapp : CanalAviso.Email;

    /// <summary>Entrega pelo canal da origem e, se ele não existir ou não entregar, pelo e-mail.</summary>
    public async Task EnviarAsync(AvisoAoContato aviso, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aviso);
        var preferido = CanalDaOrigem(aviso.Origem);
        if (preferido != CanalAviso.Email
            && _canais.TryGetValue(preferido, out var canal)
            && await canal.EnviarAsync(aviso, cancellationToken))
        {
            return;
        }

        await _email.EnviarAsync(aviso, cancellationToken);
    }
}
