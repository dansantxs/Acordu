using System.Globalization;
using Acordu.Application.Abstractions.Operacao;

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
    /// <summary>Fuso da operação da Nebrys: decide o "dia" das marcas.</summary>
    public const string FusoOperacao = "America/Sao_Paulo";

    private static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById(FusoOperacao);

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
        await _marcas.DesmarcarAsync(marca, cancellationToken);
        return false;
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_relogio.GetUtcNow(), Fuso).DateTime);
}
