using Acordu.Infrastructure.Persistence.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class RegistroAuditoriaConfiguration : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
    {
        builder.ToTable("auditoria");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Acao).HasMaxLength(RegistroAuditoria.TamanhoAcao);
        builder.Property(r => r.AlvoTipo).HasMaxLength(RegistroAuditoria.TamanhoAlvoTipo);
        builder.Property(r => r.Ip).HasMaxLength(RegistroAuditoria.TamanhoIp);
        builder.Property(r => r.Dados).HasColumnType("jsonb");
        // Tela de auditoria (mais recentes do tenant) e expurgo por data.
        builder.HasIndex(r => new { r.TenantId, r.OcorridoEm });
        builder.HasIndex(r => r.OcorridoEm);
    }
}
