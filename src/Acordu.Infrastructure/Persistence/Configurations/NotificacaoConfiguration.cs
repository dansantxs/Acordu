using Acordu.Infrastructure.Persistence.Notificacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class NotificacaoConfiguration : IEntityTypeConfiguration<Notificacao>
{
    public void Configure(EntityTypeBuilder<Notificacao> builder)
    {
        builder.ToTable("notificacoes");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Categoria).HasMaxLength(40);
        builder.Property(n => n.Chave).HasMaxLength(Notificacao.TamanhoChave);
        builder.Property(n => n.Titulo).HasMaxLength(Notificacao.TamanhoTitulo);
        builder.Property(n => n.Link).HasMaxLength(Notificacao.TamanhoLink);
        // Idempotência do registro (ON CONFLICT) e o sino de cada pessoa, mais recentes primeiro.
        builder.HasIndex(n => new { n.TenantId, n.UsuarioId, n.Chave }).IsUnique();
        builder.HasIndex(n => new { n.TenantId, n.UsuarioId, n.CriadaEm });
        builder.HasIndex(n => n.CriadaEm);
    }
}

internal sealed class PreferenciaNotificacaoConfiguration : IEntityTypeConfiguration<PreferenciaNotificacao>
{
    public void Configure(EntityTypeBuilder<PreferenciaNotificacao> builder)
    {
        builder.ToTable("preferencias_notificacao");
        builder.HasKey(p => new { p.TenantId, p.UsuarioId });
        builder.Property(p => p.EmailDesligado);
    }
}
