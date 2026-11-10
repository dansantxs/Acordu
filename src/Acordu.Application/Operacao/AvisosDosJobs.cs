using System.Globalization;
using Acordu.Application.Abstractions.Operacao;
using Acordu.Domain.Entities;

namespace Acordu.Application.Operacao;

/// <summary>Falhas de um passo de job (por passo que lançou ou por tenant que falhou nele).</summary>
public sealed record FalhaDePasso(string Passo, int Falhas);

/// <summary>
/// Avisos dos jobs agendados (spec do F13, F13c; ADR 0012): falha de passo em #alertas, uma vez por passo e por dia
/// (F13-13); diário sem falhas, o resumo em #deploy, uma vez por dia (F13-14), e a falta dele avisa que o agendamento
/// parou. O dia é o da operação (Brasília), não o de algum tenant. Só números e nomes de passo: nada de tenant.
/// </summary>
public sealed class AvisosDosJobs
{
    /// <summary>Fuso da operação da Nebrys (o padrão do produto, conferido na partida): decide o "dia" das marcas.</summary>
    private static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById(CalendarioUtil.FusoPadrao);

    private readonly IMensageiroOperacao _mensageiro;
    private readonly IMarcasDeOperacao _marcas;
    private readonly TimeProvider _relogio;

    public AvisosDosJobs(IMensageiroOperacao mensageiro, IMarcasDeOperacao marcas, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(mensageiro);
        ArgumentNullException.ThrowIfNull(marcas);
        ArgumentNullException.ThrowIfNull(relogio);
        _mensageiro = mensageiro;
        _marcas = marcas;
        _relogio = relogio;
    }

    /// <summary>Uma mensagem por passo que falhou, se ainda não houve aviso dele hoje. Devolve quantas saíram.</summary>
    public async Task<int> FalhasAsync(string job, IEnumerable<FalhaDePasso> passos, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(passos);
        if (!_mensageiro.Configurado(CanalOperacao.Alertas))
            return 0;

        var enviadas = 0;
        foreach (var (passo, falhas) in passos.Where(p => p.Falhas > 0))
        {
            var texto = string.Create(
                CultureInfo.InvariantCulture, $"**Job {job}**: {falhas} {(falhas == 1 ? "falha" : "falhas")} em `{passo}` hoje. Detalhes no log.");
            if (await UmaVezHojeAsync($"job:{job}:{passo}", CanalOperacao.Alertas, texto, cancellationToken))
                enviadas++;
        }

        return enviadas;
    }

    /// <summary>
    /// Fim do diário: com falha em algum passo, só os alertas (F13-13); sem falha, o resumo em #deploy (F13-14).
    /// </summary>
    public async Task DiarioAsync(
        IReadOnlyList<FalhaDePasso> falhas, IEnumerable<(string Rotulo, int Valor)> contagens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(falhas);
        if (falhas.Any(f => f.Falhas > 0))
            await FalhasAsync("diario", falhas, cancellationToken);
        else
            await ResumoDoDiarioAsync(contagens, cancellationToken);
    }

    /// <summary>
    /// Falhas por passo: o passo que lançou conta 1; o que falhou em alguns tenants conta quantos (sem somar os dois,
    /// porque a falha por tenant só é conhecida quando o passo termina).
    /// </summary>
    public static IReadOnlyList<FalhaDePasso> PorPasso(IEnumerable<string> passosQueLancaram, IEnumerable<FalhaDePasso> porTenant)
    {
        ArgumentNullException.ThrowIfNull(passosQueLancaram);
        ArgumentNullException.ThrowIfNull(porTenant);
        return passosQueLancaram.Select(p => new FalhaDePasso(p, 1))
            .Concat(porTenant)
            .GroupBy(f => f.Passo, StringComparer.Ordinal)
            .Select(g => new FalhaDePasso(g.Key, g.Sum(f => f.Falhas)))
            .Where(f => f.Falhas > 0)
            .ToList();
    }

    /// <summary>Resumo do diário em #deploy (só quando não houve falha), uma vez por dia.</summary>
    public Task<bool> ResumoDoDiarioAsync(IEnumerable<(string Rotulo, int Valor)> contagens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contagens);
        if (!_mensageiro.Configurado(CanalOperacao.Deploy))
            return Task.FromResult(false);

        var dia = Hoje();
        var itens = string.Join(", ", contagens.Select(c => string.Create(CultureInfo.InvariantCulture, $"{c.Rotulo} {c.Valor}")));
        var texto = string.Create(CultureInfo.InvariantCulture, $"**Diário ok** · {dia:dd/MM}: {itens}.");
        return UmaVezHojeAsync("diario:resumo", CanalOperacao.Deploy, texto, cancellationToken);
    }

    private async Task<bool> UmaVezHojeAsync(string chave, CanalOperacao canal, string texto, CancellationToken cancellationToken)
    {
        var marca = string.Create(CultureInfo.InvariantCulture, $"{chave}:{Hoje():yyyy-MM-dd}");
        if (!await _marcas.MarcarAsync(marca, cancellationToken))
            return false;
        if (await _mensageiro.EnviarAsync(canal, texto, cancellationToken))
            return true;
        // Sem o token de quem chamou: se ele cancelou, é justamente quando o aviso não saiu e a marca precisa sair.
        await _marcas.DesmarcarAsync(marca, CancellationToken.None);
        return false;
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_relogio.GetUtcNow(), Fuso).DateTime);
}
