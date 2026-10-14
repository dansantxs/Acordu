using Acordu.Application.Agenda;
using Acordu.Application.Common;
using Acordu.Application.Contratos;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>
/// Linhas da agenda pelos filtros globais (read side §6a). Durações e o modelo da recorrência são colunas convertidas:
/// saem em memória. O consumo de contrato reaproveita as fórmulas do §3, um contrato por vez, como o dashboard.
/// </summary>
public sealed class ConsultasAgenda : IConsultasAgenda
{
    private static readonly StatusSolicitacao[] EmAberto =
    [
        StatusSolicitacao.Aberta, StatusSolicitacao.EmAndamento, StatusSolicitacao.AguardandoCliente, StatusSolicitacao.AguardandoTerceiro,
    ];

    private readonly AcorduDbContext _db;
    private readonly IConsultasContrato _contratos;

    public ConsultasAgenda(AcorduDbContext db, IConsultasContrato contratos)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contratos);
        _db = db;
        _contratos = contratos;
    }

    public async Task<IReadOnlyList<Referencia>> EquipeAsync(CancellationToken cancellationToken = default) =>
        await (from v in _db.Vinculos.AsNoTracking()
               join u in _db.Users on v.UsuarioId equals u.Id
               where v.ClienteId == null
               orderby u.Nome, u.Id
               select new Referencia(u.Id, u.Nome))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SolicitacaoAgenda>> SolicitacoesAsync(
        DateTimeOffset antes, FiltroAgenda filtro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var linhas = await (from s in _db.Solicitacoes.AsNoTracking()
                            join c in _db.Clientes on s.ClienteId equals c.Id
                            where s.Sla != null && EmAberto.Contains(s.Status) && s.Sla.PrazoResolucao < antes
                                && (filtro.ClienteId == null || s.ClienteId == filtro.ClienteId)
                                && (filtro.ResponsavelId == null || s.ResponsavelId == filtro.ResponsavelId)
                            select new
                            {
                                s.Id,
                                s.Numero,
                                s.Titulo,
                                s.ClienteId,
                                ClienteNome = c.Nome,
                                s.ResponsavelId,
                                s.Sla!.PrazoResolucao,
                                s.Sla.PrazoPrimeiraResposta,
                                s.Sla.LimiteRisco,
                                Pausada = s.Sla.PausadoDesde != null,
                                Respondida = s.PrimeiraRespostaEm != null,
                            })
            .ToListAsync(cancellationToken);
        var pessoas = await Nomes.DeUsuariosAsync(_db, linhas.Where(l => l.ResponsavelId is not null).Select(l => l.ResponsavelId!.Value), cancellationToken);
        return [.. linhas.Select(l => new SolicitacaoAgenda(
            l.Id, l.Numero, l.Titulo, new Referencia(l.ClienteId, l.ClienteNome), Nomes.Pessoa(l.ResponsavelId, pessoas),
            l.PrazoResolucao, l.PrazoPrimeiraResposta, l.LimiteRisco, l.Pausada, l.Respondida))];
    }

    public async Task<IReadOnlyList<TarefaAgenda>> TarefasAsync(
        DateOnly de, DateOnly ate, bool atrasadas, FiltroAgenda filtro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var linhas = await (from t in _db.Tarefas.AsNoTracking()
                            join c in _db.Clientes on t.ClienteId equals (Guid?)c.Id into clientes
                            from c in clientes.DefaultIfEmpty()
                            let dia = t.PlanejadaPara ?? t.Prazo
                            where t.Status != StatusTarefa.Cancelada && dia != null
                                && ((dia >= de && dia <= ate)
                                    || (atrasadas && dia < de && (t.Status == StatusTarefa.AFazer || t.Status == StatusTarefa.Fazendo)))
                                && (filtro.ClienteId == null || t.ClienteId == filtro.ClienteId)
                                && (filtro.ResponsavelId == null || t.ResponsavelId == filtro.ResponsavelId)
                            select new { Tarefa = t, ClienteNome = c == null ? null : c.Nome })
            .ToListAsync(cancellationToken);
        var pessoas = await Nomes.DeUsuariosAsync(
            _db, linhas.Where(l => l.Tarefa.ResponsavelId is not null).Select(l => l.Tarefa.ResponsavelId!.Value), cancellationToken);
        return [.. linhas.Select(l =>
        {
            var t = l.Tarefa;
            var cliente = t.ClienteId is { } id && l.ClienteNome is { } nome ? new Referencia(id, nome) : null;
            return new TarefaAgenda(
                t.Id, t.Titulo, t.Status, cliente, Nomes.Pessoa(t.ResponsavelId, pessoas), t.PlanejadaPara, t.Prazo,
                t.Estimativa?.Minutos, t.SolicitacaoId, t.ProjetoId);
        })];
    }

    public async Task<IReadOnlyList<ProjetoAgenda>> ProjetosAsync(
        DateOnly de, DateOnly ate, bool atrasados, Guid? clienteId, CancellationToken cancellationToken = default) =>
        await (from p in _db.Projetos.AsNoTracking()
               join c in _db.Clientes on p.ClienteId equals c.Id
               let comeca = p.Inicio ?? p.Prazo
               let termina = p.Prazo ?? p.Inicio
               where p.Status == StatusProjeto.EmAndamento && comeca != null
                   && comeca <= ate && (termina >= de || atrasados)
                   && (clienteId == null || p.ClienteId == clienteId)
               select new ProjetoAgenda(p.Id, p.Nome, new Referencia(p.ClienteId, c.Nome), p.Inicio, p.Prazo))
            .ToListAsync(cancellationToken);

    /// <summary>O modelo é jsonb com conversor: título, responsável e estimativa saem em memória.</summary>
    public async Task<IReadOnlyList<RecorrenciaAgenda>> RecorrenciasAsync(
        DateOnly ate, Guid? clienteId, CancellationToken cancellationToken = default)
    {
        var linhas = await (from r in _db.Recorrencias.AsNoTracking()
                            join c in _db.Clientes on r.ClienteId equals c.Id
                            where r.Status == StatusRecorrencia.Ativa && r.ProximaData != null && r.ProximaData <= ate
                                && (clienteId == null || r.ClienteId == clienteId)
                            select new { r, ClienteNome = c.Nome })
            .ToListAsync(cancellationToken);
        var pessoas = await Nomes.DeUsuariosAsync(
            _db, linhas.Where(l => l.r.Modelo.ResponsavelId is not null).Select(l => l.r.Modelo.ResponsavelId!.Value), cancellationToken);
        return [.. linhas.Select(l => new RecorrenciaAgenda(
            l.r.Id, l.r.Modelo.Titulo, new Referencia(l.r.ClienteId, l.ClienteNome), Nomes.Pessoa(l.r.Modelo.ResponsavelId, pessoas),
            l.r.Modelo.Estimativa?.Minutos, l.r.Regra, l.r.ProximaData!.Value))];
    }

    public async Task<IReadOnlyList<ContratoAgenda>> ContratosAsync(
        DateOnly de, DateOnly ate, DateOnly hoje, Guid? clienteId, CancellationToken cancellationToken = default)
    {
        var ativos = await (from c in _db.Contratos.AsNoTracking()
                            join cl in _db.Clientes on c.ClienteId equals cl.Id
                            where c.Status == StatusContrato.Ativo
                                && c.Vigencia.Inicio <= ate && (c.Vigencia.Fim == null || c.Vigencia.Fim >= de)
                                && (clienteId == null || c.ClienteId == clienteId)
                            select new { c.Id, c.ClienteId, Cliente = cl.Nome, c.Tipo, c.Vigencia.Inicio, c.Vigencia.Fim })
            .ToListAsync(cancellationToken);

        var periodo = MesReferencia.De(hoje);
        var linhas = new List<ContratoAgenda>(ativos.Count);
        foreach (var ativo in ativos)
        {
            int? percentual = null;
            if (ativo.Inicio <= hoje && (ativo.Fim is null || ativo.Fim >= hoje)
                && await _contratos.ConsumoAsync(ativo.Id, periodo, hoje, cancellationToken) is { } consumo)
                percentual = consumo.Percentual;
            linhas.Add(new ContratoAgenda(ativo.Id, new Referencia(ativo.ClienteId, ativo.Cliente), ativo.Tipo, ativo.Fim, percentual));
        }

        return linhas;
    }
}
