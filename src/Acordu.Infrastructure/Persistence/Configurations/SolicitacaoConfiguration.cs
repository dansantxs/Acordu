using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class SolicitacaoConfiguration : IEntityTypeConfiguration<Solicitacao>
{
    public void Configure(EntityTypeBuilder<Solicitacao> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "solicitacoes");
        ConfiguracaoRaiz.AplicarCliente(builder, indexar: false);

        builder.Property(s => s.Titulo).HasMaxLength(LimitesTexto.Titulo).IsRequired();
        builder.Property(s => s.Descricao);
        builder.Property(s => s.MotivoCancelamento).HasMaxLength(LimitesTexto.Motivo);
        builder.HasIndex(s => new { s.TenantId, s.Numero }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.ClienteId, s.Status });

        // Colunas do relógio com nomes fixos: o read side filtra por prazo_resolucao e limite_risco (§5).
        builder.OwnsOne(s => s.Sla, sla =>
        {
            sla.Property(r => r.AbertoEm).HasColumnName("sla_aberto_em");
            sla.Property(r => r.InicioCiclo).HasColumnName("sla_inicio_ciclo");
            sla.Property(r => r.TempoPrimeiraResposta).HasConversion(new DuracaoConverter())
                .HasColumnName("sla_tempo_primeira_resposta_minutos");
            sla.Property(r => r.TempoResolucao).HasConversion(new DuracaoConverter())
                .HasColumnName("sla_tempo_resolucao_minutos");
            sla.Property(r => r.PausadoNoCiclo).HasConversion(new DuracaoConverter())
                .HasColumnName("sla_pausado_no_ciclo_minutos");
            sla.Property(r => r.PausadoDesde).HasColumnName("sla_pausado_desde");
            sla.Property(r => r.PrazoPrimeiraResposta).HasColumnName("prazo_primeira_resposta");
            sla.Property(r => r.PrazoResolucao).HasColumnName("prazo_resolucao");
            sla.Property(r => r.LimiteRisco).HasColumnName("limite_risco");
        });

        builder.OwnsOne(s => s.Solucao, solucao =>
        {
            solucao.Property(x => x.Texto).HasColumnName("solucao_texto");
            solucao.Property(x => x.Problema).HasColumnName("solucao_problema").HasMaxLength(LimitesTexto.SolucaoProblemaCausa);
            solucao.Property(x => x.Causa).HasColumnName("solucao_causa").HasMaxLength(LimitesTexto.SolucaoProblemaCausa);
        });
    }
}
