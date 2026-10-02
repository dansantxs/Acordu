using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Unidade de trabalho (modelo de domínio §4.6): avulsa, de uma solicitação e/ou de um projeto.
/// O <see cref="ClienteId"/> é herdado da solicitação/projeto pela Application e desnormalizado para o filtro do portal.
/// </summary>
public sealed class Tarefa : EntidadeBase
{
    /// <summary>Limite de itens da checklist por tarefa.</summary>
    public const int MaximoItensChecklist = 50;

    private readonly List<ItemChecklist> _checklist = [];

    private Tarefa(Guid id, Guid tenantId, string titulo)
        : base(id, tenantId)
    {
        Titulo = titulo;
        Status = StatusTarefa.AFazer;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Tarefa()
    {
        Titulo = null!;
    }

    /// <summary>Cliente da solicitação, do projeto ou da recorrência; nulo em tarefa interna avulsa.</summary>
    public Guid? ClienteId { get; private set; }

    public string Titulo { get; private set; }

    public string? Descricao { get; private set; }

    public StatusTarefa Status { get; private set; }

    public Guid? ResponsavelId { get; private set; }

    public DateOnly? Prazo { get; private set; }

    public Duracao? Estimativa { get; private set; }

    public Guid? SolicitacaoId { get; private set; }

    public Guid? ProjetoId { get; private set; }

    public Guid? RecorrenciaId { get; private set; }

    public DateOnly? DataReferencia { get; private set; }

    /// <summary>Aparece no portal (RF-TAR-04). Exige <see cref="ClienteId"/>.</summary>
    public bool VisivelCliente { get; private set; }

    public IReadOnlyList<ItemChecklist> Checklist => _checklist;

    public static Tarefa Criar(
        Guid id,
        Guid tenantId,
        string titulo,
        string? descricao,
        Guid? clienteId,
        Guid? solicitacaoId,
        Guid? projetoId,
        Guid? responsavelId,
        DateOnly? prazo,
        Duracao? estimativa,
        bool visivelCliente)
    {
        var cliente = Ids.Opcional(clienteId, nameof(clienteId));
        var solicitacao = Ids.Opcional(solicitacaoId, nameof(solicitacaoId));
        var projeto = Ids.Opcional(projetoId, nameof(projetoId));
        if ((solicitacao is not null || projeto is not null) && cliente is null)
            throw new ArgumentException("Tarefa de solicitação ou projeto herda o cliente dele.", nameof(clienteId));

        var tarefa = new Tarefa(id, tenantId, Texto.Obrigatorio(titulo, LimitesTexto.Titulo, nameof(titulo)))
        {
            Descricao = Texto.Opcional(descricao, LimitesTexto.Descricao, nameof(descricao)),
            ClienteId = cliente,
            SolicitacaoId = solicitacao,
            ProjetoId = projeto,
            ResponsavelId = Ids.Opcional(responsavelId, nameof(responsavelId)),
            Prazo = prazo,
            Estimativa = ValidarEstimativa(estimativa),
        };
        tarefa.TornarVisivelAoCliente(visivelCliente);
        return tarefa;
    }

    /// <summary>
    /// Gera a tarefa de uma ocorrência devida (RF-REC-03), copiando o modelo. Prazo = data de referência. O contrato dos
    /// apontamentos é resolvido pela Application a partir da recorrência.
    /// </summary>
    public static Tarefa CriarDeRecorrencia(Guid id, RecorrenciaTarefa recorrencia, DateOnly dataReferencia)
    {
        ArgumentNullException.ThrowIfNull(recorrencia);
        var modelo = recorrencia.Modelo;
        var tarefa = new Tarefa(id, recorrencia.TenantId, modelo.Titulo)
        {
            Descricao = modelo.Descricao,
            ClienteId = recorrencia.ClienteId,
            ResponsavelId = modelo.ResponsavelId,
            Prazo = dataReferencia,
            Estimativa = modelo.Estimativa,
            RecorrenciaId = recorrencia.Id,
            DataReferencia = dataReferencia,
            VisivelCliente = modelo.VisivelCliente,
        };
        foreach (var item in modelo.Checklist)
            tarefa._checklist.Add(new ItemChecklist(Guid.CreateVersion7(), item));
        return tarefa;
    }

    public void Editar(string titulo, string? descricao)
    {
        GarantirNaoCancelada(nameof(Editar));
        Titulo = Texto.Obrigatorio(titulo, LimitesTexto.Titulo, nameof(titulo));
        Descricao = Texto.Opcional(descricao, LimitesTexto.Descricao, nameof(descricao));
    }

    public void Atribuir(Guid? responsavelId)
    {
        GarantirNaoCancelada(nameof(Atribuir));
        ResponsavelId = Ids.Opcional(responsavelId, nameof(responsavelId));
    }

    public void DefinirPrazo(DateOnly? prazo)
    {
        GarantirNaoCancelada(nameof(DefinirPrazo));
        Prazo = prazo;
    }

    public void DefinirEstimativa(Duracao? estimativa)
    {
        GarantirNaoCancelada(nameof(DefinirEstimativa));
        Estimativa = ValidarEstimativa(estimativa);
    }

    /// <summary>Só tarefa com cliente pode ficar visível no portal.</summary>
    public void TornarVisivelAoCliente(bool visivel)
    {
        GarantirNaoCancelada(nameof(TornarVisivelAoCliente));
        if (visivel && ClienteId is null)
            throw new ArgumentException("Só tarefa ligada a um cliente pode ficar visível no portal.", nameof(visivel));
        VisivelCliente = visivel;
    }

    public void Iniciar()
    {
        Exigir(nameof(Iniciar), StatusTarefa.AFazer);
        Status = StatusTarefa.Fazendo;
    }

    public void Concluir()
    {
        Exigir(nameof(Concluir), StatusTarefa.Fazendo);
        Status = StatusTarefa.Concluida;
        Registrar(new TarefaConcluida(Id, TenantId, ClienteId, SolicitacaoId, ProjetoId));
    }

    public void Reabrir()
    {
        Exigir(nameof(Reabrir), StatusTarefa.Concluida);
        Status = StatusTarefa.Fazendo;
    }

    public void Cancelar()
    {
        Exigir(nameof(Cancelar), StatusTarefa.AFazer, StatusTarefa.Fazendo);
        Status = StatusTarefa.Cancelada;
    }

    /// <summary>Adiciona um item ao fim da checklist. O Id do item é gerado aqui (entidade filha).</summary>
    public ItemChecklist AdicionarItem(string texto)
    {
        GarantirNaoCancelada(nameof(AdicionarItem));
        var valido = ItemChecklist.ValidarTexto(texto);
        if (_checklist.Count >= MaximoItensChecklist)
            throw new ArgumentException($"A checklist aceita no máximo {MaximoItensChecklist} itens.", nameof(texto));

        var item = new ItemChecklist(Guid.CreateVersion7(), valido);
        _checklist.Add(item);
        return item;
    }

    public void EditarItem(Guid itemId, string texto)
    {
        GarantirNaoCancelada(nameof(EditarItem));
        Item(itemId).Editar(ItemChecklist.ValidarTexto(texto));
    }

    public void MarcarItem(Guid itemId, bool feito)
    {
        GarantirNaoCancelada(nameof(MarcarItem));
        Item(itemId).Marcar(feito);
    }

    public void RemoverItem(Guid itemId)
    {
        GarantirNaoCancelada(nameof(RemoverItem));
        _checklist.Remove(Item(itemId));
    }

    private ItemChecklist Item(Guid itemId) =>
        _checklist.Find(i => i.Id == itemId)
        ?? throw new ArgumentException("Item de checklist não encontrado na tarefa.", nameof(itemId));

    private static Duracao? ValidarEstimativa(Duracao? estimativa) =>
        estimativa is { EhZero: true }
            ? throw new ArgumentException("A estimativa, se informada, deve ser maior que zero.", nameof(estimativa))
            : estimativa;

    private void Exigir(string operacao, params StatusTarefa[] permitidos)
    {
        if (!permitidos.Contains(Status))
            throw Transicao(operacao);
    }

    private void GarantirNaoCancelada(string operacao)
    {
        if (Status == StatusTarefa.Cancelada)
            throw Transicao(operacao);
    }

    private TransicaoInvalidaException Transicao(string operacao) =>
        new($"A operação {operacao} não é permitida para tarefa {Status}.");
}
