using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Nome).HasMaxLength(LimitesTexto.Nome).IsRequired();
        builder.Property<uint>(ConfiguracaoRaiz.Versao)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");
    }
}
