using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class AjusteSaldoConfiguration : IEntityTypeConfiguration<AjusteSaldo>
{
    public void Configure(EntityTypeBuilder<AjusteSaldo> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "ajustes_saldo");
        ConfiguracaoRaiz.AplicarCliente(builder);
        builder.Property(a => a.Quantidade).HasConversion(new DuracaoAssinadaConverter()).HasColumnName("quantidade_minutos");
        builder.Property(a => a.Motivo).HasMaxLength(LimitesTexto.Motivo).IsRequired();
        builder.HasIndex(a => new { a.TenantId, a.ContratoId, a.Data });
    }
}
