using Acordu.Application.Nebrys;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.ConvitesEmpresa;

internal sealed class ConviteEmpresaConfiguration : IEntityTypeConfiguration<ConviteEmpresa>
{
    /// <summary>
    /// Pelo modelo do <c>AcorduDbContext</c>, sem que ele dependa do tipo (design do F14, D4 e D12: só esta feature, a área
    /// da Nebrys e as consultas dela usam a tabela; teste de arquitetura).
    /// </summary>
    public static void Aplicar(ModelBuilder modelo) => modelo.ApplyConfiguration(new ConviteEmpresaConfiguration());

    public void Configure(EntityTypeBuilder<ConviteEmpresa> builder)
    {
        builder.ToTable("convites_empresa");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Email).HasMaxLength(LimitesTexto.Email).IsRequired();
        builder.Property(c => c.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(c => c.CriadoEm);
        builder.Property(c => c.EnviadoEm);
        builder.Property(c => c.ExpiraEm);
        builder.Property(c => c.UsadoEm);
        builder.Property(c => c.RevogadoEm);
        builder.Ignore(c => c.Pendente);
        // O cadastro acha o convite pelo hash do token (único, como os outros tokens; convenções §1).
        builder.HasIndex(c => c.TokenHash).IsUnique();
        // A área confere se o e-mail já tem um convite em aberto antes de emitir outro.
        builder.HasIndex(c => c.Email);
        // Dois cadastros com o mesmo convite ao mesmo tempo: o segundo falha na versão (F14-48, "um cadastro só"); o mesmo
        // vale para o cadastro contra o reenvio ou a revogação da área.
        builder.Property<uint>(Configurations.ConfiguracaoRaiz.Versao)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");
    }
}

/// <summary>
/// Convites de empresa (RF-AUT-08, F14n; design do F14, D12), na tabela global <c>convites_empresa</c>, sem filtro de
/// tenant (a empresa ainda não existe). Serve à área da Nebrys (<see cref="IConvitesEmpresa"/>: emitir, reenviar,
/// revogar) e ao cadastro (<see cref="ConferirAsync"/> e <see cref="UsarAsync"/>), que usa esta classe e não o tipo
/// (convenções §1, tabela global nova). Grava no contexto de quem chama: a trilha da área, a empresa nova do cadastro.
/// </summary>
public sealed class RegistroConvitesEmpresa : IConvitesEmpresa
{
    private readonly AcorduDbContext _db;

    public RegistroConvitesEmpresa(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public void Adicionar(ConviteEmpresa convite)
    {
        ArgumentNullException.ThrowIfNull(convite);
        _db.Add(convite);
    }

    public Task<ConviteEmpresa?> ObterAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Set<ConviteEmpresa>().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> HaEmAbertoAsync(string emailNormalizado, CancellationToken cancellationToken = default) =>
        _db.Set<ConviteEmpresa>().AnyAsync(
            c => c.Email == emailNormalizado && c.UsadoEm == null && c.RevogadoEm == null, cancellationToken);

    /// <summary>
    /// O convite do token, para o cadastro (F14-45, F14-49, F14-105): <see cref="ConferenciaConvite.Invalido"/> se não
    /// existe, venceu, foi usado ou revogado (sem distinguir); <see cref="ConferenciaConvite.OutroEmail"/> se vale, mas é
    /// de outro e-mail; <see cref="ConferenciaConvite.Valido"/> se o cadastro pode usá-lo. Não grava nada: o convite só
    /// fica usado em <see cref="UsarAsync"/>, na gravação do cadastro.
    /// </summary>
    public async Task<ConferenciaConvite> ConferirAsync(
        string? token, string emailNormalizado, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        var convite = await PeloTokenAsync(token, cancellationToken);
        if (convite is null || !convite.Validar(token, agora))
            return ConferenciaConvite.Invalido;
        return convite.EhDoEmail(emailNormalizado) ? ConferenciaConvite.Valido : ConferenciaConvite.OutroEmail;
    }

    /// <summary>
    /// O e-mail do convite do token, se ele vale (pendente e dentro da validade), para a tela de cadastro mostrar sem deixar
    /// trocar (F14-46); nulo em qualquer outro caso, sem distinguir o motivo.
    /// </summary>
    public async Task<string?> EmailDoConviteValidoAsync(string? token, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        var convite = await PeloTokenAsync(token, cancellationToken);
        return convite is not null && convite.Validar(token, agora) ? convite.Email : null;
    }

    /// <summary>
    /// Marca o convite como usado no contexto, sem gravar (F14-47): a gravação é a do cadastro, na mesma transação da
    /// empresa. Quem chama já conferiu (<see cref="ConferirAsync"/>); se o convite deixou de valer no meio, falso. A disputa
    /// com outro cadastro, ou com o reenvio e a revogação, aparece na gravação (<see cref="Disputa"/>).
    /// </summary>
    public async Task<bool> UsarAsync(
        string token, string emailNormalizado, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        var convite = await PeloTokenAsync(token, cancellationToken);
        if (convite is null || !convite.Validar(token, agora) || !convite.EhDoEmail(emailNormalizado))
            return false;
        convite.Usar(token, emailNormalizado, agora);
        return true;
    }

    /// <summary>A gravação falhou porque o convite mudou no meio (outro cadastro o usou, ou a área reenviou ou revogou).</summary>
    public static bool Disputa(DbUpdateException excecao)
    {
        ArgumentNullException.ThrowIfNull(excecao);
        return excecao is DbUpdateConcurrencyException concorrencia && concorrencia.Entries.Any(e => e.Entity is ConviteEmpresa);
    }

    private async Task<ConviteEmpresa?> PeloTokenAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;
        var hash = TokenSeguro.Hash(token);
        return await _db.Set<ConviteEmpresa>().SingleOrDefaultAsync(c => c.TokenHash == hash, cancellationToken);
    }
}

/// <summary>Resultado da conferência do convite de empresa no cadastro (<see cref="RegistroConvitesEmpresa.ConferirAsync"/>).</summary>
public enum ConferenciaConvite
{
    /// <summary>Vale e é do e-mail do cadastro.</summary>
    Valido = 1,

    /// <summary>Inexistente, vencido, já usado ou revogado (sem distinguir, F14-45).</summary>
    Invalido = 2,

    /// <summary>Vale, mas é de outro e-mail (F14-49).</summary>
    OutroEmail = 3,
}
