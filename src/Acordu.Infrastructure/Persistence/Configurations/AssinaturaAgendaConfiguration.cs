using Acordu.Infrastructure.Persistence.Agenda;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class AssinaturaAgendaConfiguration : IEntityTypeConfiguration<AssinaturaAgendaRegistro>
{
    public void Configure(EntityTypeBuilder<AssinaturaAgendaRegistro> builder)
    {
        builder.ToTable("assinaturas_agenda");
        builder.HasKey(a => new { a.TenantId, a.UsuarioId });
        builder.Property(a => a.TokenHash).HasMaxLength(64).IsRequired();
        // O feed procura pelo hash em todos os tenants.
        builder.HasIndex(a => a.TokenHash).IsUnique();
    }
}
