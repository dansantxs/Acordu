using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class ContratoConfiguration : IEntityTypeConfiguration<Contrato>
{
    /// <summary>Propriedade só de campo com os meses fechados (primeiro dia de cada mês), em <c>date[]</c>.</summary>
    public const string DatasFechadas = "_periodosFechados";

    public void Configure(EntityTypeBuilder<Contrato> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "contratos");
        ConfiguracaoRaiz.AplicarCliente(builder, indexar: false);

        builder.OwnsOne(c => c.Vigencia, vigencia =>
        {
            vigencia.Property(v => v.Inicio).HasColumnName("vigencia_inicio");
            vigencia.Property(v => v.Fim).HasColumnName("vigencia_fim");
        });
        builder.Navigation(c => c.Vigencia).IsRequired();

        builder.Property(c => c.Franquia).HasConversion(new DuracaoConverter()).HasColumnName("franquia_minutos");
        builder.Property(c => c.Sla).HasConversion(new PoliticaSlaConverter()).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(c => new { c.TenantId, c.ClienteId, c.Status });
        // Apoio à regra "um ATIVO de cada tipo por cliente no período" (RF-CTR-02), checada na Application.
        builder.HasIndex(c => new { c.TenantId, c.ClienteId, c.Tipo }).HasFilter("status = 'ATIVO'");

        builder.Ignore(c => c.PeriodosFechados);
        builder.PrimitiveCollection<List<DateOnly>>(DatasFechadas)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("periodos_fechados")
            .IsRequired();
    }
}
