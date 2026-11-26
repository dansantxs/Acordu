using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

/// <summary>
/// Endereço da conversa (design do F15, D3 e D9): uma por solicitação e o token único na empresa, os dois por índice único
/// começando pelo tenant. O token fica em claro: é identificador de roteamento, não segredo (convenções §1).
/// </summary>
internal sealed class ConversaPorEmailConfiguration : IEntityTypeConfiguration<ConversaPorEmail>
{
    public void Configure(EntityTypeBuilder<ConversaPorEmail> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "conversas_por_email");
        ConfiguracaoRaiz.AplicarCliente(builder);
        builder.Property(c => c.Token).HasMaxLength(ConversaPorEmail.TamanhoToken).IsFixedLength().IsRequired();
        builder.Property(c => c.MessageIdOrigem).HasMaxLength(ConversaPorEmail.TamanhoCabecalho);
        builder.Property(c => c.AssuntoOrigem).HasMaxLength(ConversaPorEmail.TamanhoCabecalho);
        builder.HasIndex(c => new { c.TenantId, c.SolicitacaoId }).IsUnique();
        builder.HasIndex(c => new { c.TenantId, c.Token }).IsUnique();
    }
}
