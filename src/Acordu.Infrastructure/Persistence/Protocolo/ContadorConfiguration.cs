using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Protocolo;

internal sealed class ContadorConfiguration : IEntityTypeConfiguration<Contador>
{
    public void Configure(EntityTypeBuilder<Contador> builder)
    {
        builder.ToTable("contadores");
        builder.HasKey(c => new { c.TenantId, c.Nome });
        builder.Property(c => c.Nome).HasMaxLength(50);
    }
}
