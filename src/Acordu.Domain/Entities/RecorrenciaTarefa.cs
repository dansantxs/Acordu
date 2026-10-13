using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.Services;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Tarefa recorrente (modelo de domínio §4.9). <see cref="GerarDevidas"/> é puro: devolve as datas devidas e avança
/// <see cref="ProximaData"/>; quem cria as tarefas, no mesmo commit, é a Application. A idempotência (RF-REC-03) tem duas
/// camadas: <see cref="ProximaData"/> avança junto com a criação, e o índice único (RecorrenciaId, DataReferencia) em
/// Tarefa barra duplicata numa corrida.
/// </summary>
public sealed class RecorrenciaTarefa : RaizComCliente
{
    /// <summary>Teto de ocorrências por chamada (agendador parado por muito tempo numa regra semanal, por exemplo).</summary>
    public const int MaximoOcorrenciasPorChamada = 100;

    private RecorrenciaTarefa(Guid id, Guid tenantId, Guid clienteId, ModeloTarefa modelo, RegraRecorrencia regra)
        : base(id, tenantId, clienteId)
    {
        Modelo = modelo;
        Regra = regra;
        Status = StatusRecorrencia.Ativa;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private RecorrenciaTarefa()
    {
        Modelo = null!;
        Regra = null!;
    }

    public Guid? ContratoId { get; private set; }

    public ModeloTarefa Modelo { get; private set; }

    public RegraRecorrencia Regra { get; private set; }

    /// <summary>Próxima data de referência a gerar; nula quando a regra não tem mais ocorrências.</summary>
    public DateOnly? ProximaData { get; private set; }

    public StatusRecorrencia Status { get; private set; }

    /// <summary>
    /// A Application garante que o contrato, se houver, é do mesmo cliente. A primeira ocorrência é a primeira ≥ início e
    /// ≥ <paramref name="hoje"/>: um início no passado só ancora o ciclo, sem gerar tarefas retroativas.
    /// </summary>
    public static RecorrenciaTarefa Criar(
        Guid id,
        Guid tenantId,
        Guid clienteId,
        Guid? contratoId,
        ModeloTarefa modelo,
        RegraRecorrencia regra,
        DateOnly hoje)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        ArgumentNullException.ThrowIfNull(regra);
        var recorrencia = new RecorrenciaTarefa(id, tenantId, clienteId, modelo, regra)
        {
            ContratoId = Ids.Opcional(contratoId, nameof(contratoId)),
        };
        recorrencia.DefinirProxima(GeradorDeDatas.Proxima(regra, regra.Inicio > hoje ? regra.Inicio : hoje));
        return recorrencia;
    }

    /// <summary>
    /// Ocorrências cuja geração já é devida em <paramref name="hoje"/> (data de referência − antecedência ≤ hoje), a partir
    /// de <see cref="ProximaData"/>, avançando-a. Fora de ATIVA não gera nada. Esgotar a regra encerra a recorrência.
    /// </summary>
    public IReadOnlyList<DateOnly> GerarDevidas(DateOnly hoje)
    {
        if (Status != StatusRecorrencia.Ativa)
            return [];

        var devidas = new List<DateOnly>();
        while (ProximaData is { } data
            && data.AddDays(-Regra.AntecedenciaDias) <= hoje
            && devidas.Count < MaximoOcorrenciasPorChamada)
        {
            devidas.Add(data);
            DefinirProxima(GeradorDeDatas.Proxima(Regra, data.AddDays(1)));
        }

        return devidas;
    }

    public void AlterarModelo(ModeloTarefa modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        GarantirNaoEncerrada(nameof(AlterarModelo));
        Modelo = modelo;
    }

    /// <summary>Troca o contrato das próximas tarefas (apontamentos delas); a Application garante que é do mesmo cliente.</summary>
    public void AlterarContrato(Guid? contratoId)
    {
        GarantirNaoEncerrada(nameof(AlterarContrato));
        ContratoId = Ids.Opcional(contratoId, nameof(contratoId));
    }

    /// <summary>Troca a regra; a próxima data passa a ser a primeira ocorrência da nova regra a partir de hoje.</summary>
    public void AlterarRegra(RegraRecorrencia regra, DateOnly hoje)
    {
        ArgumentNullException.ThrowIfNull(regra);
        GarantirNaoEncerrada(nameof(AlterarRegra));
        Regra = regra;
        DefinirProxima(GeradorDeDatas.Proxima(regra, hoje));
    }

    public void Pausar()
    {
        if (Status != StatusRecorrencia.Ativa)
            throw Transicao(nameof(Pausar));
        Status = StatusRecorrencia.Pausada;
    }

    /// <summary>Volta a gerar a partir da primeira ocorrência ≥ hoje: o período pausado não gera tarefas retroativas (RF-REC-05).</summary>
    public void Retomar(DateOnly hoje)
    {
        if (Status != StatusRecorrencia.Pausada)
            throw Transicao(nameof(Retomar));
        Status = StatusRecorrencia.Ativa;
        DefinirProxima(GeradorDeDatas.Proxima(Regra, hoje));
    }

    public void Encerrar()
    {
        GarantirNaoEncerrada(nameof(Encerrar));
        Status = StatusRecorrencia.Encerrada;
    }

    private void DefinirProxima(DateOnly? proxima)
    {
        ProximaData = proxima;
        if (proxima is null)
            Status = StatusRecorrencia.Encerrada;
    }

    private void GarantirNaoEncerrada(string operacao)
    {
        if (Status == StatusRecorrencia.Encerrada)
            throw Transicao(operacao);
    }

    private TransicaoInvalidaException Transicao(string operacao) =>
        new($"A operação {operacao} não é permitida para recorrência {Status}.");
}
