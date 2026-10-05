using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Outbox;

internal sealed class MensagemOutboxConfiguration : IEntityTypeConfiguration<MensagemOutbox>
{
    public void Configure(EntityTypeBuilder<MensagemOutbox> builder)
    {
        builder.ToTable("outbox_mensagens");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Sequencia).UseIdentityAlwaysColumn();
        builder.HasIndex(m => m.Sequencia).IsUnique();
        builder.Property(m => m.Tipo).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Conteudo).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.Erro).HasMaxLength(2000);
        // Fila: só as pendentes, na ordem de vencimento.
        builder.HasIndex(m => m.ProximaTentativaEm)
            .HasFilter("processada_em IS NULL AND falhou_em IS NULL");
    }
}
