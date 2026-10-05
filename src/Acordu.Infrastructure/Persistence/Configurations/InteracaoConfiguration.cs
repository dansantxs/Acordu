using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class InteracaoConfiguration : IEntityTypeConfiguration<Interacao>
{
    public void Configure(EntityTypeBuilder<Interacao> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "interacoes");
        ConfiguracaoRaiz.AplicarCliente(builder);
        builder.Property(i => i.Texto);
        builder.HasIndex(i => new { i.TenantId, i.SolicitacaoId, i.RegistradaEm });

        builder.OwnsMany(i => i.Anexos, anexo =>
        {
            anexo.ToTable("anexos");
            anexo.WithOwner().HasForeignKey("InteracaoId");
            anexo.HasKey(a => a.Id);
            anexo.Property(a => a.Id).ValueGeneratedNever();
            anexo.Property(a => a.NomeArquivo).HasMaxLength(LimitesTexto.NomeArquivo).IsRequired();
            anexo.Property(a => a.TipoConteudo).HasMaxLength(255).IsRequired();
            anexo.Property(a => a.ChaveArmazenamento).HasMaxLength(512).IsRequired();
        });
        builder.Navigation(i => i.Anexos).HasField("_anexos").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
