using Acordu.Domain.Common;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Tempo gasto numa tarefa (modelo de domínio §4.8). O <see cref="ContratoId"/> é resolvido pela Application a partir da
/// solicitação/projeto da tarefa e <b>congelado</b> no registro (ADR 0004). Os meses fechados do contrato são passados
/// pela Application; sem contrato não há fechamento. Autorização (autor ou <c>apontamento.editar_todos</c>) é da Application.
/// </summary>
public sealed class Apontamento : EntidadeBase
{
    /// <summary>Duração máxima de um apontamento.</summary>
    public static readonly Duracao DuracaoMaxima = Duracao.FromHoras(24);

    private Apontamento(Guid id, Guid tenantId)
        : base(id, tenantId)
    {
        Descricao = null!;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Apontamento()
    {
        Descricao = null!;
    }

    public Guid TarefaId { get; private set; }

    public Guid? ClienteId { get; private set; }

    public Guid UsuarioId { get; private set; }

    public Guid? ContratoId { get; private set; }

    public DateOnly Data { get; private set; }

    public Duracao Duracao { get; private set; }

    public string Descricao { get; private set; }

    /// <summary>Texto mostrado no portal no lugar da descrição interna; só com <see cref="VisivelCliente"/>.</summary>
    public string? DescricaoExterna { get; private set; }

    public bool Faturavel { get; private set; }

    public bool VisivelCliente { get; private set; }

    /// <summary>
    /// Registra o apontamento. <paramref name="visivelCliente"/> nulo assume o padrão do RF-APT-04: visível se faturável
    /// (e se houver cliente).
    /// </summary>
    public static Apontamento Registrar(
        Guid id,
        Guid tenantId,
        Guid tarefaId,
        Guid? clienteId,
        Guid usuarioId,
        Guid? contratoId,
        DateOnly data,
        Duracao duracao,
        string descricao,
        string? descricaoExterna,
        bool faturavel,
        bool? visivelCliente,
        IReadOnlyCollection<MesReferencia> periodosFechados)
    {
        ArgumentNullException.ThrowIfNull(periodosFechados);
        var apontamento = new Apontamento(id, tenantId)
        {
            TarefaId = Ids.Obrigatorio(tarefaId, nameof(tarefaId)),
            ClienteId = Ids.Opcional(clienteId, nameof(clienteId)),
            UsuarioId = Ids.Obrigatorio(usuarioId, nameof(usuarioId)),
            ContratoId = Ids.Opcional(contratoId, nameof(contratoId)),
        };
        var visivel = visivelCliente ?? (faturavel && apontamento.ClienteId is not null);
        apontamento.Aplicar(data, duracao, descricao, descricaoExterna, faturavel, visivel, periodosFechados);
        apontamento.Registrar(new ApontamentoRegistrado(
            id, tenantId, apontamento.ClienteId, apontamento.ContratoId, apontamento.Data, apontamento.Duracao, faturavel));
        return apontamento;
    }

    /// <summary>Edita todos os campos editáveis. Bloqueia se a data atual <b>ou</b> a nova estiver em período fechado.</summary>
    public void Editar(
        DateOnly data,
        Duracao duracao,
        string descricao,
        string? descricaoExterna,
        bool faturavel,
        bool visivelCliente,
        IReadOnlyCollection<MesReferencia> periodosFechados)
    {
        ArgumentNullException.ThrowIfNull(periodosFechados);
        GarantirAberto(Data, periodosFechados);
        var anterior = Data;
        Aplicar(data, duracao, descricao, descricaoExterna, faturavel, visivelCliente, periodosFechados);
        Registrar(new ApontamentoAlterado(Id, TenantId, ClienteId, ContratoId, anterior, Data));
    }

    /// <summary>Valida e registra o evento; quem remove o registro é a Application.</summary>
    public void Excluir(IReadOnlyCollection<MesReferencia> periodosFechados)
    {
        ArgumentNullException.ThrowIfNull(periodosFechados);
        GarantirAberto(Data, periodosFechados);
        Registrar(new ApontamentoExcluido(Id, TenantId, ClienteId, ContratoId, Data));
    }

    private void Aplicar(
        DateOnly data,
        Duracao duracao,
        string descricao,
        string? descricaoExterna,
        bool faturavel,
        bool visivelCliente,
        IReadOnlyCollection<MesReferencia> periodosFechados)
    {
        if (duracao.EhZero || duracao > DuracaoMaxima)
            throw new ArgumentOutOfRangeException(nameof(duracao), duracao, "A duração deve ser maior que zero e no máximo 24h.");
        var descricaoValida = Texto.Obrigatorio(descricao, LimitesTexto.DescricaoApontamento, nameof(descricao));
        var externaValida = Texto.Opcional(descricaoExterna, LimitesTexto.DescricaoApontamento, nameof(descricaoExterna));
        if (visivelCliente && ClienteId is null)
            throw new ArgumentException("Só apontamento de tarefa com cliente pode ficar visível no portal.", nameof(visivelCliente));
        if (externaValida is not null && !visivelCliente)
            throw new ArgumentException("Descrição externa só existe em apontamento visível ao cliente.", nameof(descricaoExterna));
        GarantirAberto(data, periodosFechados);

        Data = data;
        Duracao = duracao;
        Descricao = descricaoValida;
        DescricaoExterna = externaValida;
        Faturavel = faturavel;
        VisivelCliente = visivelCliente;
    }

    private void GarantirAberto(DateOnly data, IReadOnlyCollection<MesReferencia> periodosFechados)
    {
        if (ContratoId is null)
            return;

        var mes = MesReferencia.De(data);
        if (periodosFechados.Contains(mes))
            throw new PeriodoFechadoException($"O período {mes} do contrato está fechado para apontamentos.");
    }
}
