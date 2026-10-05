using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class RecorrenciaTarefaConfiguration : IEntityTypeConfiguration<RecorrenciaTarefa>
{
    public void Configure(EntityTypeBuilder<RecorrenciaTarefa> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "recorrencias");
        ConfiguracaoRaiz.AplicarCliente(builder);
        builder.Property(r => r.Modelo).HasConversion(new ModeloTarefaConverter()).HasColumnType("jsonb").IsRequired();
        // Propriedades só com getter não são mapeadas por convenção: declaradas uma a uma (o EF usa o construtor).
        builder.OwnsOne(r => r.Regra, regra =>
        {
            regra.Property(x => x.Periodicidade).HasColumnName("regra_periodicidade");
            regra.Property(x => x.DiaReferencia).HasColumnName("regra_dia_referencia");
            regra.Property(x => x.AntecedenciaDias).HasColumnName("regra_antecedencia_dias");
            regra.Property(x => x.Inicio).HasColumnName("regra_inicio");
            regra.Property(x => x.Fim).HasColumnName("regra_fim");
        });
        builder.Navigation(r => r.Regra).IsRequired();
        // Varredura do agendador: recorrências ativas com geração devida.
        builder.HasIndex(r => new { r.Status, r.ProximaData });
    }
}
