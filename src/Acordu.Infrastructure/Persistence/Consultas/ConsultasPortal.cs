using Acordu.Application.Abstractions;
using Acordu.Application.Anexos;
using Acordu.Application.Common;
using Acordu.Application.Contratos;
using Acordu.Application.Portal;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>
/// Leituras do portal (contrato §12). Os filtros globais já restringem ao cliente do contato; aqui entra o escopo
/// (PROPRIAS: só solicitações em que ele é o contato) e a regra de nunca devolver o que é interno: interações
/// EXTERNAS, tarefas e apontamentos marcados como visíveis ao cliente, e só o nome do responsável.
/// </summary>
public sealed class ConsultasPortal : IConsultasPortal
{
    private readonly AcorduDbContext _db;
    private readonly ConsultaSql _sql;
    private readonly ICalendarios _calendarios;
    private readonly IConsultasContrato _contratos;
    private readonly TimeProvider _relogio;

    public ConsultasPortal(
        AcorduDbContext db, ConsultaSql sql, ICalendarios calendarios, IConsultasContrato contratos, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(calendarios);
        ArgumentNullException.ThrowIfNull(contratos);
        ArgumentNullException.ThrowIfNull(relogio);
        _db = db;
        _sql = sql;
        _calendarios = calendarios;
        _contratos = contratos;
        _relogio = relogio;
    }

    public async Task<PortalEuDto?> EuAsync(ContatoDoPortal contato, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contato);
        var cliente = await _db.Clientes.AsNoTracking().SingleOrDefaultAsync(c => c.Id == contato.ClienteId, cancellationToken);
        var pessoa = cliente?.Contatos.FirstOrDefault(c => c.Id == contato.ContatoId);
        var tenant = await _db.Tenants.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (cliente is null || pessoa is null || tenant is null)
            return null;

        return new PortalEuDto(
            new ContatoPortalDto(pessoa.Id, pessoa.Nome, pessoa.Email),
            new ClientePortalDto(cliente.Id, cliente.Nome),
            new PrestadorPortalDto(tenant.NomePortal ?? tenant.Nome),
            contato.Escopo,
            contato.VeContratos,
            contato.SolicitacaoPermitida,
            !tenant.Situacao.SomenteLeitura());
    }

    public async Task<ResultadoPaginado<SolicitacaoPortalResumoDto>> ListarSolicitacoesAsync(
        ContatoDoPortal contato,
        IReadOnlyCollection<StatusExterno>? status,
        bool podeAprovar,
        Paginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var consulta = Visiveis(contato);
        if (status is { Count: > 0 })
        {
            var internos = Enum.GetValues<StatusSolicitacao>().Where(s => status.Contains(StatusNoPortal.Para(s, podeAprovar))).ToList();
            consulta = consulta.Where(s => internos.Contains(s.Status));
        }

        var total = await consulta.CountAsync(cancellationToken);
        var itens = await consulta
            .OrderByDescending(s => s.AbertaEm).ThenByDescending(s => s.Id)
            .Skip(paginacao.Pular).Take(paginacao.TamanhoPagina)
            .Select(s => new { s.Id, s.Numero, s.Titulo, s.Status, s.AbertaEm })
            .ToListAsync(cancellationToken);
        return new ResultadoPaginado<SolicitacaoPortalResumoDto>(
            [.. itens.Select(s => new SolicitacaoPortalResumoDto(
                s.Id, s.Numero, s.Titulo, StatusNoPortal.Para(s.Status, podeAprovar), s.AbertaEm, s.Status == StatusSolicitacao.Resolvida))],
            paginacao.Pagina,
            paginacao.TamanhoPagina,
            total);
    }

    public async Task<SolicitacaoPortalDto?> DetalharSolicitacaoAsync(
        ContatoDoPortal contato, Guid id, bool podeAprovar, CancellationToken cancellationToken = default)
    {
        var s = await Visiveis(contato).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (s is null)
            return null;

        var agora = _relogio.GetUtcNow();
        var calendario = await _calendarios.DoTenantAsync(cancellationToken);
        var nomes = await Nomes.DeUsuariosAsync(_db, s.ResponsavelId is { } r ? [r] : [], cancellationToken);
        var tarefas = await _db.Tarefas.AsNoTracking()
            .Where(t => t.SolicitacaoId == id && t.VisivelCliente)
            .OrderBy(t => t.Id)
            .Select(t => new TarefaPortalDto(t.Titulo, t.Status))
            .ToListAsync(cancellationToken);
        var tempoVisivel = (await _sql.ListarAsync<Soma>(e => $"""
            SELECT coalesce(sum(a.duracao_minutos), 0)::int AS total
            FROM apontamentos a
            JOIN tarefas t ON t.id = a.tarefa_id AND t.tenant_id = a.tenant_id
            WHERE a.tenant_id = {e.TenantId} AND t.solicitacao_id = {id} AND a.visivel_cliente
              AND (CAST({e.ClienteId} AS uuid) IS NULL OR a.cliente_id = {e.ClienteId})
            """, cancellationToken)).Single().Total;

        return new SolicitacaoPortalDto(
            s.Id,
            s.Numero,
            s.Titulo,
            s.Descricao,
            StatusNoPortal.Para(s.Status, podeAprovar),
            s.AbertaEm,
            s.ResponsavelId is { } responsavel ? new ResponsavelPortalDto(nomes.GetValueOrDefault(responsavel, string.Empty)) : null,
            s.Sla is { } sla ? new SlaPortalDto(sla.PrazoResolucao, s.SituacaoSla(agora, calendario), sla.Pausado) : null,
            tarefas,
            tempoVisivel,
            s.Status == StatusSolicitacao.Resolvida,
            s.EstaFinalizada);
    }

    public async Task<IReadOnlyList<InteracaoPortalDto>?> InteracoesAsync(
        ContatoDoPortal contato, Guid solicitacaoId, CancellationToken cancellationToken = default)
    {
        if (!await Visiveis(contato).AnyAsync(s => s.Id == solicitacaoId, cancellationToken))
            return null;

        var interacoes = await _db.Interacoes.AsNoTracking()
            .Where(i => i.SolicitacaoId == solicitacaoId && i.Visibilidade == Visibilidade.Externa)
            .OrderBy(i => i.RegistradaEm).ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);
        var nomes = await Nomes.DeAutoresAsync(_db, contato.ClienteId, interacoes.Select(i => i.AutorId), cancellationToken);
        return [.. interacoes.Select(i => new InteracaoPortalDto(
            i.Id,
            i.Texto,
            nomes.GetValueOrDefault(i.AutorId, string.Empty),
            !i.AutorEhCliente,
            i.RegistradaEm,
            [.. i.Anexos.OrderBy(a => a.NomeArquivo, StringComparer.Ordinal).Select(a => new AnexoPortalDto(a.Id, a.NomeArquivo, a.TipoConteudo, a.TamanhoBytes))]))];
    }

    public async Task<AnexoArmazenado?> AnexoAsync(ContatoDoPortal contato, Guid anexoId, CancellationToken cancellationToken = default)
    {
        var visiveis = Visiveis(contato).Select(s => s.Id);
        return await _db.Interacoes.AsNoTracking()
            .Where(i => i.Visibilidade == Visibilidade.Externa && visiveis.Contains(i.SolicitacaoId))
            .SelectMany(i => i.Anexos)
            .Where(a => a.Id == anexoId)
            .Select(a => new AnexoArmazenado(a.ChaveArmazenamento, a.NomeArquivo, a.TipoConteudo))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContratoPortalDto>> ContratosAsync(DateOnly hoje, CancellationToken cancellationToken = default)
    {
        var contratos = await _db.Contratos.AsNoTracking()
            .Where(c => c.Status == StatusContrato.Ativo
                && c.Vigencia.Inicio <= hoje && (c.Vigencia.Fim == null || c.Vigencia.Fim >= hoje))
            .OrderBy(c => c.Vigencia.Inicio).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);
        return [.. contratos.Select(c => new ContratoPortalDto(c.Id, c.Tipo, c.Vigencia.Inicio, c.Vigencia.Fim, c.Franquia?.Minutos))];
    }

    public async Task<SaldoContratoPortal?> SaldoAsync(
        Guid contratoId, MesReferencia periodo, DateOnly hoje, CancellationToken cancellationToken = default)
    {
        var consumo = await _contratos.ConsumoAsync(contratoId, periodo, hoje, cancellationToken);
        return consumo is null
            ? null
            : new SaldoContratoPortal(consumo.Tipo, consumo.FranquiaMinutos, consumo.SaldoMinutos, consumo.Percentual);
    }

    /// <summary>
    /// Solicitações do cliente do contexto (filtro global) que o contato pode ver: na sessão de link, só a dela; com
    /// conta, pelo escopo (mesma regra de <see cref="ContatoDoPortal.Ve"/>).
    /// </summary>
    private IQueryable<Solicitacao> Visiveis(ContatoDoPortal contato)
    {
        ArgumentNullException.ThrowIfNull(contato);
        var consulta = _db.Solicitacoes.AsNoTracking().Where(s => s.ClienteId == contato.ClienteId);
        if (contato.SolicitacaoPermitida is { } permitida)
            return consulta.Where(s => s.Id == permitida);
        return contato.Escopo == EscopoPortal.Proprias ? consulta.Where(s => s.ContatoId == contato.ContatoId) : consulta;
    }

    private sealed class Soma
    {
        public int Total { get; set; }
    }
}
