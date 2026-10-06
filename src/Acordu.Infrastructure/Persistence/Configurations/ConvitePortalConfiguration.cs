using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class ConvitePortalConfiguration : IEntityTypeConfiguration<ConvitePortal>
{
    public void Configure(EntityTypeBuilder<ConvitePortal> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "convites_portal");
        ConfiguracaoRaiz.AplicarCliente(builder, indexar: false);
        builder.Property(c => c.TokenHash).HasMaxLength(64).IsFixedLength();
        // Busca do aceite (sem tenant no contexto): pelo hash, só entre os que já foram enviados.
        builder.HasIndex(c => c.TokenHash).IsUnique().HasFilter("token_hash IS NOT NULL");
        builder.HasIndex(c => new { c.TenantId, c.ClienteId, c.ContatoId });
    }
}
