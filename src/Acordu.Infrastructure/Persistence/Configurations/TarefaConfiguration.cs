using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class TarefaConfiguration : IEntityTypeConfiguration<Tarefa>
{
    public void Configure(EntityTypeBuilder<Tarefa> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "tarefas");
        builder.Property(t => t.Titulo).HasMaxLength(LimitesTexto.Titulo).IsRequired();
        builder.Property(t => t.Descricao);
        builder.Property(t => t.Estimativa).HasConversion(new DuracaoConverter()).HasColumnName("estimativa_minutos");
        builder.HasIndex(t => new { t.TenantId, t.ClienteId, t.Status });
        builder.HasIndex(t => new { t.TenantId, t.SolicitacaoId });
        builder.HasIndex(t => new { t.TenantId, t.ProjetoId });
        // Idempotência da recorrência (RF-REC-03): no máximo uma tarefa por recorrência e data de referência.
        builder.HasIndex(t => new { t.RecorrenciaId, t.DataReferencia })
            .IsUnique()
            .HasFilter("recorrencia_id IS NOT NULL");

        builder.OwnsMany(t => t.Checklist, item =>
        {
            item.ToTable("itens_checklist");
            item.WithOwner().HasForeignKey("TarefaId");
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedNever();
            item.Property(i => i.Texto).HasMaxLength(LimitesTexto.ItemChecklist).IsRequired();
        });
        builder.Navigation(t => t.Checklist).HasField("_checklist").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
