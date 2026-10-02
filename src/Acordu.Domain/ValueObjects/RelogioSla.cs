using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Services;

namespace Acordu.Domain.ValueObjects;

/// <summary>
/// Relógio de SLA de uma solicitação com contrato (ADR 0003, modelo de domínio §4.4). Imutável: cada operação
/// devolve um novo relógio. Todos os instantes derivam de uma única fórmula sobre o ciclo corrente:
/// <code>
/// PrazoResolucao = Somar(InicioCiclo, TempoResolucao + PausadoNoCiclo)
/// LimiteRisco    = Somar(InicioCiclo, ⌈75% de TempoResolucao⌉ + PausadoNoCiclo)
/// </code>
/// Assim pausar, retomar e repriorizar nunca "apagam" uma violação: um prazo que já passou continua no passado.
/// O prazo de primeira resposta conta desde a abertura e nunca pausa.
/// </summary>
public sealed record RelogioSla
{
    /// <summary>Percentual do tempo útil a partir do qual a solicitação está "em risco".</summary>
    public const int PercentualRisco = 75;

    private RelogioSla(
        DateTimeOffset abertoEm,
        DateTimeOffset inicioCiclo,
        Duracao tempoPrimeiraResposta,
        Duracao tempoResolucao,
        Duracao pausadoNoCiclo,
        DateTimeOffset? pausadoDesde,
        CalendarioUtil calendario)
    {
        AbertoEm = abertoEm;
        InicioCiclo = inicioCiclo;
        TempoPrimeiraResposta = tempoPrimeiraResposta;
        TempoResolucao = tempoResolucao;
        PausadoNoCiclo = pausadoNoCiclo;
        PausadoDesde = pausadoDesde;

        PrazoPrimeiraResposta = CalculadoraSla.Somar(calendario, abertoEm, tempoPrimeiraResposta);
        PrazoResolucao = CalculadoraSla.Somar(calendario, inicioCiclo, tempoResolucao + pausadoNoCiclo);
        LimiteRisco = CalculadoraSla.Somar(calendario, inicioCiclo, TempoAteRisco(tempoResolucao) + pausadoNoCiclo);
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private RelogioSla()
    {
    }

    /// <summary>Instante da abertura da solicitação; base do prazo de primeira resposta.</summary>
    public DateTimeOffset AbertoEm { get; private init; }

    /// <summary>Início do ciclo de resolução corrente: a abertura ou a última reabertura.</summary>
    public DateTimeOffset InicioCiclo { get; private init; }

    /// <summary>Minutos úteis de primeira resposta da prioridade vigente.</summary>
    public Duracao TempoPrimeiraResposta { get; private init; }

    /// <summary>Minutos úteis de resolução da prioridade vigente.</summary>
    public Duracao TempoResolucao { get; private init; }

    /// <summary>Minutos úteis já passados em AGUARDANDO_CLIENTE no ciclo, sem contar a pausa em curso.</summary>
    public Duracao PausadoNoCiclo { get; private init; }

    public DateTimeOffset? PausadoDesde { get; private init; }

    public DateTimeOffset PrazoPrimeiraResposta { get; private init; }

    public DateTimeOffset PrazoResolucao { get; private init; }

    /// <summary>Instante em que 75% do tempo útil de resolução é atingido (filtro "em risco" no SQL — read side §5).</summary>
    public DateTimeOffset LimiteRisco { get; private init; }

    public bool Pausado => PausadoDesde is not null;

    public static RelogioSla Iniciar(PrazoSla prazo, DateTimeOffset agora, CalendarioUtil calendario)
    {
        ArgumentNullException.ThrowIfNull(prazo);
        ArgumentNullException.ThrowIfNull(calendario);
        var inicio = agora.ToUniversalTime();
        return new RelogioSla(inicio, inicio, prazo.PrimeiraResposta, prazo.Resolucao, Duracao.Zero, null, calendario);
    }

    /// <summary>Congela o prazo de resolução. Pausar um relógio já pausado não muda nada.</summary>
    public RelogioSla Pausar(DateTimeOffset agora) =>
        Pausado ? this : this with { PausadoDesde = agora.ToUniversalTime() };

    /// <summary>
    /// Encerra a pausa em curso, somando os minutos úteis pausados ao ciclo. Sem pausa, não muda nada.
    /// Pausa iniciada depois do prazo já vencido não desloca o prazo: a violação fica no instante original.
    /// </summary>
    public RelogioSla Retomar(DateTimeOffset agora, CalendarioUtil calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        if (PausadoDesde is not { } desde)
            return this;

        var pausado = desde >= PrazoResolucao
            ? PausadoNoCiclo
            : PausadoNoCiclo + CalculadoraSla.MinutosUteisEntre(calendario, desde, agora);
        return new RelogioSla(AbertoEm, InicioCiclo, TempoPrimeiraResposta, TempoResolucao, pausado, null, calendario);
    }

    /// <summary>
    /// Aplica os prazos de outra prioridade a partir do início do ciclo, descontando o tempo pausado.
    /// O prazo de primeira resposta só muda se ela ainda não aconteceu.
    /// </summary>
    public RelogioSla Repriorizar(PrazoSla prazo, bool primeiraRespostaRegistrada, CalendarioUtil calendario)
    {
        ArgumentNullException.ThrowIfNull(prazo);
        ArgumentNullException.ThrowIfNull(calendario);
        return new RelogioSla(
            AbertoEm,
            InicioCiclo,
            primeiraRespostaRegistrada ? TempoPrimeiraResposta : prazo.PrimeiraResposta,
            prazo.Resolucao,
            PausadoNoCiclo,
            PausadoDesde,
            calendario);
    }

    /// <summary>Novo ciclo de resolução com o prazo cheio a partir da reabertura (ADR 0003, decisão 6).</summary>
    public RelogioSla Reabrir(DateTimeOffset agora, CalendarioUtil calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        return new RelogioSla(
            AbertoEm, agora.ToUniversalTime(), TempoPrimeiraResposta, TempoResolucao, Duracao.Zero, null, calendario);
    }

    /// <summary>
    /// Situação do prazo de resolução de uma solicitação ainda não resolvida. Pausado → calculada no instante da pausa.
    /// </summary>
    public SituacaoSla SituacaoResolucao(DateTimeOffset agora, CalendarioUtil calendario)
    {
        var momento = PausadoDesde ?? agora;
        if (momento > PrazoResolucao)
            return SituacaoSla.Violado;

        return PercentualResolucao(agora, calendario) >= PercentualRisco ? SituacaoSla.EmRisco : SituacaoSla.NoPrazo;
    }

    /// <summary>Situação da primeira resposta: comparada com o prazo se já aconteceu; senão, pela mesma regra de risco.</summary>
    public SituacaoSla SituacaoPrimeiraResposta(DateTimeOffset? primeiraRespostaEm, DateTimeOffset agora, CalendarioUtil calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        if (primeiraRespostaEm is { } em)
            return em <= PrazoPrimeiraResposta ? SituacaoSla.Cumprido : SituacaoSla.Violado;
        if (agora > PrazoPrimeiraResposta)
            return SituacaoSla.Violado;

        var decorrido = CalculadoraSla.MinutosUteisEntre(calendario, AbertoEm, agora);
        return Percentual(decorrido, TempoPrimeiraResposta) >= PercentualRisco ? SituacaoSla.EmRisco : SituacaoSla.NoPrazo;
    }

    /// <summary>Percentual do tempo útil de resolução consumido no ciclo (pode passar de 100 quando violado).</summary>
    public int PercentualResolucao(DateTimeOffset agora, CalendarioUtil calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        var momento = PausadoDesde ?? agora;
        var bruto = CalculadoraSla.MinutosUteisEntre(calendario, InicioCiclo, momento);
        var decorrido = bruto > PausadoNoCiclo ? bruto - PausadoNoCiclo : Duracao.Zero;
        return Percentual(decorrido, TempoResolucao);
    }

    private static Duracao TempoAteRisco(Duracao total) =>
        Duracao.FromMinutos((int)Math.Ceiling(total.Minutos * PercentualRisco / 100m));

    private static int Percentual(Duracao decorrido, Duracao total) =>
        (int)(decorrido.Minutos * 100L / total.Minutos);
}
