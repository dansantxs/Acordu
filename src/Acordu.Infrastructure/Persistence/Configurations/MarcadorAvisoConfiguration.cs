using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class MarcadorAvisoConfiguration : IEntityTypeConfiguration<MarcadorAviso>
{
    public void Configure(EntityTypeBuilder<MarcadorAviso> builder)
    {
        builder.ToTable("marcadores_aviso");
        builder.HasKey(m => new { m.TenantId, m.Referencia, m.Chave });
        builder.Property(m => m.Chave).HasMaxLength(100);
    }
}
