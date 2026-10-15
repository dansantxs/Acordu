using Acordu.Domain.Enums;

namespace Acordu.Infrastructure.Persistence.Colaboracao;

/// <summary>
/// Quem segue uma solicitação ou tarefa (RF-COL-03), fora do domínio como o sino. Incluído por SQL
/// (<c>ON CONFLICT DO NOTHING</c>) e removido ao deixar de seguir; sem chave estrangeira para o alvo.
/// </summary>
public sealed class Seguidor
{
    private Seguidor()
    {
    }

    public Guid TenantId { get; private set; }

    public AlvoColaboracao Alvo { get; private set; }

    public Guid AlvoId { get; private set; }

    public Guid UsuarioId { get; private set; }

    public DateTimeOffset Desde { get; private set; }
}
