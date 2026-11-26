using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

/// <summary>
/// Registro de entrada (design do F15, D6): a chave de idempotência é única na empresa, pelo índice que começa pelo
/// tenant. Os hashes ficam em <c>character(64)</c>; nada do conteúdo nem dos endereços em claro.
/// </summary>
internal sealed class EmailRecebidoConfiguration : IEntityTypeConfiguration<EmailRecebido>
{
    /// <summary>Nome fixo: a entrada reconhece a corrida de duas entregas iguais por ele (<c>EmailsRecebidos.ChaveRepetida</c>).</summary>
    public const string IndiceChave = "ix_emails_recebidos_tenant_id_chave_entrada";

    public void Configure(EntityTypeBuilder<EmailRecebido> builder)
    {
        ConfiguracaoRaiz.Aplicar(builder, "emails_recebidos");
        builder.Property(e => e.ChaveEntrada).HasMaxLength(EmailRecebido.TamanhoHash).IsFixedLength().IsRequired();
        builder.Property(e => e.RemetenteHash).HasMaxLength(EmailRecebido.TamanhoHash).IsFixedLength();
        builder.HasIndex(e => new { e.TenantId, e.ChaveEntrada }).IsUnique().HasDatabaseName(IndiceChave);
    }
}
