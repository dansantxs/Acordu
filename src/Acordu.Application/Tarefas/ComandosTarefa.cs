using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Tarefas;

/// <summary>Casos de uso de tarefas e checklist (contrato §8.1). O cliente é herdado da solicitação/projeto (modelo §4.6).</summary>
public sealed class ComandosTarefa
{
    private readonly ITenantContext _contexto;
    private readonly IRepositorio<Tarefa> _tarefas;
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly IRepositorio<Projeto> _projetos;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IMembrosDaEquipe _equipe;
    private readonly IUnitOfWork _unidade;
    private readonly IControleVersao _versoes;
    private readonly TimeProvider _relogio;

    public ComandosTarefa(
        ITenantContext contexto,
        IRepositorio<Tarefa> tarefas,
        IRepositorio<Solicitacao> solicitacoes,
        IRepositorio<Projeto> projetos,
        IRepositorio<Cliente> clientes,
        IMembrosDaEquipe equipe,
        IUnitOfWork unidade,
        IControleVersao versoes,
        TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(tarefas);
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(projetos);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(equipe);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(versoes);
        ArgumentNullException.ThrowIfNull(relogio);
        _contexto = contexto;
        _tarefas = tarefas;
        _solicitacoes = solicitacoes;
        _projetos = projetos;
        _clientes = clientes;
        _equipe = equipe;
        _unidade = unidade;
        _versoes = versoes;
        _relogio = relogio;
    }

    public async Task<Guid> CriarAsync(NovaTarefaRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.TarefaCriar);
        var clienteId = await ClienteDaTarefaAsync(dados, cancellationToken);
        ValidarVisibilidade(dados.VisivelCliente, clienteId);
        await ValidarResponsavelAsync(dados.ResponsavelId, cancellationToken);

        var tarefa = Tarefa.Criar(
            Guid.CreateVersion7(),
            _contexto.TenantId!.Value,
            dados.Titulo,
            dados.Descricao,
            clienteId,
            dados.SolicitacaoId,
            dados.ProjetoId,
            dados.ResponsavelId,
            dados.Prazo,
            Estimativa(dados.EstimativaMinutos),
            dados.VisivelCliente,
            dados.PlanejadaPara);
        _tarefas.Adicionar(tarefa);
        await _unidade.SalvarAsync(cancellationToken);
        return tarefa.Id;
    }

    public async Task EditarAsync(Guid id, EdicaoTarefaRequest dados, string? versao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var tarefa = await CarregarAsync(id, versao, cancellationToken);
        ValidarVisibilidade(dados.VisivelCliente, tarefa.ClienteId);
        if (dados.ResponsavelId != tarefa.ResponsavelId)
            await ValidarResponsavelAsync(dados.ResponsavelId, cancellationToken);

        tarefa.Editar(dados.Titulo, dados.Descricao);
        tarefa.Atribuir(dados.ResponsavelId);
        tarefa.DefinirPrazo(dados.Prazo);
        tarefa.Planejar(dados.PlanejadaPara);
        tarefa.DefinirEstimativa(Estimativa(dados.EstimativaMinutos));
        tarefa.TornarVisivelAoCliente(dados.VisivelCliente);
        await _unidade.SalvarAsync(cancellationToken);
    }

    public Task IniciarAsync(Guid id, string? versao, CancellationToken cancellationToken = default) =>
        AlterarAsync(id, versao, t => t.Iniciar(), cancellationToken);

    public Task ConcluirAsync(Guid id, string? versao, CancellationToken cancellationToken = default) =>
        AlterarAsync(id, versao, t => t.Concluir(_relogio.GetUtcNow()), cancellationToken);

    public Task RetornarAsync(Guid id, string? versao, CancellationToken cancellationToken = default) =>
        AlterarAsync(id, versao, t => t.Retornar(), cancellationToken);

    public Task ReabrirAsync(Guid id, string? versao, CancellationToken cancellationToken = default) =>
        AlterarAsync(id, versao, t => t.Reabrir(), cancellationToken);

    public Task CancelarAsync(Guid id, string? versao, CancellationToken cancellationToken = default) =>
        AlterarAsync(id, versao, t => t.Cancelar(), cancellationToken);

    public async Task<Guid> AdicionarItemAsync(Guid id, ItemChecklistRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var tarefa = await CarregarAsync(id, null, cancellationToken);
        var item = tarefa.AdicionarItem(dados.Texto);
        if (dados.Feito is true)
            tarefa.MarcarItem(item.Id, true);
        await _unidade.SalvarAsync(cancellationToken);
        return item.Id;
    }

    public async Task EditarItemAsync(Guid id, Guid itemId, ItemChecklistRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var tarefa = await CarregarComItemAsync(id, itemId, cancellationToken);
        tarefa.EditarItem(itemId, dados.Texto);
        if (dados.Feito is { } feito)
            tarefa.MarcarItem(itemId, feito);
        await _unidade.SalvarAsync(cancellationToken);
    }

    public async Task RemoverItemAsync(Guid id, Guid itemId, CancellationToken cancellationToken = default)
    {
        var tarefa = await CarregarComItemAsync(id, itemId, cancellationToken);
        tarefa.RemoverItem(itemId);
        await _unidade.SalvarAsync(cancellationToken);
    }

    private async Task AlterarAsync(Guid id, string? versao, Action<Tarefa> alterar, CancellationToken cancellationToken)
    {
        var tarefa = await CarregarAsync(id, versao, cancellationToken);
        alterar(tarefa);
        await _unidade.SalvarAsync(cancellationToken);
    }

    private async Task<Tarefa> CarregarAsync(Guid id, string? versao, CancellationToken cancellationToken)
    {
        _contexto.Exigir(Permissoes.TarefaEditar);
        var tarefa = await _tarefas.ObterPorIdAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Tarefa não encontrada.");
        _versoes.Esperar(tarefa, versao);
        return tarefa;
    }

    private async Task<Tarefa> CarregarComItemAsync(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        var tarefa = await CarregarAsync(id, null, cancellationToken);
        if (!tarefa.Checklist.Any(i => i.Id == itemId))
            throw new NaoEncontradoException("Item de checklist não encontrado.");
        return tarefa;
    }

    /// <summary>Cliente herdado da solicitação e/ou do projeto (que precisam concordar) ou, avulsa, o informado.</summary>
    private async Task<Guid?> ClienteDaTarefaAsync(NovaTarefaRequest dados, CancellationToken cancellationToken)
    {
        Guid? herdado = null;
        if (dados.SolicitacaoId is { } solicitacaoId)
        {
            var solicitacao = await _solicitacoes.ObterPorIdAsync(solicitacaoId, cancellationToken)
                ?? throw ErroDeCampo.Criar("solicitacaoId", "Solicitação não encontrada.");
            herdado = solicitacao.ClienteId;
        }

        if (dados.ProjetoId is { } projetoId)
        {
            var projeto = await _projetos.ObterPorIdAsync(projetoId, cancellationToken)
                ?? throw ErroDeCampo.Criar("projetoId", "Projeto não encontrado.");
            if (projeto.Status != StatusProjeto.EmAndamento)
                throw ErroDeCampo.Criar("projetoId", "Projeto concluído ou cancelado não recebe tarefas.");
            if (herdado is not null && herdado != projeto.ClienteId)
                throw ErroDeCampo.Criar("projetoId", "Solicitação e projeto são de clientes diferentes.");
            herdado = projeto.ClienteId;
        }

        if (herdado is not null)
        {
            if (dados.ClienteId is { } informado && informado != herdado)
                throw ErroDeCampo.Criar("clienteId", "A tarefa herda o cliente da solicitação ou do projeto.");
            return herdado;
        }

        if (dados.ClienteId is { } clienteId && await _clientes.ObterPorIdAsync(clienteId, cancellationToken) is null)
            throw ErroDeCampo.Criar("clienteId", "Cliente não encontrado.");
        return dados.ClienteId;
    }

    private async Task ValidarResponsavelAsync(Guid? responsavelId, CancellationToken cancellationToken)
    {
        if (responsavelId is { } id && !await _equipe.EhDaEquipeAsync(id, cancellationToken))
            throw ErroDeCampo.Criar("responsavelId", "O responsável precisa ser da equipe.");
    }

    private static void ValidarVisibilidade(bool visivel, Guid? clienteId)
    {
        if (visivel && clienteId is null)
            throw ErroDeCampo.Criar("visivelCliente", "Só tarefa ligada a um cliente pode ficar visível no portal.");
    }

    private static Duracao? Estimativa(int? minutos) => minutos switch
    {
        null => null,
        <= 0 => throw ErroDeCampo.Criar("estimativaMinutos", "A estimativa precisa ser maior que zero."),
        _ => Duracao.FromMinutos(minutos.Value),
    };
}
