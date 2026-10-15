using Acordu.Infrastructure.Persistence.Atividades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class AtividadeConfiguration : IEntityTypeConfiguration<Atividade>
{
    public void Configure(EntityTypeBuilder<Atividade> builder)
    {
        builder.ToTable("atividades");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Sequencia).UseIdentityAlwaysColumn();
        builder.Property(a => a.Tipo).HasMaxLength(40);
        builder.Property(a => a.Alvo).HasConversion<int>();
        builder.Property(a => a.StatusDe).HasMaxLength(40);
        builder.Property(a => a.StatusPara).HasMaxLength(40);
        // Idempotência (ON CONFLICT), o feed do tenant e o filtro por cliente, mais recentes primeiro; expurgo por data.
        builder.HasIndex(a => new { a.TenantId, a.Chave }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.Sequencia });
        builder.HasIndex(a => new { a.TenantId, a.ClienteId, a.Sequencia });
        builder.HasIndex(a => a.OcorridaEm);
    }
}
