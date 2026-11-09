using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.Tarefas;

/// <summary>
/// Ação conjunta com a solicitação (F11c) ou o projeto (F11e), convenções §4: conclui ou cancela as tarefas abertas na
/// mesma transação da transição, sem que uma entidade mexa na outra. Exige <c>tarefa.editar</c> além da permissão do
/// dono.
/// </summary>
public sealed class TarefasAbertas
{
    private readonly IConsultasTarefa _consultas;
    private readonly IRepositorio<Tarefa> _tarefas;
    private readonly ITenantContext _contexto;

    public TarefasAbertas(IConsultasTarefa consultas, IRepositorio<Tarefa> tarefas, ITenantContext contexto)
    {
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(tarefas);
        ArgumentNullException.ThrowIfNull(contexto);
        _consultas = consultas;
        _tarefas = tarefas;
        _contexto = contexto;
    }

    /// <param name="permitida">A única ação além de MANTER que a transição aceita.</param>
    public Task AplicarAsync(
        Guid solicitacaoId, TarefasAbertasRequest? pedido, AcaoTarefasAbertas permitida, DateTimeOffset agora,
        CancellationToken cancellationToken) =>
        AplicarAsync(
            () => _consultas.AbertasDaSolicitacaoAsync(solicitacaoId, cancellationToken), pedido, permitida, agora,
            cancellationToken);

    /// <inheritdoc cref="AplicarAsync(Guid, TarefasAbertasRequest?, AcaoTarefasAbertas, DateTimeOffset, CancellationToken)"/>
    public Task AplicarNoProjetoAsync(
        Guid projetoId, TarefasAbertasRequest? pedido, AcaoTarefasAbertas permitida, DateTimeOffset agora,
        CancellationToken cancellationToken) =>
        AplicarAsync(
            () => _consultas.AbertasDoProjetoAsync(projetoId, cancellationToken), pedido, permitida, agora,
            cancellationToken);

    private async Task AplicarAsync(
        Func<Task<IReadOnlyList<Guid>>> buscarAbertas, TarefasAbertasRequest? pedido, AcaoTarefasAbertas permitida,
        DateTimeOffset agora, CancellationToken cancellationToken)
    {
        if (pedido is null || pedido.Acao == AcaoTarefasAbertas.Manter)
            return;
        if (pedido.Acao != permitida)
            throw ErroDeCampo.Criar("tarefas.acao", $"Aqui só vale MANTER ou {permitida.ToString().ToUpperInvariant()}.");
        _contexto.Exigir(Permissoes.TarefaEditar);

        var abertas = await buscarAbertas();
        if (!abertas.ToHashSet().SetEquals(pedido.Ids ?? []))
            throw new ListaDesatualizadaException();
        foreach (var id in abertas)
        {
            var tarefa = await _tarefas.ObterPorIdAsync(id, cancellationToken) ?? throw new ListaDesatualizadaException();
            if (permitida == AcaoTarefasAbertas.Cancelar)
            {
                tarefa.Cancelar();
                continue;
            }
            // Como no quadro: A_FAZER passa por FAZENDO para concluir.
            if (tarefa.Status == StatusTarefa.AFazer)
                tarefa.Iniciar();
            tarefa.Concluir(agora);
        }
    }
}
