using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class CalendarioUtilConfiguration : IEntityTypeConfiguration<CalendarioUtil>
{
    public void Configure(EntityTypeBuilder<CalendarioUtil> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "calendarios");
        builder.HasIndex(c => c.TenantId).IsUnique();
        builder.Property(c => c.FusoHorario).HasMaxLength(64).IsRequired();
        builder.Ignore(c => c.Fuso);

        builder.OwnsMany(c => c.Expediente, janela =>
        {
            janela.ToTable("calendario_expediente");
            janela.WithOwner().HasForeignKey("CalendarioUtilId");
            janela.HasKey("CalendarioUtilId", "Dia");
            janela.Property(j => j.Dia).HasConversion<int>();
            janela.Property(j => j.Inicio).HasColumnType("time");
            janela.Property(j => j.Fim).HasColumnType("time");
        });
        builder.Navigation(c => c.Expediente).HasField("_expediente").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.PrimitiveCollection(c => c.Feriados)
            .HasField("_feriados")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("feriados");
    }
}
