using Acordu.Application.Autenticacao;
using Acordu.Application.Cadastro;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql;

namespace Acordu.Infrastructure.Persistence.Aceites;

internal sealed class AceiteTextoConfiguration : IEntityTypeConfiguration<AceiteTexto>
{
    /// <summary>
    /// Pelo modelo do <c>AcorduDbContext</c>, sem que ele dependa do tipo (design do F14, D4: só esta feature usa a tabela;
    /// teste de arquitetura).
    /// </summary>
    public static void Aplicar(ModelBuilder modelo) => modelo.ApplyConfiguration(new AceiteTextoConfiguration());

    public void Configure(EntityTypeBuilder<AceiteTexto> builder)
    {
        builder.ToTable("aceites_textos");
        // Uma linha por conta, texto e versão: a versão vigente com linha não é pendente.
        builder.HasKey(a => new { a.UsuarioId, a.Texto, a.Versao });
        builder.Property(a => a.Versao).HasMaxLength(AceiteTexto.TamanhoVersao);
        builder.Property(a => a.Forma).IsRequired();
        builder.Property(a => a.RegistradoEm);
        // Apagado com a conta (design do F14, D10): o expurgo apaga a conta e o banco leva os aceites junto.
        builder.HasOne<Usuario>().WithMany().HasForeignKey(a => a.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Aceites dos textos legais por conta (RF-AUT-11; design do F14, D10 e D16), na tabela global <c>aceites_textos</c>, sem
/// filtro de tenant (é da pessoa, não do prestador, e não é gravação da empresa). Grava no contexto de quem chama (o
/// cadastro e o aceite de convite, na mesma transação deles) e lista os pendentes do <c>/auth/eu</c>. Com os textos não
/// publicados, não lista nem grava nada (F14-115 a F14-118).
/// </summary>
public sealed class RegistroAceites
{
    private const string ChavePrimaria = "pk_aceites_textos";

    private readonly AcorduDbContext _db;
    private readonly OpcoesTextosLegais _opcoes;

    public RegistroAceites(AcorduDbContext db, OpcoesTextosLegais opcoes)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(opcoes);
        _db = db;
        _opcoes = opcoes;
    }

    /// <summary>
    /// Textos cuja versão vigente a conta ainda não tem (a falta de linha conta como versão aceita nula, F14-119). Vazia com
    /// os textos não publicados (F14-118).
    /// </summary>
    public async Task<IReadOnlyList<TextoPendente>> PendentesAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (!_opcoes.Publicados)
            return [];
        var aceitos = await AceitosVigentesAsync(usuarioId, cancellationToken);
        return [.. TextosLegais.Todos
            .Where(texto => !aceitos.Contains(texto))
            .Select(texto => new TextoPendente(texto, TextosLegais.Vigente(texto)))];
    }

    /// <summary>
    /// Põe no contexto, sem gravar, a versão vigente de cada texto que a conta ainda não tem, na forma dada. Quem chama
    /// grava (na transação dele). A conta nova não tem nada; a existente que já aceitou a vigente não ganha outra linha.
    /// </summary>
    public async Task RegistrarVigentesAsync(
        Guid usuarioId, FormaAceiteTexto forma, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        var aceitos = await AceitosVigentesAsync(usuarioId, cancellationToken);
        foreach (var texto in TextosLegais.Todos.Where(t => !aceitos.Contains(t)))
            _db.Add(AceiteTexto.Registrar(usuarioId, texto, TextosLegais.Vigente(texto), forma, agora));
    }

    /// <summary>A gravação falhou porque outro pedido da mesma conta já gravou a mesma versão (chave primária).</summary>
    public static bool Duplicado(DbUpdateException excecao)
    {
        ArgumentNullException.ThrowIfNull(excecao);
        return excecao.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: ChavePrimaria };
    }

    private async Task<HashSet<TipoTextoLegal>> AceitosVigentesAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var linhas = await _db.Set<AceiteTexto>().AsNoTracking()
            .Where(a => a.UsuarioId == usuarioId)
            .Select(a => new { a.Texto, a.Versao })
            .ToListAsync(cancellationToken);
        return [.. linhas.Where(l => l.Versao == TextosLegais.Vigente(l.Texto)).Select(l => l.Texto)];
    }
}
