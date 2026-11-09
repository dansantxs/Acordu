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

    /// <summary>
    /// Dia em que a tarefa está planejada para ser feita (plano da semana e linha do tempo). Independe do
    /// <see cref="Prazo"/>: arrastar no planejamento muda este dia, nunca o prazo combinado.
    /// </summary>
    public DateOnly? PlanejadaPara { get; private set; }

    public Guid? SolicitacaoId { get; private set; }

    public Guid? ProjetoId { get; private set; }

    public Guid? RecorrenciaId { get; private set; }

    public DateOnly? DataReferencia { get; private set; }

    /// <summary>Quando foi concluída; zera ao reabrir (o quadro mostra as concluídas recentes, F3 de 21/10).</summary>
    public DateTimeOffset? ConcluidaEm { get; private set; }

    /// <summary>Aparece no portal (RF-TAR-04). Exige <see cref="ClienteId"/>.</summary>
    public bool VisivelCliente { get; private set; }

    /// <summary>Itens na ordem da checklist.</summary>
    public IReadOnlyList<ItemChecklist> Checklist => [.. _checklist.OrderBy(i => i.Ordem)];

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
        bool visivelCliente,
        DateOnly? planejadaPara = null)
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
            PlanejadaPara = planejadaPara,
        };
        tarefa.TornarVisivelAoCliente(visivelCliente);
        tarefa.Registrar(new TarefaCriada(tarefa.Id, tenantId, tarefa.ResponsavelId, tarefa.SolicitacaoId));
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
            tarefa._checklist.Add(new ItemChecklist(Guid.CreateVersion7(), tarefa.ProximaOrdem(), item));
        tarefa.Registrar(new TarefaCriada(tarefa.Id, tarefa.TenantId, tarefa.ResponsavelId));
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
        var novo = Ids.Opcional(responsavelId, nameof(responsavelId));
        if (novo == ResponsavelId)
            return;
        var anterior = ResponsavelId;
        ResponsavelId = novo;
        Registrar(new TarefaAtribuida(Id, TenantId, novo, anterior));
    }

    public void DefinirPrazo(DateOnly? prazo)
    {
        GarantirNaoCancelada(nameof(DefinirPrazo));
        Prazo = prazo;
    }

    public void Planejar(DateOnly? dia)
    {
        GarantirNaoCancelada(nameof(Planejar));
        PlanejadaPara = dia;
    }

    public void DefinirEstimativa(Duracao? estimativa)
    {
        GarantirNaoCancelada(nameof(DefinirEstimativa));
        Estimativa = ValidarEstimativa(estimativa);
    }

    /// <summary>
    /// Vincula, troca ou tira o projeto (F11-23 a F11-26): só projeto EM_ANDAMENTO do mesmo cliente; <c>null</c> tira.
    /// Lê o projeto, não o altera. Apontamentos já feitos mantêm o contrato que consumiram (ADR 0004); o progresso dos
    /// projetos segue a tarefa.
    /// </summary>
    public void MoverParaProjeto(Projeto? projeto)
    {
        GarantirNaoCancelada(nameof(MoverParaProjeto));
        if (projeto?.Id == ProjetoId)
            return;
        if (projeto is not null)
        {
            if (ClienteId is null)
                throw new ProjetoDeOutroClienteException("Tarefa sem cliente não entra em projeto.");
            if (projeto.TenantId != TenantId || projeto.ClienteId != ClienteId)
                throw new ProjetoDeOutroClienteException();
            if (projeto.Status != StatusProjeto.EmAndamento)
                throw new ProjetoEncerradoException();
        }

        var de = ProjetoId;
        ProjetoId = projeto?.Id;
        Registrar(new TarefaProjetoAlterado(Id, TenantId, de, ProjetoId));
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
        MudarStatus(StatusTarefa.Fazendo);
    }

    /// <summary>Volta de FAZENDO para A_FAZER (decidido em 21/10, F3): mover no quadro e desfazer o "Iniciar".</summary>
    public void Retornar()
    {
        Exigir(nameof(Retornar), StatusTarefa.Fazendo);
        MudarStatus(StatusTarefa.AFazer);
    }

    public void Concluir(DateTimeOffset agora)
    {
        Exigir(nameof(Concluir), StatusTarefa.Fazendo);
        ConcluidaEm = agora;
        MudarStatus(StatusTarefa.Concluida);
        Registrar(new TarefaConcluida(Id, TenantId, ClienteId, SolicitacaoId, ProjetoId));
    }

    public void Reabrir()
    {
        Exigir(nameof(Reabrir), StatusTarefa.Concluida);
        ConcluidaEm = null;
        MudarStatus(StatusTarefa.Fazendo);
    }

    public void Cancelar()
    {
        Exigir(nameof(Cancelar), StatusTarefa.AFazer, StatusTarefa.Fazendo);
        MudarStatus(StatusTarefa.Cancelada);
    }

    /// <summary>Adiciona um item ao fim da checklist. O Id do item é gerado aqui (entidade filha).</summary>
    public ItemChecklist AdicionarItem(string texto)
    {
        GarantirNaoCancelada(nameof(AdicionarItem));
        var valido = ItemChecklist.ValidarTexto(texto);
        if (_checklist.Count >= MaximoItensChecklist)
            throw new ArgumentException($"A checklist aceita no máximo {MaximoItensChecklist} itens.", nameof(texto));

        var item = new ItemChecklist(Guid.CreateVersion7(), ProximaOrdem(), valido);
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

    private int ProximaOrdem() => _checklist.Count == 0 ? 1 : _checklist.Max(i => i.Ordem) + 1;

    private ItemChecklist Item(Guid itemId) =>
        _checklist.Find(i => i.Id == itemId)
        ?? throw new ArgumentException("Item de checklist não encontrado na tarefa.", nameof(itemId));

    private static Duracao? ValidarEstimativa(Duracao? estimativa) =>
        estimativa is { EhZero: true }
            ? throw new ArgumentException("A estimativa, se informada, deve ser maior que zero.", nameof(estimativa))
            : estimativa;

    private void MudarStatus(StatusTarefa para)
    {
        var de = Status;
        Status = para;
        Registrar(new TarefaStatusAlterada(Id, TenantId, de, para, Guid.CreateVersion7(), SolicitacaoId));
    }

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
