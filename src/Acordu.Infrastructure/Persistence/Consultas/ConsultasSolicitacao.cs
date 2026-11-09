using System.Globalization;
using Acordu.Application.Abstractions;
using Acordu.Application.Anexos;
using Acordu.Application.Common;
using Acordu.Application.Solicitacoes;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>
/// Fila, detalhe e linha do tempo de solicitações, pelos filtros globais. A situação de SLA da fila é filtrada no banco
/// pelos instantes persistidos (read side §5) e calculada pela regra do domínio só para as linhas da página.
/// </summary>
public sealed class ConsultasSolicitacao : IConsultasSolicitacao
{
    private static readonly StatusSolicitacao[] EmAberto =
    [
        StatusSolicitacao.Aberta, StatusSolicitacao.EmAndamento, StatusSolicitacao.AguardandoCliente, StatusSolicitacao.AguardandoTerceiro,
    ];

    private readonly AcorduDbContext _db;
    private readonly ConsultaSql _sql;
    private readonly ICalendarios _calendarios;
    private readonly TimeProvider _relogio;

    public ConsultasSolicitacao(AcorduDbContext db, ConsultaSql sql, ICalendarios calendarios, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(calendarios);
        ArgumentNullException.ThrowIfNull(relogio);
        _db = db;
        _sql = sql;
        _calendarios = calendarios;
        _relogio = relogio;
    }

    public async Task<ResultadoPaginado<SolicitacaoResumoDto>> ListarAsync(
        FiltroSolicitacoes filtro, Paginacao paginacao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var agora = _relogio.GetUtcNow();
        var calendario = await _calendarios.DoTenantAsync(cancellationToken);
        var consulta = Filtrar(_db.Solicitacoes.AsNoTracking(), filtro, agora, calendario);

        var total = await consulta.CountAsync(cancellationToken);
        var pagina = await Ordenar(consulta, filtro.Ordem)
            .Skip(paginacao.Pular).Take(paginacao.TamanhoPagina)
            .ToListAsync(cancellationToken);

        var clientes = await NomesDeClientesAsync(pagina.Select(s => s.ClienteId), cancellationToken);
        var pessoas = await NomesDeUsuariosAsync(pagina.Where(s => s.ResponsavelId is not null).Select(s => s.ResponsavelId!.Value), cancellationToken);
        return new ResultadoPaginado<SolicitacaoResumoDto>(
            [.. pagina.Select(s => new SolicitacaoResumoDto(
                s.Id,
                s.Numero,
                s.Titulo,
                new Referencia(s.ClienteId, clientes.GetValueOrDefault(s.ClienteId, string.Empty)),
                s.Prioridade,
                s.Status,
                s.StatusExterno,
                Pessoa(s.ResponsavelId, pessoas),
                s.AbertaEm,
                s.Sla?.PrazoResolucao,
                s.SituacaoSla(agora, calendario)))],
            paginacao.Pagina,
            paginacao.TamanhoPagina,
            total);
    }

    public async Task<SolicitacaoDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var linha = await _db.Solicitacoes.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { Solicitacao = s, Versao = EF.Property<uint>(s, ConfiguracaoRaiz.Versao) })
            .SingleOrDefaultAsync(cancellationToken);
        if (linha is null)
            return null;

        var s = linha.Solicitacao;
        var agora = _relogio.GetUtcNow();
        var calendario = await _calendarios.DoTenantAsync(cancellationToken);
        var cliente = await _db.Clientes.AsNoTracking().SingleAsync(c => c.Id == s.ClienteId, cancellationToken);
        var contato = s.ContatoId is { } contatoId ? cliente.Contatos.FirstOrDefault(c => c.Id == contatoId) : null;
        var pessoas = await NomesDeUsuariosAsync(s.ResponsavelId is { } r ? [r] : [], cancellationToken);
        var tarefas = await _db.Tarefas.AsNoTracking()
            .Where(t => t.SolicitacaoId == id)
            .OrderBy(t => t.Id)
            .Select(t => new TarefaDaSolicitacaoDto(t.Id, t.Titulo, t.Status, t.ProjetoId))
            .ToListAsync(cancellationToken);
        var apontado = (await _sql.ListarAsync<Soma>(e => $"""
            SELECT coalesce(sum(a.duracao_minutos), 0)::int AS total
            FROM apontamentos a
            JOIN tarefas t ON t.id = a.tarefa_id AND t.tenant_id = a.tenant_id
            WHERE a.tenant_id = {e.TenantId} AND t.solicitacao_id = {id}
              AND (CAST({e.ClienteId} AS uuid) IS NULL OR a.cliente_id = {e.ClienteId})
            """, cancellationToken)).Single().Total;

        return new SolicitacaoDto(
            s.Id,
            s.Numero,
            linha.Versao.ToString(CultureInfo.InvariantCulture),
            s.Titulo,
            s.Descricao,
            new Referencia(cliente.Id, cliente.Nome),
            contato is null ? null : new Referencia(contato.Id, contato.Nome),
            s.ContratoId,
            s.CategoriaId,
            s.Prioridade,
            s.Status,
            s.StatusExterno,
            s.Origem,
            Pessoa(s.ResponsavelId, pessoas),
            s.AbertaEm,
            s.Sla is { } sla
                ? new SlaDto(
                    sla.PrazoPrimeiraResposta,
                    s.PrimeiraRespostaEm,
                    sla.PrazoResolucao,
                    sla.Pausado,
                    s.SituacaoSla(agora, calendario),
                    sla.PercentualResolucao(agora, calendario))
                : null,
            s.SituacaoSla(agora, calendario),
            apontado,
            tarefas,
            s.Solucao is { } solucao ? new SolucaoDto(solucao.Problema, solucao.Causa, solucao.Texto) : null,
            s.ResolvidaEm,
            s.FechadaEm,
            s.CanceladaEm,
            s.MotivoCancelamento);
    }

    public async Task<IReadOnlyList<InteracaoDto>?> InteracoesAsync(Guid solicitacaoId, CancellationToken cancellationToken = default)
    {
        var clienteId = await _db.Solicitacoes
            .Where(s => s.Id == solicitacaoId)
            .Select(s => (Guid?)s.ClienteId)
            .SingleOrDefaultAsync(cancellationToken);
        if (clienteId is null)
            return null;

        var interacoes = await _db.Interacoes.AsNoTracking()
            .Where(i => i.SolicitacaoId == solicitacaoId)
            .OrderBy(i => i.RegistradaEm).ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);
        var pessoas = await Nomes.DeAutoresAsync(_db, clienteId.Value, interacoes.Select(i => i.AutorId), cancellationToken);
        return [.. interacoes.Select(i => new InteracaoDto(
            i.Id,
            new Referencia(i.AutorId, pessoas.GetValueOrDefault(i.AutorId, string.Empty)),
            i.AutorEhCliente,
            i.Texto,
            i.Visibilidade,
            i.RegistradaEm,
            [.. i.Anexos.OrderBy(a => a.NomeArquivo, StringComparer.Ordinal).Select(a => new AnexoDto(a.Id, a.NomeArquivo, a.TipoConteudo, a.TamanhoBytes))]))];
    }

    public async Task<AnexoArmazenado?> AnexoAsync(Guid anexoId, CancellationToken cancellationToken = default) =>
        await _db.Interacoes.AsNoTracking()
            .SelectMany(i => i.Anexos)
            .Where(a => a.Id == anexoId)
            .Select(a => new AnexoArmazenado(a.ChaveArmazenamento, a.NomeArquivo, a.TipoConteudo))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ResolvidasAteAsync(DateTimeOffset limite, CancellationToken cancellationToken = default) =>
        await _db.Solicitacoes.AsNoTracking()
            .Where(s => s.Status == StatusSolicitacao.Resolvida && s.ResolvidaEm != null && s.ResolvidaEm <= limite)
            .OrderBy(s => s.ResolvidaEm)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> CandidatasAAlertaDeSlaAsync(DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        await _db.Solicitacoes.AsNoTracking()
            .Where(s => EmAberto.Contains(s.Status) && s.Sla != null && s.Sla.LimiteRisco <= agora)
            .OrderBy(s => s.Sla!.PrazoResolucao)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

    private static IQueryable<Solicitacao> Filtrar(
        IQueryable<Solicitacao> consulta, FiltroSolicitacoes filtro, DateTimeOffset agora, CalendarioUtil calendario)
    {
        if (filtro.Status is { Count: > 0 } status)
            consulta = consulta.Where(s => status.Contains(s.Status));
        if (filtro.ClienteId is { } clienteId)
            consulta = consulta.Where(s => s.ClienteId == clienteId);
        if (filtro.ResponsavelId is { } responsavelId)
            consulta = consulta.Where(s => s.ResponsavelId == responsavelId);
        if (filtro.SemResponsavel)
            consulta = consulta.Where(s => s.ResponsavelId == null);
        if (filtro.Prioridade is { } prioridade)
            consulta = consulta.Where(s => s.Prioridade == prioridade);
        if (filtro.CategoriaId is { } categoriaId)
            consulta = consulta.Where(s => s.CategoriaId == categoriaId);
        if (filtro.AbertaDe is { } de)
        {
            var desde = Inicio(de, calendario);
            consulta = consulta.Where(s => s.AbertaEm >= desde);
        }

        if (filtro.AbertaAte is { } ate)
        {
            var antesDe = Inicio(ate.AddDays(1), calendario);
            consulta = consulta.Where(s => s.AbertaEm < antesDe);
        }
        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            var texto = filtro.Busca.Trim().TrimStart('#');
            var termo = $"%{texto.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
            var numero = int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1;
            consulta = consulta.Where(s => s.Numero == numero || EF.Functions.ILike(s.Titulo, termo));
        }

        return filtro.SituacaoSla switch
        {
            null => consulta,
            // Não pausada e em aberto: passou do prazo de resolução, ou do de primeira resposta sem resposta ainda.
            // Resolvida/fechada: resolvida depois do prazo.
            SituacaoSla.Violado => consulta.Where(s => s.Sla != null && (
                (EmAberto.Contains(s.Status) && s.Sla.PausadoDesde == null
                    && (agora > s.Sla.PrazoResolucao || (s.PrimeiraRespostaEm == null && agora > s.Sla.PrazoPrimeiraResposta)))
                || ((s.Status == StatusSolicitacao.Resolvida || s.Status == StatusSolicitacao.Fechada) && s.ResolvidaEm > s.Sla.PrazoResolucao))),
            // Em aberto, não pausada, já passou de 75% do prazo de resolução e ainda não violou.
            SituacaoSla.EmRisco => consulta.Where(s => s.Sla != null
                && EmAberto.Contains(s.Status) && s.Sla.PausadoDesde == null
                && s.Sla.LimiteRisco <= agora && agora <= s.Sla.PrazoResolucao
                && !(s.PrimeiraRespostaEm == null && agora > s.Sla.PrazoPrimeiraResposta)),
            _ => throw ErroDeCampo.Criar("situacaoSla", "O filtro de SLA aceita EM_RISCO ou VIOLADO."),
        };
    }

    private static IQueryable<Solicitacao> Ordenar(IQueryable<Solicitacao> consulta, string? ordem) => ordem?.Trim() switch
    {
        null or "" or "-abertaEm" => consulta.OrderByDescending(s => s.AbertaEm).ThenByDescending(s => s.Numero),
        "abertaEm" => consulta.OrderBy(s => s.AbertaEm).ThenBy(s => s.Numero),
        "numero" => consulta.OrderBy(s => s.Numero),
        "-numero" => consulta.OrderByDescending(s => s.Numero),
        "prioridade" => consulta.OrderBy(s => s.Prioridade).ThenByDescending(s => s.AbertaEm),
        "-prioridade" => consulta.OrderByDescending(s => s.Prioridade).ThenByDescending(s => s.AbertaEm),
        // Sem SLA (prazo nulo) vai para o fim no crescente (NULLS LAST do Postgres).
        "prazoResolucao" => consulta.OrderBy(s => s.Sla!.PrazoResolucao).ThenBy(s => s.Numero),
        "-prazoResolucao" => consulta.OrderByDescending(s => s.Sla!.PrazoResolucao).ThenBy(s => s.Numero),
        _ => throw ErroDeCampo.Criar("ordem", "A ordem aceita abertaEm, numero, prioridade ou prazoResolucao (com - para decrescente)."),
    };

    /// <summary>Início do dia civil no fuso do tenant, em UTC.</summary>
    private static DateTimeOffset Inicio(DateOnly dia, CalendarioUtil calendario)
    {
        var local = dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, calendario.Fuso.GetUtcOffset(local)).ToUniversalTime();
    }

    private static Referencia? Pessoa(Guid? id, IReadOnlyDictionary<Guid, string> nomes) =>
        id is { } valor ? new Referencia(valor, nomes.GetValueOrDefault(valor, string.Empty)) : null;

    private async Task<Dictionary<Guid, string>> NomesDeClientesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var lista = ids.Distinct().ToList();
        return await _db.Clientes.AsNoTracking()
            .Where(c => lista.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nome, cancellationToken);
    }

    /// <summary>Nomes de pessoas (tabela global do Identity): só nomes, para quem já aparece num registro visível.</summary>
    private async Task<Dictionary<Guid, string>> NomesDeUsuariosAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0)
            return [];
        return await _db.Users.AsNoTracking()
            .Where(u => lista.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nome, cancellationToken);
    }

    private sealed class Soma
    {
        public int Total { get; set; }
    }
}
