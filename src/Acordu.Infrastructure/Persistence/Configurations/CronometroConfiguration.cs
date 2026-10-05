using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class CronometroConfiguration : IEntityTypeConfiguration<Cronometro>
{
    public void Configure(EntityTypeBuilder<Cronometro> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "cronometros");
        // RF-APT-02: no máximo um cronômetro ativo por usuário (existir = estar ativo).
        builder.HasIndex(c => new { c.TenantId, c.UsuarioId }).IsUnique();
    }
}
