using Acordu.Domain.Common;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Cronômetro ativo de um usuário (RF-APT-02, modelo de domínio §3.4). Existir = estar ativo: a Application remove o
/// registro ao parar ou descartar. "No máximo um por usuário" é garantido por índice único (TenantId, UsuarioId).
/// </summary>
public sealed class Cronometro : EntidadeBase
{
    private Cronometro(Guid id, Guid tenantId, Guid usuarioId, Guid tarefaId, DateTimeOffset iniciadoEm)
        : base(id, tenantId)
    {
        UsuarioId = usuarioId;
        TarefaId = tarefaId;
        IniciadoEm = iniciadoEm;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Cronometro()
    {
    }

    public Guid UsuarioId { get; private set; }

    public Guid TarefaId { get; private set; }

    public DateTimeOffset IniciadoEm { get; private set; }

    public static Cronometro Iniciar(Guid id, Guid tenantId, Guid usuarioId, Guid tarefaId, DateTimeOffset agora) =>
        new(
            id,
            tenantId,
            Ids.Obrigatorio(usuarioId, nameof(usuarioId)),
            Ids.Obrigatorio(tarefaId, nameof(tarefaId)),
            agora.ToUniversalTime());

    /// <summary>
    /// Tempo decorrido arredondado ao minuto mais próximo, com mínimo de 1 minuto. Não corta em 24h: acima disso o
    /// <see cref="Apontamento"/> rejeita e o usuário decide (apontar à mão ou descartar).
    /// </summary>
    public Duracao Parar(DateTimeOffset agora)
    {
        if (agora < IniciadoEm)
            throw new ArgumentException("O instante de parada não pode ser anterior ao início.", nameof(agora));

        var minutos = (int)Math.Round((agora - IniciadoEm).TotalMinutes, MidpointRounding.AwayFromZero);
        return Duracao.FromMinutos(Math.Max(minutos, 1));
    }
}
