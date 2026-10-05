using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "categorias");
        builder.Property(c => c.Nome).HasMaxLength(LimitesTexto.NomeCategoria).IsRequired();
        builder.HasIndex(c => new { c.TenantId, c.Nome }).IsUnique();
    }
}
