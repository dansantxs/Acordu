using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Pedido do cliente, com protocolo, status e SLA (modelo de domínio §4.3). A tabela de transições do modelo é a
/// fonte da verdade da máquina de estados; qualquer transição fora dela lança <see cref="TransicaoInvalidaException"/>.
/// Regras que cruzam agregados (contrato do mesmo cliente e vigente, primeira resposta a partir de uma interação)
/// são orquestradas pela Application.
/// </summary>
public sealed class Solicitacao : RaizComCliente
{
    private Solicitacao(
        Guid id,
        Guid tenantId,
        Guid clienteId,
        int numero,
        string titulo,
        string? descricao,
        Prioridade prioridade,
        OrigemSolicitacao origem,
        DateTimeOffset abertaEm)
        : base(id, tenantId, clienteId)
    {
        Numero = numero;
        Titulo = titulo;
        Descricao = descricao;
        Prioridade = prioridade;
        Origem = origem;
        AbertaEm = abertaEm;
        Status = StatusSolicitacao.Aberta;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Solicitacao()
    {
        Titulo = null!;
    }

    /// <summary>Protocolo sequencial por tenant, gerado na Infrastructure e imutável (§3.5).</summary>
    public int Numero { get; private set; }

    public string Titulo { get; private set; }

    public string? Descricao { get; private set; }

    public Prioridade Prioridade { get; private set; }

    public StatusSolicitacao Status { get; private set; }

    public OrigemSolicitacao Origem { get; private set; }

    public Guid? ContatoId { get; private set; }

    public Guid? ContratoId { get; private set; }

    public Guid? CategoriaId { get; private set; }

    public Guid? ResponsavelId { get; private set; }

    /// <summary>Relógio do SLA; nulo quando não há contrato (SLA não se aplica).</summary>
    public RelogioSla? Sla { get; private set; }

    public Solucao? Solucao { get; private set; }

    public DateTimeOffset AbertaEm { get; private set; }

    /// <summary>Primeiro comentário EXTERNO da equipe (RF-SOL-07); registrado mesmo sem SLA.</summary>
    public DateTimeOffset? PrimeiraRespostaEm { get; private set; }

    public DateTimeOffset? ResolvidaEm { get; private set; }

    public DateTimeOffset? FechadaEm { get; private set; }

    public DateTimeOffset? CanceladaEm { get; private set; }

    public string? MotivoCancelamento { get; private set; }

    public bool EstaFinalizada => Status is StatusSolicitacao.Fechada or StatusSolicitacao.Cancelada;

    /// <summary>Status exibido no portal (RF-POR-02).</summary>
    public StatusExterno StatusExterno => ParaExterno(Status);

    /// <summary>
    /// Abre a solicitação. <paramref name="contratoId"/> e <paramref name="politicaSla"/> vêm juntos ou não vêm:
    /// sem contrato não há SLA. A Application garante que o contrato é do mesmo cliente e está vigente.
    /// </summary>
    public static Solicitacao Abrir(
        Guid id,
        Guid tenantId,
        Guid clienteId,
        int numero,
        string titulo,
        string? descricao,
        Prioridade prioridade,
        OrigemSolicitacao origem,
        Guid? contatoId,
        Guid? categoriaId,
        Guid? responsavelId,
        Guid? contratoId,
        PoliticaSla? politicaSla,
        DateTimeOffset agora,
        CalendarioUtil calendario,
        Guid? abertaPor = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numero);
        ValidarPrioridade(prioridade);
        if (!Enum.IsDefined(origem))
            throw new ArgumentOutOfRangeException(nameof(origem), origem, "Origem inválida.");
        ValidarVinculoContrato(contratoId, politicaSla);
        ArgumentNullException.ThrowIfNull(calendario);

        var solicitacao = new Solicitacao(
            id,
            tenantId,
            clienteId,
            numero,
            Texto.Obrigatorio(titulo, LimitesTexto.Titulo, nameof(titulo)),
            Texto.Opcional(descricao, LimitesTexto.Descricao, nameof(descricao)),
            prioridade,
            origem,
            agora.ToUniversalTime())
        {
            ContatoId = Ids.Opcional(contatoId, nameof(contatoId)),
            CategoriaId = Ids.Opcional(categoriaId, nameof(categoriaId)),
            ResponsavelId = Ids.Opcional(responsavelId, nameof(responsavelId)),
            ContratoId = contratoId,
        };
        if (politicaSla is not null)
            solicitacao.Sla = RelogioSla.Iniciar(politicaSla.PrazosDe(prioridade), agora, calendario);

        solicitacao.Registrar(new SolicitacaoAberta(
            id, tenantId, clienteId, numero, origem, solicitacao.AbertaEm, Ids.Opcional(abertaPor, nameof(abertaPor))));
        return solicitacao;
    }

    public void Editar(string titulo, string? descricao)
    {
        GarantirNaoFinalizada(nameof(Editar));
        Titulo = Texto.Obrigatorio(titulo, LimitesTexto.Titulo, nameof(titulo));
        Descricao = Texto.Opcional(descricao, LimitesTexto.Descricao, nameof(descricao));
    }

    public void DefinirCategoria(Guid? categoriaId)
    {
        GarantirNaoFinalizada(nameof(DefinirCategoria));
        CategoriaId = Ids.Opcional(categoriaId, nameof(categoriaId));
    }

    /// <summary>
    /// Define ou remove o responsável. Emite <see cref="SolicitacaoAtribuida"/> só quando muda, com quem atribuiu (para não
    /// avisar quem atribuiu a si mesmo).
    /// </summary>
    public void Atribuir(Guid? responsavelId, Guid? atribuidoPor = null)
    {
        GarantirNaoFinalizada(nameof(Atribuir));
        var novo = Ids.Opcional(responsavelId, nameof(responsavelId));
        if (novo == ResponsavelId)
            return;

        var anterior = ResponsavelId;
        ResponsavelId = novo;
        Registrar(new SolicitacaoAtribuida(
            Id, TenantId, ClienteId, novo, Ids.Opcional(atribuidoPor, nameof(atribuidoPor)), anterior));
    }

    /// <summary>
    /// Muda a prioridade e, havendo SLA, recalcula os prazos a partir do início do ciclo (§4.4, regra 4).
    /// A <paramref name="politicaSla"/> do contrato é obrigatória se, e só se, há contrato.
    /// Não permitido em RESOLVIDA nem em estado final.
    /// </summary>
    public void Repriorizar(Prioridade prioridade, PoliticaSla? politicaSla, CalendarioUtil calendario)
    {
        ValidarPrioridade(prioridade);
        ArgumentNullException.ThrowIfNull(calendario);
        if (EstaFinalizada || Status == StatusSolicitacao.Resolvida)
            throw Transicao(nameof(Repriorizar));
        ValidarVinculoContrato(ContratoId, politicaSla);

        Prioridade = prioridade;
        if (Sla is not null && politicaSla is not null)
            Sla = Sla.Repriorizar(politicaSla.PrazosDe(prioridade), PrimeiraRespostaEm is not null, calendario);
    }

    public void Iniciar(DateTimeOffset agora)
    {
        Exigir(nameof(Iniciar), StatusSolicitacao.Aberta);
        MudarStatus(StatusSolicitacao.EmAndamento, agora);
    }

    /// <summary>Pausa o prazo de resolução (o de primeira resposta não pausa).</summary>
    public void AguardarCliente(DateTimeOffset agora)
    {
        Exigir(nameof(AguardarCliente), StatusSolicitacao.Aberta, StatusSolicitacao.EmAndamento);
        Sla = Sla?.Pausar(agora);
        MudarStatus(StatusSolicitacao.AguardandoCliente, agora);
    }

    /// <summary>Espera por fornecedor: aparece ao cliente como "em andamento" e o SLA <b>não</b> pausa.</summary>
    public void AguardarTerceiro(DateTimeOffset agora)
    {
        Exigir(nameof(AguardarTerceiro), StatusSolicitacao.Aberta, StatusSolicitacao.EmAndamento);
        MudarStatus(StatusSolicitacao.AguardandoTerceiro, agora);
    }

    public void Retomar(DateTimeOffset agora, CalendarioUtil calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        Exigir(nameof(Retomar), StatusSolicitacao.AguardandoCliente, StatusSolicitacao.AguardandoTerceiro);
        Sla = Sla?.Retomar(agora, calendario);
        MudarStatus(StatusSolicitacao.EmAndamento, agora);
    }

    /// <summary>
    /// Registra o instante da primeira resposta. Idempotente: só a primeira chamada grava e emite evento,
    /// porque o handler de interações chama a cada comentário externo da equipe (§3.1).
    /// </summary>
    public void RegistrarPrimeiraResposta(DateTimeOffset agora)
    {
        if (PrimeiraRespostaEm is not null)
            return;
        GarantirNaoFinalizada(nameof(RegistrarPrimeiraResposta));

        PrimeiraRespostaEm = agora.ToUniversalTime();
        Registrar(new PrimeiraRespostaRegistrada(Id, TenantId, ClienteId, PrimeiraRespostaEm.Value));
    }

    /// <summary>Resolve com solução obrigatória. Resolver durante a pausa encerra a pausa antes de parar o relógio.</summary>
    public void Resolver(Solucao solucao, DateTimeOffset agora, CalendarioUtil calendario)
    {
        ArgumentNullException.ThrowIfNull(solucao);
        ArgumentNullException.ThrowIfNull(calendario);
        Exigir(
            nameof(Resolver),
            StatusSolicitacao.Aberta,
            StatusSolicitacao.EmAndamento,
            StatusSolicitacao.AguardandoCliente,
            StatusSolicitacao.AguardandoTerceiro);

        Sla = Sla?.Retomar(agora, calendario);
        Solucao = solucao;
        ResolvidaEm = agora.ToUniversalTime();
        MudarStatus(StatusSolicitacao.Resolvida, agora);
        Registrar(new SolicitacaoResolvida(Id, TenantId, ClienteId, ResolvidaEm.Value));
    }

    /// <summary>
    /// Volta para EM_ANDAMENTO com novo ciclo de SLA. Motivo sempre obrigatório (reprovação do cliente ou decisão
    /// da equipe). A solução reprovada é descartada para não aparecer em "semelhantes". A janela de N dias
    /// (RF-SOL-10) é garantida pela Application e pelo fechamento automático.
    /// </summary>
    public void Reabrir(string motivo, DateTimeOffset agora, CalendarioUtil calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        var motivoValido = Texto.Obrigatorio(motivo, LimitesTexto.Motivo, nameof(motivo));
        Exigir(nameof(Reabrir), StatusSolicitacao.Resolvida);

        Sla = Sla?.Reabrir(agora, calendario);
        Solucao = null;
        ResolvidaEm = null;
        MudarStatus(StatusSolicitacao.EmAndamento, agora);
        Registrar(new SolicitacaoReaberta(Id, TenantId, ClienteId, motivoValido, agora.ToUniversalTime()));
    }

    public void Fechar(DateTimeOffset agora)
    {
        Exigir(nameof(Fechar), StatusSolicitacao.Resolvida);
        FechadaEm = agora.ToUniversalTime();
        MudarStatus(StatusSolicitacao.Fechada, agora);
    }

    public void Cancelar(string motivo, DateTimeOffset agora)
    {
        var motivoValido = Texto.Obrigatorio(motivo, LimitesTexto.Motivo, nameof(motivo));
        Exigir(
            nameof(Cancelar),
            StatusSolicitacao.Aberta,
            StatusSolicitacao.EmAndamento,
            StatusSolicitacao.AguardandoCliente,
            StatusSolicitacao.AguardandoTerceiro);

        MotivoCancelamento = motivoValido;
        CanceladaEm = agora.ToUniversalTime();
        MudarStatus(StatusSolicitacao.Cancelada, agora);
    }

    /// <summary>
    /// Situação do SLA para exibição (read side §5): sem contrato ou cancelada → SEM_SLA; resolvida/fechada →
    /// CUMPRIDO ou VIOLADO pela data de resolução; em aberto → a pior entre primeira resposta e resolução.
    /// </summary>
    public SituacaoSla SituacaoSla(DateTimeOffset agora, CalendarioUtil calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        if (Sla is null || Status == StatusSolicitacao.Cancelada)
            return Enums.SituacaoSla.SemSla;
        if (ResolvidaEm is { } resolvidaEm)
            return resolvidaEm <= Sla.PrazoResolucao ? Enums.SituacaoSla.Cumprido : Enums.SituacaoSla.Violado;

        var resolucao = Sla.SituacaoResolucao(agora, calendario);
        var primeiraResposta = Sla.SituacaoPrimeiraResposta(PrimeiraRespostaEm, agora, calendario);
        return Pior(resolucao, primeiraResposta);
    }

    public static StatusExterno ParaExterno(StatusSolicitacao status) => status switch
    {
        StatusSolicitacao.Aberta => StatusExterno.Recebida,
        StatusSolicitacao.EmAndamento or StatusSolicitacao.AguardandoTerceiro => StatusExterno.EmAndamento,
        StatusSolicitacao.AguardandoCliente => StatusExterno.AguardandoVoce,
        StatusSolicitacao.Resolvida or StatusSolicitacao.Fechada => StatusExterno.Concluida,
        StatusSolicitacao.Cancelada => StatusExterno.Cancelada,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Status inválido."),
    };

    private static SituacaoSla Pior(SituacaoSla a, SituacaoSla b) => Gravidade(a) >= Gravidade(b) ? a : b;

    private static int Gravidade(SituacaoSla situacao) => situacao switch
    {
        Enums.SituacaoSla.Violado => 3,
        Enums.SituacaoSla.EmRisco => 2,
        Enums.SituacaoSla.NoPrazo => 1,
        _ => 0,
    };

    private void MudarStatus(StatusSolicitacao novo, DateTimeOffset agora)
    {
        var anterior = Status;
        Status = novo;
        Registrar(new SolicitacaoStatusAlterado(
            Id, TenantId, ClienteId, anterior, novo, ParaExterno(anterior), ParaExterno(novo), agora.ToUniversalTime()));
    }

    private void Exigir(string operacao, params StatusSolicitacao[] permitidos)
    {
        if (!permitidos.Contains(Status))
            throw Transicao(operacao);
    }

    private void GarantirNaoFinalizada(string operacao)
    {
        if (EstaFinalizada)
            throw Transicao(operacao);
    }

    private TransicaoInvalidaException Transicao(string operacao) =>
        new($"A operação {operacao} não é permitida para solicitação {Status}.");

    private static void ValidarPrioridade(Prioridade prioridade)
    {
        if (!Enum.IsDefined(prioridade))
            throw new ArgumentOutOfRangeException(nameof(prioridade), prioridade, "Prioridade inválida.");
    }

    private static void ValidarVinculoContrato(Guid? contratoId, PoliticaSla? politicaSla)
    {
        if (contratoId == Guid.Empty)
            throw new ArgumentException("O ContratoId não pode ser vazio.", nameof(contratoId));
        if (contratoId is null != politicaSla is null)
            throw new ArgumentException("Contrato e política de SLA devem ser informados juntos.", nameof(politicaSla));
    }
}
