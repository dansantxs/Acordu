using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Entities;

namespace Acordu.Application.Recorrencias;

/// <summary>
/// Agendador das tarefas recorrentes (RF-REC-03), no tenant do contexto, chamado pelo job <c>tick</c>. Cada recorrência
/// devida é gravada com as tarefas dela num commit próprio: a próxima data avança junto, e o índice único
/// (RecorrenciaId, DataReferencia) barra duplicata numa corrida. Uma falha interrompe o tenant; o que já foi gravado
/// fica, e o próximo tick continua dali.
/// </summary>
public sealed class GeracaoRecorrencias
{
    private readonly IConsultasRecorrencia _consultas;
    private readonly IRepositorio<RecorrenciaTarefa> _recorrencias;
    private readonly IRepositorio<Tarefa> _tarefas;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IMembrosDaEquipe _equipe;
    private readonly RelogioDoTenant _relogio;
    private readonly IUnitOfWork _unidade;
    private readonly ITenantAtual _tenant;

    public GeracaoRecorrencias(
        IConsultasRecorrencia consultas,
        IRepositorio<RecorrenciaTarefa> recorrencias,
        IRepositorio<Tarefa> tarefas,
        IRepositorio<Cliente> clientes,
        IMembrosDaEquipe equipe,
        RelogioDoTenant relogio,
        IUnitOfWork unidade,
        ITenantAtual tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        _tenant = tenant;
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(recorrencias);
        ArgumentNullException.ThrowIfNull(tarefas);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(equipe);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        _consultas = consultas;
        _recorrencias = recorrencias;
        _tarefas = tarefas;
        _clientes = clientes;
        _equipe = equipe;
        _relogio = relogio;
        _unidade = unidade;
    }

    /// <summary>Gera as tarefas devidas e devolve quantas criou.</summary>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var hoje = await _relogio.HojeAsync(cancellationToken);
        var clientesAtivos = new Dictionary<Guid, bool>();
        var geradas = 0;
        // Volta do só leitura (F14-24): as datas anteriores ao dia da volta passam sem gerar, como as de cliente inativo.
        DateOnly? gravavelDesde = (await _tenant.ObterAsync(cancellationToken)).GravavelDesde is { } volta
            ? await _relogio.DiaAsync(volta, cancellationToken)
            : null;

        foreach (var id in await _consultas.DevidasAsync(hoje, cancellationToken))
        {
            var recorrencia = await _recorrencias.ObterPorIdAsync(id, cancellationToken);
            if (recorrencia is null)
                continue;
            var datas = recorrencia.GerarDevidas(hoje);
            if (datas.Count == 0)
                continue;
            if (gravavelDesde is { } desde)
                datas = [.. datas.Where(d => d >= desde)];

            // Cliente inativo: as datas passam sem gerar e sem acumular (reativar não traz tarefas atrasadas).
            if (await ClienteAtivoAsync(recorrencia.ClienteId, clientesAtivos, cancellationToken))
            {
                // Responsável que saiu da equipe: a tarefa nasce sem responsável, visível para alguém assumir.
                var semResponsavel = recorrencia.Modelo.ResponsavelId is { } responsavel
                    && !await _equipe.EhDaEquipeAsync(responsavel, cancellationToken);
                foreach (var data in datas)
                {
                    var tarefa = Tarefa.CriarDeRecorrencia(Guid.CreateVersion7(), recorrencia, data);
                    if (semResponsavel)
                        tarefa.Atribuir(null);
                    _tarefas.Adicionar(tarefa);
                    geradas++;
                }
            }

            await _unidade.SalvarAsync(cancellationToken);
        }

        return geradas;
    }

    private async Task<bool> ClienteAtivoAsync(Guid clienteId, Dictionary<Guid, bool> cache, CancellationToken cancellationToken)
    {
        if (!cache.TryGetValue(clienteId, out var ativo))
        {
            ativo = (await _clientes.ObterPorIdAsync(clienteId, cancellationToken))?.Ativo ?? false;
            cache[clienteId] = ativo;
        }

        return ativo;
    }
}
