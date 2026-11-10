using System.Collections.Concurrent;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Acordu.Application.Abstractions.Operacao;
using Acordu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acordu.Infrastructure.Operacao;

/// <summary>Teto de avisos de erro por hora e o link do Error Reporting (<c>Operacao__LinkErros</c>, opcional).</summary>
public sealed record OpcoesAvisosDeErro(int TetoPorHora = OpcoesAvisosDeErro.TetoPadrao, Uri? LinkErros = null)
{
    public const int TetoPadrao = 20;
}

/// <summary>
/// Deduplicação dos avisos de erro na tabela global <c>ocorrencias_erro</c> (ADR 0012, F13-05 a F13-08), por SQL
/// atômico, num escopo próprio (o <c>DbContext</c> da requisição que falhou pode estar numa transação abortada):
/// <list type="number">
/// <item>conta a ocorrência (<c>INSERT ... ON CONFLICT DO UPDATE</c>);</item>
/// <item>nunca avisada, ou avisada há 24 h ou mais: é devida;</item>
/// <item>devida, mas o teto da hora já foi: fica segurada;</item>
/// <item>senão, reserva o aviso com um <c>UPDATE</c> condicional (duas instâncias não avisam o mesmo erro) e leva
/// junto o resumo das seguradas.</item>
/// </list>
/// Sem banco (o erro pode ser ele), deduplica em memória e avisa assim mesmo. Nunca lança.
/// </summary>
public sealed partial class AvisosDeErro : IAvisosDeErro
{
    private const string SqlContar = """
        INSERT INTO ocorrencias_erro (impressao, servico, tipo, quadro, rota, versao, primeira_em, ultima_em,
            por_resumo, segurada, total, desde_aviso)
        VALUES (@impressao, @servico, @tipo, @quadro, @rota, @versao, @agora, @agora, false, false, 1, 1)
        ON CONFLICT (impressao) DO UPDATE SET ultima_em = @agora, versao = @versao,
            total = ocorrencias_erro.total + 1, desde_aviso = ocorrencias_erro.desde_aviso + 1
        RETURNING avisada_em, desde_aviso
        """;

    private const string SqlNaHora = """
        SELECT count(*) FROM ocorrencias_erro WHERE avisada_em > @hora AND NOT por_resumo
        """;

    private const string SqlSegurar = "UPDATE ocorrencias_erro SET segurada = true WHERE impressao = @impressao";

    private const string SqlReservar = """
        UPDATE ocorrencias_erro SET avisada_em = @agora, desde_aviso = 0, segurada = false, por_resumo = false
        WHERE impressao = @impressao AND (avisada_em IS NULL OR avisada_em <= @dia)
        """;

    private const string SqlResumir = """
        WITH resumidas AS (
            UPDATE ocorrencias_erro SET segurada = false, avisada_em = @agora, desde_aviso = 0, por_resumo = true
            WHERE segurada AND impressao <> @impressao
            RETURNING 1)
        SELECT count(*) FROM resumidas
        """;

    private static readonly TimeSpan Quieto = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _escopos;
    private readonly IMensageiroOperacao _mensageiro;
    private readonly TimeProvider _relogio;
    private readonly OpcoesAvisosDeErro _opcoes;
    private readonly ILogger<AvisosDeErro> _logger;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _semBanco = new(StringComparer.Ordinal);

    public AvisosDeErro(
        IServiceScopeFactory escopos,
        IMensageiroOperacao mensageiro,
        TimeProvider relogio,
        OpcoesAvisosDeErro opcoes,
        ILogger<AvisosDeErro> logger)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(mensageiro);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(logger);
        _escopos = escopos;
        _mensageiro = mensageiro;
        _relogio = relogio;
        _opcoes = opcoes;
        _logger = logger;
    }

    /// <summary>Impressão digital: SHA-256 de serviço, tipo, quadro e rota (a versão fica de fora: o erro é o mesmo).</summary>
    public static string Impressao(ErroInesperado erro)
    {
        ArgumentNullException.ThrowIfNull(erro);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{erro.Servico}|{erro.Tipo}|{erro.Quadro}|{erro.Rota}"));
        return Convert.ToHexStringLower(bytes);
    }

    public async Task AvisarAsync(ErroInesperado erro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(erro);
        if (!_mensageiro.Configurado(CanalOperacao.Alertas))
            return;

        var agora = _relogio.GetUtcNow();
        Decisao decisao;
        try
        {
            decisao = await DecidirNoBancoAsync(erro, agora, cancellationToken);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or OperationCanceledException or TimeoutException)
        {
            SemBanco(_logger, ex.GetType().Name);
            decisao = DecidirEmMemoria(Impressao(erro), agora);
        }

        if (decisao.Avisar)
            await _mensageiro.EnviarAsync(CanalOperacao.Alertas, Texto(erro, decisao, _opcoes.LinkErros), cancellationToken);
    }

    internal static string Texto(ErroInesperado erro, Decisao decisao, Uri? link)
    {
        var titulo = decisao.Vezes is { } vezes
            ? $"**Erro de novo** ({vezes} {(vezes == 1 ? "vez" : "vezes")} desde o último aviso)"
            : "**Erro novo**";
        var texto = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"{titulo} · {erro.Servico} `{erro.Versao}`\n")
            .Append(CultureInfo.InvariantCulture, $"`{erro.Tipo}`")
            .Append(erro.Quadro is null ? "" : $" em `{erro.Quadro}`")
            .Append(CultureInfo.InvariantCulture, $"\n`{erro.Rota}`");
        if (decisao.Segurados > 0)
            texto.Append(CultureInfo.InvariantCulture, $"\n+{decisao.Segurados} avisos segurados pelo teto da hora.");
        if (decisao.SemBanco)
            texto.Append("\n(sem banco: deduplicado só nesta instância)");
        if (link is not null)
            texto.Append('\n').Append(link);
        return texto.ToString();
    }

    private async Task<Decisao> DecidirNoBancoAsync(ErroInesperado erro, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var impressao = Impressao(erro);
        await using var escopo = _escopos.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AcorduDbContext>();
        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);
        var conexao = db.Database.GetDbConnection();

        DateTimeOffset? avisadaEm;
        int desdeAviso;
        await using (var contar = Comando(conexao, transacao, SqlContar))
        {
            Parametro(contar, "impressao", impressao);
            Parametro(contar, "servico", Cortar(erro.Servico, 40));
            Parametro(contar, "tipo", Cortar(erro.Tipo, 300));
            Parametro(contar, "quadro", erro.Quadro is null ? DBNull.Value : Cortar(erro.Quadro, 300));
            Parametro(contar, "rota", Cortar(erro.Rota, 300));
            Parametro(contar, "versao", Cortar(erro.Versao, 40));
            Parametro(contar, "agora", agora);
            await using var leitor = await contar.ExecuteReaderAsync(cancellationToken);
            await leitor.ReadAsync(cancellationToken);
            avisadaEm = leitor.IsDBNull(0) ? null : leitor.GetFieldValue<DateTimeOffset>(0);
            desdeAviso = leitor.GetInt32(1);
        }

        var decisao = Decisao.Quieto;
        if (avisadaEm is null || avisadaEm <= agora - Quieto)
            decisao = await ReservarAsync(conexao, transacao, impressao, agora, avisadaEm is null ? null : desdeAviso, cancellationToken);

        await transacao.CommitAsync(cancellationToken);
        return decisao;
    }

    private async Task<Decisao> ReservarAsync(
        DbConnection conexao, IDbContextTransaction transacao, string impressao, DateTimeOffset agora, int? vezes,
        CancellationToken cancellationToken)
    {
        await using (var naHora = Comando(conexao, transacao, SqlNaHora))
        {
            Parametro(naHora, "hora", agora.AddHours(-1));
            if (Convert.ToInt32(await naHora.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) >= _opcoes.TetoPorHora)
            {
                await using var segurar = Comando(conexao, transacao, SqlSegurar);
                Parametro(segurar, "impressao", impressao);
                await segurar.ExecuteNonQueryAsync(cancellationToken);
                return Decisao.Quieto;
            }
        }

        await using (var reservar = Comando(conexao, transacao, SqlReservar))
        {
            Parametro(reservar, "impressao", impressao);
            Parametro(reservar, "agora", agora);
            Parametro(reservar, "dia", agora - Quieto);
            // Outra instância reservou primeiro: ela avisa.
            if (await reservar.ExecuteNonQueryAsync(cancellationToken) == 0)
                return Decisao.Quieto;
        }

        await using var resumir = Comando(conexao, transacao, SqlResumir);
        Parametro(resumir, "impressao", impressao);
        Parametro(resumir, "agora", agora);
        var segurados = Convert.ToInt32(await resumir.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        return new Decisao(true, vezes, segurados, SemBanco: false);
    }

    private Decisao DecidirEmMemoria(string impressao, DateTimeOffset agora)
    {
        var avisar = false;
        _semBanco.AddOrUpdate(
            impressao,
            _ =>
            {
                avisar = true;
                return agora;
            },
            (_, anterior) =>
            {
                avisar = anterior <= agora - Quieto;
                return avisar ? agora : anterior;
            });
        return avisar ? new Decisao(true, null, 0, SemBanco: true) : Decisao.Quieto;
    }

    private static DbCommand Comando(DbConnection conexao, IDbContextTransaction transacao, string sql)
    {
        var comando = conexao.CreateCommand();
        comando.Transaction = transacao.GetDbTransaction();
        comando.CommandText = sql;
        return comando;
    }

    private static void Parametro(DbCommand comando, string nome, object valor)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }

    private static string Cortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];

    /// <summary>Se avisa; <c>Vezes</c> nulo é "Erro novo"; quantos segurados vão no resumo.</summary>
    internal sealed record Decisao(bool Avisar, int? Vezes, int Segurados, bool SemBanco)
    {
        public static readonly Decisao Quieto = new(false, null, 0, false);
    }

    [LoggerMessage(EventId = 602, Level = LogLevel.Warning,
        Message = "Deduplicação dos avisos de erro sem banco ({Falha}); deduplicando em memória.")]
    private static partial void SemBanco(ILogger logger, string falha);
}
