using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class ConviteEquipeConfiguration : IEntityTypeConfiguration<ConviteEquipe>
{
    public void Configure(EntityTypeBuilder<ConviteEquipe> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "convites_equipe");
        builder.Property(c => c.Email).HasMaxLength(LimitesTexto.Email).IsRequired();
        builder.Property(c => c.TokenHash).HasMaxLength(64).IsFixedLength();
        // Busca do aceite (sem tenant no contexto): pelo hash, só entre os já enviados.
        builder.HasIndex(c => c.TokenHash).IsUnique().HasFilter("token_hash IS NOT NULL");
        builder.HasIndex(c => new { c.TenantId, c.Email });
    }
}
