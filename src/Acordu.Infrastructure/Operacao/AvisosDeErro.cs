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
        SELECT count(*) FROM ocorrencias_erro
        WHERE servico = @servico AND avisada_em > @hora AND avisada_em <= @agora AND NOT por_resumo
        """;

    private const string SqlSegurar = "UPDATE ocorrencias_erro SET segurada = true WHERE impressao = @impressao";

    private const string SqlReservar = """
        UPDATE ocorrencias_erro SET avisada_em = @agora, desde_aviso = 0, segurada = false, por_resumo = false
        WHERE impressao = @impressao AND (avisada_em IS NULL OR avisada_em <= @dia)
        """;

    // SKIP LOCKED: outra transação que está com a linha (ela mesma voltando a avisar) fica fora do resumo, sem deadlock.
    private const string SqlResumir = """
        WITH alvo AS (
            SELECT impressao, avisada_em AS antes, desde_aviso AS desde_antes FROM ocorrencias_erro
            WHERE segurada AND impressao <> @impressao
            FOR UPDATE SKIP LOCKED),
        feitas AS (
            UPDATE ocorrencias_erro o SET segurada = false, avisada_em = @agora, desde_aviso = 0, por_resumo = true
            FROM alvo WHERE o.impressao = alvo.impressao
            RETURNING o.impressao, alvo.antes, alvo.desde_antes)
        SELECT impressao, antes, desde_antes FROM feitas
        """;

    // O aviso não saiu: a ocorrência volta a como estava (devida), e as resumidas voltam a seguradas.
    private const string SqlDesfazer = """
        UPDATE ocorrencias_erro SET avisada_em = @antes, desde_aviso = desde_aviso + @desde, segurada = @segurada,
            por_resumo = false
        WHERE impressao = @impressao AND avisada_em = @agora
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

        if (!decisao.Avisar)
            return;
        if (await _mensageiro.EnviarAsync(CanalOperacao.Alertas, Texto(erro, decisao, _opcoes.LinkErros), cancellationToken))
            return;

        // Não saiu (Discord fora ou lento): sem desfazer, o erro ficaria quieto por 24 h sem ninguém saber dele.
        if (decisao.Reserva is { } reserva)
            await DesfazerAsync(reserva);
        else
            _semBanco.TryRemove(new KeyValuePair<string, DateTimeOffset>(Impressao(erro), agora));
    }

    /// <summary>Prazo próprio: o de quem chamou pode ter acabado (é justamente quando o aviso não sai).</summary>
    private async Task DesfazerAsync(Reserva reserva)
    {
        using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await using var escopo = _escopos.CreateAsyncScope();
            var db = escopo.ServiceProvider.GetRequiredService<AcorduDbContext>();
            await using var transacao = await db.Database.BeginTransactionAsync(prazo.Token);
            var conexao = db.Database.GetDbConnection();
            var linhas = reserva.Resumidas.Select(r => (r.Impressao, r.Antes, r.DesdeAntes, Segurada: true))
                .Prepend((reserva.Impressao, reserva.AvisadaAntes, reserva.DesdeAntes, Segurada: false));
            foreach (var (impressao, antes, desde, segurada) in linhas)
            {
                await using var desfazer = Comando(conexao, transacao, SqlDesfazer);
                Parametro(desfazer, "impressao", impressao);
                Parametro(desfazer, "antes", antes is { } valor ? (object)valor : DBNull.Value);
                Parametro(desfazer, "desde", desde);
                Parametro(desfazer, "segurada", segurada);
                Parametro(desfazer, "agora", reserva.Agora);
                await desfazer.ExecuteNonQueryAsync(prazo.Token);
            }

            await transacao.CommitAsync(prazo.Token);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or OperationCanceledException or TimeoutException)
        {
            NaoDesfez(_logger, ex.GetType().Name);
        }
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
            texto.Append(CultureInfo.InvariantCulture,
                $"\n+{decisao.Segurados} {(decisao.Segurados == 1 ? "aviso segurado" : "avisos segurados")} pelo teto da hora.");
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
            decisao = await ReservarAsync(conexao, transacao, impressao, erro.Servico, agora, avisadaEm, desdeAviso, cancellationToken);

        await transacao.CommitAsync(cancellationToken);
        return decisao;
    }

    private async Task<Decisao> ReservarAsync(
        DbConnection conexao, IDbContextTransaction transacao, string impressao, string servico, DateTimeOffset agora, DateTimeOffset? avisadaAntes,
        int desdeAviso, CancellationToken cancellationToken)
    {
        await using (var naHora = Comando(conexao, transacao, SqlNaHora))
        {
            Parametro(naHora, "hora", agora.AddHours(-1));
            Parametro(naHora, "agora", agora);
            Parametro(naHora, "servico", Cortar(servico, 40));
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

        var resumidas = new List<Resumida>();
        await using (var resumir = Comando(conexao, transacao, SqlResumir))
        {
            Parametro(resumir, "impressao", impressao);
            Parametro(resumir, "agora", agora);
            await using var leitor = await resumir.ExecuteReaderAsync(cancellationToken);
            while (await leitor.ReadAsync(cancellationToken))
                resumidas.Add(new Resumida(
                    leitor.GetString(0), leitor.IsDBNull(1) ? null : leitor.GetFieldValue<DateTimeOffset>(1), leitor.GetInt32(2)));
        }

        // O "desde o último aviso" já conta esta vez; o desfazer devolve o que havia antes dela.
        var reserva = new Reserva(impressao, agora, avisadaAntes, desdeAviso, resumidas);
        return new Decisao(true, avisadaAntes is null ? null : desdeAviso, resumidas.Count, SemBanco: false, reserva);
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

    /// <summary>Se avisa; <c>Vezes</c> nulo é "Erro novo"; quantos segurados vão no resumo; a reserva, para desfazer.</summary>
    internal sealed record Decisao(bool Avisar, int? Vezes, int Segurados, bool SemBanco, Reserva? Reserva = null)
    {
        public static readonly Decisao Quieto = new(false, null, 0, false);
    }

    /// <summary>Como a ocorrência e as resumidas estavam antes da reserva.</summary>
    internal sealed record Reserva(
        string Impressao, DateTimeOffset Agora, DateTimeOffset? AvisadaAntes, int DesdeAntes, IReadOnlyList<Resumida> Resumidas);

    internal sealed record Resumida(string Impressao, DateTimeOffset? Antes, int DesdeAntes);

    [LoggerMessage(EventId = 603, Level = LogLevel.Warning,
        Message = "O aviso de erro não saiu e a reserva não foi desfeita ({Falha}); o erro fica quieto até 24 h.")]
    private static partial void NaoDesfez(ILogger logger, string falha);

    [LoggerMessage(EventId = 602, Level = LogLevel.Warning,
        Message = "Deduplicação dos avisos de erro sem banco ({Falha}); deduplicando em memória.")]
    private static partial void SemBanco(ILogger logger, string falha);
}
