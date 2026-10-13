namespace Acordu.Infrastructure.Persistence.Auditoria;

/// <summary>
/// Linha da trilha de auditoria (segurança §8). Não é raiz do domínio: só é incluída (nunca alterada) e só some pelo
/// expurgo. <see cref="TenantId"/> nulo só no login com falha (antes de haver tenant): invisível pelos filtros.
/// </summary>
public sealed class RegistroAuditoria
{
    public const int TamanhoAcao = 80;
    public const int TamanhoAlvoTipo = 40;
    public const int TamanhoIp = 45;

    private RegistroAuditoria()
    {
        Acao = null!;
    }

    public Guid Id { get; private set; }

    public Guid? TenantId { get; private set; }

    public Guid? UsuarioId { get; private set; }

    public string Acao { get; private set; }

    public string? AlvoTipo { get; private set; }

    public Guid? AlvoId { get; private set; }

    /// <summary>JSON (jsonb) com os detalhes da ação.</summary>
    public string? Dados { get; private set; }

    public string? Ip { get; private set; }

    public DateTimeOffset OcorridoEm { get; private set; }

    public static RegistroAuditoria Criar(
        Guid? tenantId,
        Guid? usuarioId,
        string acao,
        string? alvoTipo,
        Guid? alvoId,
        string? dados,
        string? ip,
        DateTimeOffset ocorridoEm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acao);
        return new RegistroAuditoria
        {
            Id = Guid.CreateVersion7(ocorridoEm),
            TenantId = tenantId,
            UsuarioId = usuarioId,
            Acao = acao,
            AlvoTipo = alvoTipo,
            AlvoId = alvoId,
            Dados = dados,
            Ip = ip is { Length: > TamanhoIp } ? ip[..TamanhoIp] : ip,
            OcorridoEm = ocorridoEm,
        };
    }
}
