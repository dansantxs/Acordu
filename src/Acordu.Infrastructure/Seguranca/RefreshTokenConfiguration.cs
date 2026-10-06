using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Seguranca;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Hash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(r => r.Hash).IsUnique();
        builder.HasIndex(r => new { r.UsuarioId, r.TenantId });
        // Duas renovações simultâneas do mesmo token: a segunda falha em vez de gerar duas sessões.
        builder.Property<uint>("Versao").IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
    }
}
