using Acordu.Infrastructure.Persistence.Colaboracao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class SeguidorConfiguration : IEntityTypeConfiguration<Seguidor>
{
    public void Configure(EntityTypeBuilder<Seguidor> builder)
    {
        builder.ToTable("seguidores");
        // A chave é a idempotência do ON CONFLICT e serve à leitura "quem segue este alvo".
        builder.HasKey(s => new { s.TenantId, s.Alvo, s.AlvoId, s.UsuarioId });
        builder.Property(s => s.Alvo).HasConversion<int>();
    }
}
