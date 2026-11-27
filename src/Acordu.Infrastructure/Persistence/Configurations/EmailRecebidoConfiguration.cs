using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Acordu.Infrastructure.Persistence.Configurations;

/// <summary>
/// Registro de entrada (design do F15, D6): a chave de idempotência é única na empresa, pelo índice que começa pelo
/// tenant. Os hashes ficam em <c>character(64)</c>; nada do conteúdo nem dos endereços em claro. Da abertura (F15g), a
/// solicitação (sem FK: o registro vive 7 dias e não segura a solicitação) e os anexos recusados em <c>jsonb</c>; da
/// triagem (F15h), o item, também sem FK (o item sai pelas ações da equipe e pelo expurgo, e o registro fica).
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
        builder.Property(e => e.SolicitacaoId);
        builder.Property(e => e.ItemTriagemId);
        // Conversor não genérico: o EF só converte valores não nulos, e a coluna aceita nulo (nenhum recusado).
        builder.Property(e => e.Recusados).HasConversion((ValueConverter)new AnexosRecusadosConverter()).HasColumnType("jsonb");
    }
}
