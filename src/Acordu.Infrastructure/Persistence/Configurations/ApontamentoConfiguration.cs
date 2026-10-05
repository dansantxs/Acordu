using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class ApontamentoConfiguration : IEntityTypeConfiguration<Apontamento>
{
    public void Configure(EntityTypeBuilder<Apontamento> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "apontamentos");
        builder.Property(a => a.Duracao).HasConversion(new DuracaoConverter()).HasColumnName("duracao_minutos");
        builder.Property(a => a.Descricao).HasMaxLength(LimitesTexto.DescricaoApontamento).IsRequired();
        builder.Property(a => a.DescricaoExterna).HasMaxLength(LimitesTexto.DescricaoApontamento);
        // Consumo do contrato (read side §3.5): a soma do mês vira index-only scan.
        builder.HasIndex(a => new { a.TenantId, a.ContratoId, a.Data })
            .IncludeProperties(a => new { a.Duracao, a.Faturavel });
        builder.HasIndex(a => new { a.TenantId, a.UsuarioId, a.Data });
        builder.HasIndex(a => new { a.TenantId, a.TarefaId });
        builder.HasIndex(a => new { a.TenantId, a.ClienteId, a.Data });
    }
}
