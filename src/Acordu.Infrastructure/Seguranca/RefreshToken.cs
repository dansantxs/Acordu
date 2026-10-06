namespace Acordu.Infrastructure.Seguranca;

/// <summary>
/// Refresh token persistido e revogável (segurança §5.1). Só o SHA-256 fica no banco. Cada uso o consome e gera outro
/// (rotação); apresentar um já consumido indica roubo e revoga todos os do usuário no tenant.
/// </summary>
public sealed class RefreshToken
{
    public RefreshToken(Guid usuarioId, Guid tenantId, string hash, DateTimeOffset criadoEm, DateTimeOffset expiraEm)
    {
        Id = Guid.CreateVersion7();
        UsuarioId = usuarioId;
        TenantId = tenantId;
        Hash = hash;
        CriadoEm = criadoEm;
        ExpiraEm = expiraEm;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private RefreshToken()
    {
        Hash = null!;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    /// <summary>Tenant da sessão: a renovação abre a sessão de novo nele.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>SHA-256 do token, em hexadecimal minúsculo.</summary>
    public string Hash { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset ExpiraEm { get; private set; }

    /// <summary>Trocado por outro na renovação.</summary>
    public DateTimeOffset? ConsumidoEm { get; private set; }

    /// <summary>Logout, troca de tenant, reuso detectado ou vínculo removido.</summary>
    public DateTimeOffset? RevogadoEm { get; private set; }

    public bool Ativo(DateTimeOffset agora) => ConsumidoEm is null && RevogadoEm is null && ExpiraEm > agora;

    public void Consumir(DateTimeOffset agora) => ConsumidoEm ??= agora;

    public void Revogar(DateTimeOffset agora) => RevogadoEm ??= agora;
}
