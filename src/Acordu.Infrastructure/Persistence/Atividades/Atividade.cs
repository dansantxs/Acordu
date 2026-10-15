using Acordu.Domain.Enums;

namespace Acordu.Infrastructure.Persistence.Atividades;

/// <summary>
/// Linha do feed de atividade (RF-COL-05), fora do domínio como o sino. Incluída por SQL (<c>ON CONFLICT DO NOTHING</c>
/// pela chave = mensagem da outbox) e apagada pelo expurgo depois de 90 dias.
/// </summary>
public sealed class Atividade
{
    private Atividade()
    {
        Tipo = null!;
    }

    public Guid Id { get; private set; }

    /// <summary>Ordem de inclusão (identity): ordena o feed e serve de cursor (eventos do mesmo commit têm o mesmo instante).</summary>
    public long Sequencia { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Mensagem da outbox que gerou a linha (idempotência).</summary>
    public Guid Chave { get; private set; }

    /// <summary>Tipo no formato do JSON (<c>SOLICITACAO_ABERTA</c>).</summary>
    public string Tipo { get; private set; }

    public DateTimeOffset OcorridaEm { get; private set; }

    public Guid? AutorId { get; private set; }

    public bool AutorEhCliente { get; private set; }

    public AlvoColaboracao Alvo { get; private set; }

    public Guid AlvoId { get; private set; }

    public Guid? ClienteId { get; private set; }

    public string? StatusDe { get; private set; }

    public string? StatusPara { get; private set; }

    public Guid? ResponsavelId { get; private set; }
}
