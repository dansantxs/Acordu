namespace Acordu.Infrastructure.Persistence.Agenda;

/// <summary>Link da agenda assinável de uma pessoa num tenant (só o SHA-256 do token), fora do domínio como o sino.</summary>
public sealed class AssinaturaAgendaRegistro
{
    public AssinaturaAgendaRegistro(Guid tenantId, Guid usuarioId, string tokenHash, bool discreta, DateTimeOffset criadaEm)
    {
        TenantId = tenantId;
        UsuarioId = usuarioId;
        TokenHash = tokenHash;
        Discreta = discreta;
        CriadaEm = criadaEm;
    }

    public Guid TenantId { get; private set; }

    public Guid UsuarioId { get; private set; }

    public string TokenHash { get; private set; }

    public bool Discreta { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    public void Renovar(string tokenHash, bool discreta, DateTimeOffset agora)
    {
        TokenHash = tokenHash;
        Discreta = discreta;
        CriadaEm = agora;
    }

    public void DefinirModo(bool discreta) => Discreta = discreta;
}
