using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Seguranca;

internal sealed class TokenContaConfiguration : IEntityTypeConfiguration<TokenConta>
{
    public void Configure(EntityTypeBuilder<TokenConta> builder)
    {
        builder.ToTable("tokens_conta");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Finalidade).HasConversion<string>().HasMaxLength(40);
        builder.Property(t => t.Hash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(t => t.Hash).IsUnique();
        builder.HasIndex(t => new { t.UsuarioId, t.Finalidade });
    }
}
