using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class PapelConfiguration : IEntityTypeConfiguration<Papel>
{
    public void Configure(EntityTypeBuilder<Papel> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "papeis");
        builder.Property(p => p.Nome).HasMaxLength(LimitesTexto.NomePapel).IsRequired();
        builder.HasIndex(p => new { p.TenantId, p.Nome }).IsUnique();
        builder.PrimitiveCollection(p => p.Permissoes)
            .HasField("_permissoes")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("permissoes")
            .IsRequired();
    }
}
