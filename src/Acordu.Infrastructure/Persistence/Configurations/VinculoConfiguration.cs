using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class VinculoConfiguration : IEntityTypeConfiguration<Vinculo>
{
    public void Configure(EntityTypeBuilder<Vinculo> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "vinculos");
        builder.Property(v => v.CapacidadeSemanal).HasConversion(new DuracaoConverter()).HasColumnName("capacidade_semanal_minutos");
        builder.HasIndex(v => new { v.TenantId, v.UsuarioId }).IsUnique();
        // Login e troca de tenant procuram os vínculos do usuário em todos os tenants.
        builder.HasIndex(v => v.UsuarioId);
        builder.HasIndex(v => new { v.TenantId, v.ClienteId }).HasFilter("cliente_id IS NOT NULL");
    }
}
