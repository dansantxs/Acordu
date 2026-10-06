using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class VinculoConfiguration : IEntityTypeConfiguration<Vinculo>
{
    public void Configure(EntityTypeBuilder<Vinculo> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "vinculos");
        builder.HasIndex(v => new { v.TenantId, v.UsuarioId }).IsUnique();
        // Login e troca de tenant procuram os vínculos do usuário em todos os tenants.
        builder.HasIndex(v => v.UsuarioId);
        builder.HasIndex(v => new { v.TenantId, v.ClienteId }).HasFilter("cliente_id IS NOT NULL");
    }
}
