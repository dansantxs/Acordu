using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Application.Tarefas;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Projetos;

/// <summary>Casos de uso de projetos (contrato §8.2). Contrato, se houver, precisa ser do mesmo cliente (modelo §4.7).</summary>
public sealed class ComandosProjeto
{
    private readonly ITenantContext _contexto;
    private readonly IRepositorio<Projeto> _projetos;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Contrato> _contratos;
    private readonly IRepositorio<Tarefa> _tarefas;
    private readonly TarefasAbertas _abertas;
    private readonly RelogioDoTenant _relogio;
    private readonly IUnitOfWork _unidade;
    private readonly IControleVersao _versoes;

    public ComandosProjeto(
        ITenantContext contexto,
        IRepositorio<Projeto> projetos,
        IRepositorio<Cliente> clientes,
        IRepositorio<Contrato> contratos,
        IRepositorio<Tarefa> tarefas,
        TarefasAbertas abertas,
        RelogioDoTenant relogio,
        IUnitOfWork unidade,
        IControleVersao versoes)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(projetos);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(contratos);
        ArgumentNullException.ThrowIfNull(tarefas);
        ArgumentNullException.ThrowIfNull(abertas);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(versoes);
        _contexto = contexto;
        _projetos = projetos;
        _clientes = clientes;
        _contratos = contratos;
        _tarefas = tarefas;
        _abertas = abertas;
        _relogio = relogio;
        _unidade = unidade;
        _versoes = versoes;
    }

    public async Task<Guid> CriarAsync(NovoProjetoRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.ProjetoGerenciar);
        var cliente = await _clientes.ObterPorIdAsync(dados.ClienteId, cancellationToken)
            ?? throw ErroDeCampo.Criar("clienteId", "Cliente não encontrado.");
        if (!cliente.Ativo)
            throw ErroDeCampo.Criar("clienteId", "Cliente inativo não recebe novos projetos.");
        await ValidarContratoAsync(dados.ContratoId, cliente.Id, cancellationToken);

        var projeto = Projeto.Criar(
            Guid.CreateVersion7(), _contexto.TenantId!.Value, cliente.Id, dados.Nome, dados.ContratoId, dados.Prazo,
            Estimativa(dados.EstimativaMinutos), dados.Inicio);
        _projetos.Adicionar(projeto);
        await LevarTarefasAsync(projeto, dados.TarefaIds, cancellationToken);
        await _unidade.SalvarAsync(cancellationToken);
        return projeto.Id;
    }

    public async Task EditarAsync(Guid id, EdicaoProjetoRequest dados, string? versao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var projeto = await CarregarAsync(id, versao, cancellationToken);
        projeto.Editar(dados.Nome, dados.Prazo, Estimativa(dados.EstimativaMinutos), dados.Inicio);
        if (dados.ContratoId != projeto.ContratoId)
        {
            await ValidarContratoAsync(dados.ContratoId, projeto.ClienteId, cancellationToken);
            projeto.AlterarContrato(dados.ContratoId);
        }

        await _unidade.SalvarAsync(cancellationToken);
    }

    /// <param name="tarefas">Concluir as abertas junto (F11-27); nulo ou MANTER deixa como estão.</param>
    public async Task ConcluirAsync(
        Guid id, string? versao, TarefasAbertasRequest? tarefas = null, CancellationToken cancellationToken = default)
    {
        var projeto = await CarregarAsync(id, versao, cancellationToken);
        await _abertas.AplicarNoProjetoAsync(
            id, tarefas, AcaoTarefasAbertas.Concluir, _relogio.Agora, cancellationToken);
        projeto.Concluir();
        await _unidade.SalvarAsync(cancellationToken);
    }

    /// <param name="tarefas">Cancelar as abertas junto (F11-27); nulo ou MANTER deixa como estão.</param>
    public async Task CancelarAsync(
        Guid id, string? versao, TarefasAbertasRequest? tarefas = null, CancellationToken cancellationToken = default)
    {
        var projeto = await CarregarAsync(id, versao, cancellationToken);
        await _abertas.AplicarNoProjetoAsync(
            id, tarefas, AcaoTarefasAbertas.Cancelar, _relogio.Agora, cancellationToken);
        projeto.Cancelar();
        await _unidade.SalvarAsync(cancellationToken);
    }

    /// <summary>F11-22: as tarefas escolhidas passam para o projeto novo, na mesma transação (regras do F11d).</summary>
    private async Task LevarTarefasAsync(Projeto projeto, IReadOnlyList<Guid>? ids, CancellationToken cancellationToken)
    {
        if (ids is not { Count: > 0 })
            return;
        _contexto.Exigir(Permissoes.TarefaEditar);
        foreach (var tarefaId in ids.Distinct())
        {
            var tarefa = await _tarefas.ObterPorIdAsync(tarefaId, cancellationToken)
                ?? throw ErroDeCampo.Criar("tarefaIds", "Tarefa não encontrada.");
            tarefa.MoverParaProjeto(projeto);
        }
    }

    private async Task<Projeto> CarregarAsync(Guid id, string? versao, CancellationToken cancellationToken)
    {
        _contexto.Exigir(Permissoes.ProjetoGerenciar);
        var projeto = await _projetos.ObterPorIdAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Projeto não encontrado.");
        _versoes.Esperar(projeto, versao);
        return projeto;
    }

    private async Task ValidarContratoAsync(Guid? contratoId, Guid clienteId, CancellationToken cancellationToken)
    {
        if (contratoId is not { } id)
            return;
        var contrato = await _contratos.ObterPorIdAsync(id, cancellationToken)
            ?? throw ErroDeCampo.Criar("contratoId", "Contrato não encontrado.");
        if (contrato.ClienteId != clienteId)
            throw new ContratoIncoerenteException("O contrato é de outro cliente.");
    }

    private static Duracao? Estimativa(int? minutos) => minutos switch
    {
        null => null,
        <= 0 => throw ErroDeCampo.Criar("estimativaMinutos", "A estimativa precisa ser maior que zero."),
        _ => Duracao.FromMinutos(minutos.Value),
    };
}
