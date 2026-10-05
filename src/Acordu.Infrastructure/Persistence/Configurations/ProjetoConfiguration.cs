using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class ProjetoConfiguration : IEntityTypeConfiguration<Projeto>
{
    public void Configure(EntityTypeBuilder<Projeto> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "projetos");
        ConfiguracaoRaiz.AplicarCliente(builder);
        builder.Property(p => p.Nome).HasMaxLength(LimitesTexto.Titulo).IsRequired();
        builder.Property(p => p.Estimativa).HasConversion(new DuracaoConverter()).HasColumnName("estimativa_minutos");
    }
}
