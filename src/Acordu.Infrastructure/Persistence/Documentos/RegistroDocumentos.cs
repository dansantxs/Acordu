using Acordu.Application.Cadastro;
using Acordu.Application.Nebrys;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql;

namespace Acordu.Infrastructure.Persistence.Documentos;

internal sealed class DocumentoTesteConfiguration : IEntityTypeConfiguration<DocumentoTeste>
{
    /// <summary>
    /// Pelo modelo do <c>AcorduDbContext</c>, sem que ele dependa do tipo (design do F14, D4: só esta feature e a área da
    /// Nebrys usam a tabela; teste de arquitetura).
    /// </summary>
    public static void Aplicar(ModelBuilder modelo) => modelo.ApplyConfiguration(new DocumentoTesteConfiguration());

    public void Configure(EntityTypeBuilder<DocumentoTeste> builder)
    {
        builder.ToTable("documentos_teste");
        // O hash é a chave: único por construção (F14-57).
        builder.HasKey(d => d.Hash);
        builder.Property(d => d.Hash).HasMaxLength(DocumentoTeste.TamanhoHash).ValueGeneratedNever();
        builder.Property(d => d.PrimeiroTesteEm);
        builder.Property(d => d.LiberadoEm);
        builder.Property(d => d.LiberacaoUsadaEm);
        builder.Ignore(d => d.LiberacaoDisponivel);
        // Dois cadastros que usam a mesma liberação ao mesmo tempo: o segundo falha na versão (F14-59, "um cadastro só").
        builder.Property<uint>(Configurations.ConfiguracaoRaiz.Versao)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");
    }
}

/// <summary>
/// Registro global de "um teste por documento" (RF-AUT-09, design do F14, D8), usado pelo cadastro e pela liberação da
/// área da Nebrys (<see cref="IDocumentosTeste"/>, F14l): sem filtro de tenant (não é dado do prestador) e gravado na
/// mesma transação de quem chama (a empresa nova do cadastro; a trilha da liberação). Não guarda o documento: só o HMAC
/// dele (<see cref="ProtecaoDocumento"/>).
/// </summary>
public sealed class RegistroDocumentos : IDocumentosTeste
{
    private const string ChavePrimaria = "pk_documentos_teste";

    private readonly AcorduDbContext _db;
    private readonly ProtecaoDocumento _protecao;

    public RegistroDocumentos(AcorduDbContext db, ProtecaoDocumento protecao)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(protecao);
        _db = db;
        _protecao = protecao;
    }

    /// <summary>
    /// Reserva o teste do documento (já normalizado pelo domínio) no contexto, sem gravar: documento novo cria o registro;
    /// documento com liberação disponível a usa (F14-59); qualquer outro devolve falso, e quem chama recusa (F14-57,
    /// F14-58). A gravação é a do cadastro; a disputa com outro cadastro simultâneo aparece nela (<see cref="Disputa"/>).
    /// </summary>
    public async Task<bool> ReservarTesteAsync(string documentoNormalizado, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        var hash = _protecao.Hash(documentoNormalizado);
        var registro = await _db.Set<DocumentoTeste>().SingleOrDefaultAsync(d => d.Hash == hash, cancellationToken);
        if (registro is null)
        {
            _db.Add(DocumentoTeste.Registrar(hash, agora));
            return true;
        }

        if (!registro.LiberacaoDisponivel)
            return false;

        registro.UsarLiberacao(agora);
        return true;
    }

    /// <summary>
    /// O registro do documento (já normalizado), achado pelo HMAC e rastreado, para a liberação da Nebrys (F14-71, F14-108):
    /// vale também para o documento de uma empresa já expurgada, da qual só ficou esta linha. Nulo se o documento nunca
    /// teve teste. O documento digitado não é guardado.
    /// </summary>
    public Task<DocumentoTeste?> ObterAsync(string documentoNormalizado, CancellationToken cancellationToken = default)
    {
        var hash = _protecao.Hash(documentoNormalizado);
        return _db.Set<DocumentoTeste>().SingleOrDefaultAsync(d => d.Hash == hash, cancellationToken);
    }

    /// <summary>
    /// Expurgo do cadastro abandonado (RF-AUT-07; decisão 16 do G2 do F14, F14-104): o teste nunca foi usado, então a linha
    /// que o próprio cadastro criou sai, e o documento pode abrir outro teste. Dentro da transação do expurgo, por remoção
    /// direta (o expurgo está fora da guarda e sem contexto de empresa).
    /// <para>
    /// A linha do cadastro é a de <c>PrimeiroTesteEm</c> igual ao <c>Tenant.CriadoEm</c>: o cadastro grava os dois com o
    /// mesmo instante (<c>ServicoAutenticacao.RegistrarAsync</c>), e o teste
    /// <c>ExpurgoEmpresasTests.Registro_DocumentoNovo_RegistroEEmpresaComOMesmoInstante</c> confere o vínculo. O cadastro
    /// que usou uma liberação não criou a linha: ela fica como está, e a liberação usada não volta (decisão do Daniel,
    /// 18/11: "só apagar a linha").
    /// </para>
    /// </summary>
    public Task DesfazerTesteDoCadastroAsync(
        string documentoNormalizado, DateTimeOffset cadastradoEm, CancellationToken cancellationToken = default)
    {
        var hash = _protecao.Hash(documentoNormalizado);
        return _db.Set<DocumentoTeste>()
            .Where(d => d.Hash == hash && d.PrimeiroTesteEm == cadastradoEm)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// A gravação falhou porque outro cadastro, ao mesmo tempo, registrou o mesmo documento (chave primária) ou usou a mesma
    /// liberação (versão): quem chama recusa como documento que já teve teste.
    /// </summary>
    public static bool Disputa(DbUpdateException excecao)
    {
        ArgumentNullException.ThrowIfNull(excecao);
        return excecao is DbUpdateConcurrencyException concorrencia
            ? concorrencia.Entries.Any(e => e.Entity is DocumentoTeste)
            : excecao.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: ChavePrimaria };
    }
}
