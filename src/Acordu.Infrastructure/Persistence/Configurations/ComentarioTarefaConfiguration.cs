using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class ComentarioTarefaConfiguration : IEntityTypeConfiguration<ComentarioTarefa>
{
    public void Configure(EntityTypeBuilder<ComentarioTarefa> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "comentarios_tarefa");
        builder.Property(c => c.Texto);
        builder.HasIndex(c => new { c.TenantId, c.TarefaId, c.RegistradoEm });

        builder.OwnsMany(c => c.Anexos, anexo =>
        {
            anexo.ToTable("anexos_comentario_tarefa");
            anexo.WithOwner().HasForeignKey("ComentarioId");
            anexo.HasKey(a => a.Id);
            anexo.Property(a => a.Id).ValueGeneratedNever();
            anexo.Property(a => a.NomeArquivo).HasMaxLength(LimitesTexto.NomeArquivo).IsRequired();
            anexo.Property(a => a.TipoConteudo).HasMaxLength(255).IsRequired();
            anexo.Property(a => a.ChaveArmazenamento).HasMaxLength(512).IsRequired();
        });
        builder.Navigation(c => c.Anexos).HasField("_anexos").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
