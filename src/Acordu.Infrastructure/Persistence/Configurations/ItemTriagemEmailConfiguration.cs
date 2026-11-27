using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Acordu.Infrastructure.Persistence.Configurations;

/// <summary>
/// Triagem dos e-mails não identificados (design do F15, D10; F15h): raiz por tenant, sem cliente. Os clientes possíveis
/// em <c>uuid[]</c>, os recusados em <c>jsonb</c> (como no registro de entrada) e os anexos válidos numa tabela filha, que
/// sai em cascata com o item. O índice por (tenant, recebimento) serve à lista do mais novo e ao expurgo de 30 dias (F15k).
/// </summary>
internal sealed class ItemTriagemEmailConfiguration : IEntityTypeConfiguration<ItemTriagemEmail>
{
    public void Configure(EntityTypeBuilder<ItemTriagemEmail> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "itens_triagem_email");
        builder.Property(i => i.RemetenteEmail).HasMaxLength(LimitesTexto.Email);
        builder.Property(i => i.RemetenteNome).HasMaxLength(LimitesTexto.Nome);
        builder.Property(i => i.Assunto).HasMaxLength(ItemTriagemEmail.TamanhoAssunto);
        builder.Property(i => i.Texto);
        builder.HasIndex(i => new { i.TenantId, i.RecebidoEm });
        builder.PrimitiveCollection(i => i.ClientesPossiveis)
            .HasField("_clientesPossiveis")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("clientes_possiveis")
            .IsRequired();
        // Conversor não genérico: o EF só converte valores não nulos, e a coluna aceita nulo (nenhum recusado).
        builder.Property(i => i.Recusados).HasConversion((ValueConverter)new AnexosRecusadosConverter()).HasColumnType("jsonb");

        builder.OwnsMany(i => i.Anexos, anexo =>
        {
            anexo.ToTable("anexos_triagem_email");
            anexo.WithOwner().HasForeignKey("ItemTriagemId");
            anexo.HasKey(a => a.Id);
            anexo.Property(a => a.Id).ValueGeneratedNever();
            anexo.Property(a => a.NomeArquivo).HasMaxLength(LimitesTexto.NomeArquivo).IsRequired();
            anexo.Property(a => a.TipoConteudo).HasMaxLength(255).IsRequired();
            anexo.Property(a => a.ChaveArmazenamento).HasMaxLength(512).IsRequired();
        });
        builder.Navigation(i => i.Anexos).HasField("_anexos").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
