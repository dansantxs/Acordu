using Acordu.Application.ListaEspera;
using Acordu.Application.Nebrys;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql;

namespace Acordu.Infrastructure.Persistence.ListaEspera;

internal sealed class EntradaListaEsperaConfiguration : IEntityTypeConfiguration<EntradaListaEspera>
{
    /// <summary>Índice único do e-mail: é ele que impede a entrada repetida (F14-53, design do F14, D12).</summary>
    public const string IndiceEmail = "ix_entradas_lista_espera_email";

    /// <summary>
    /// Pelo modelo do <c>AcorduDbContext</c>, sem que ele dependa do tipo (design do F14, D4 e D12: só esta feature, a
    /// inscrição pública, a área da Nebrys e as consultas dela usam a tabela; teste de arquitetura).
    /// </summary>
    public static void Aplicar(ModelBuilder modelo) => modelo.ApplyConfiguration(new EntradaListaEsperaConfiguration());

    public void Configure(EntityTypeBuilder<EntradaListaEspera> builder)
    {
        builder.ToTable("entradas_lista_espera");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Nome).HasMaxLength(LimitesTexto.Nome).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(LimitesTexto.Email).IsRequired();
        builder.Property(e => e.NomeEmpresa).HasMaxLength(LimitesTexto.Nome).IsRequired();
        builder.Property(e => e.Mensagem).HasMaxLength(LimitesTexto.MensagemListaEspera);
        builder.Property(e => e.CriadaEm);
        builder.Property(e => e.ConvidadaEm);
        builder.Ignore(e => e.Situacao);
        builder.HasIndex(e => e.Email).IsUnique().HasDatabaseName(IndiceEmail);
        // Convidar e descartar a mesma entrada ao mesmo tempo (dois cliques, duas abas): o segundo falha na versão (409).
        builder.Property<uint>(Configurations.ConfiguracaoRaiz.Versao)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");
    }
}

/// <summary>
/// Lista de espera (RF-AUT-08, F14o; design do F14, D12), na tabela global <c>entradas_lista_espera</c>, sem filtro de
/// tenant (a empresa ainda não existe). Serve à página pública (<see cref="IInscricoesListaEspera"/>, que grava na hora,
/// sem trilha) e às ações da área da Nebrys (<see cref="IListaEspera"/>, que gravam no <c>SalvarAsync</c> delas, com a
/// trilha).
/// </summary>
public sealed class RegistroListaEspera : IInscricoesListaEspera, IListaEspera
{
    private readonly AcorduDbContext _db;

    public RegistroListaEspera(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <summary>
    /// Insere e deixa o índice único decidir (F14-53): o e-mail que já está na lista, inclusive o de um envio simultâneo,
    /// cai no <c>23505</c> do índice, e a entrada nova sai do contexto sem nada gravado; a que já estava não muda. Não lê
    /// antes: o índice decide nos dois caminhos, também na corrida entre dois envios.
    /// </summary>
    public async Task GravarSeNovaAsync(EntradaListaEspera entrada, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var linha = _db.Add(entrada);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException excecao) when (Repetida(excecao))
        {
            linha.State = EntityState.Detached;
        }
    }

    public Task<EntradaListaEspera?> ObterAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Set<EntradaListaEspera>().SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void Remover(EntradaListaEspera entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        _db.Remove(entrada);
    }

    private static bool Repetida(DbUpdateException excecao) =>
        excecao.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: EntradaListaEsperaConfiguration.IndiceEmail,
        };
}
