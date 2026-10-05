using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "clientes");
        builder.Property(c => c.Nome).HasMaxLength(LimitesTexto.Nome).IsRequired();
        builder.Property(c => c.Documento).HasMaxLength(14);
        builder.Property(c => c.Observacoes).HasMaxLength(LimitesTexto.Observacoes);
        builder.HasIndex(c => new { c.TenantId, c.Nome });

        builder.OwnsMany(c => c.Contatos, contato =>
        {
            contato.ToTable("contatos");
            contato.WithOwner().HasForeignKey("ClienteId");
            contato.HasKey(c => c.Id);
            contato.Property(c => c.Id).ValueGeneratedNever();
            contato.Property(c => c.Nome).HasMaxLength(LimitesTexto.Nome).IsRequired();
            contato.Property(c => c.Email).HasMaxLength(LimitesTexto.Email).IsRequired();
            contato.Property(c => c.Telefone).HasMaxLength(LimitesTexto.Telefone);
            contato.Property(c => c.Cargo).HasMaxLength(LimitesTexto.Cargo);
            // Reforço no banco da regra do domínio: e-mail único entre os contatos ativos do cliente.
            contato.HasIndex("ClienteId", nameof(Contato.Email)).IsUnique().HasFilter("ativo");
            contato.HasIndex(c => c.UsuarioId);
        });
        builder.Navigation(c => c.Contatos).HasField("_contatos").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
