using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class AcessoPortalConfiguration : IEntityTypeConfiguration<AcessoPortal>
{
    public void Configure(EntityTypeBuilder<AcessoPortal> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "acessos_portal");
        ConfiguracaoRaiz.AplicarCliente(builder);
        builder.Property(a => a.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(a => a.TokenHash).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.SolicitacaoId, a.ContatoId });
    }
}
