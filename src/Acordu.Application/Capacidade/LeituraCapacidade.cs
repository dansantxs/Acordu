using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;

namespace Acordu.Application.Capacidade;

/// <summary>
/// Capacidade de uma semana (contrato §11c). A semana vai de segunda a domingo; qualquer data dela serve, e sem data vale
/// a semana de hoje no fuso do tenant. Só conta tarefa em aberto (A_FAZER e FAZENDO) com dia na semana; atrasadas de
/// semanas anteriores não entram (a agenda as mostra).
/// </summary>
public sealed class LeituraCapacidade
{
    private readonly ITenantContext _contexto;
    private readonly IConsultasCapacidade _consultas;
    private readonly ICalendarios _calendarios;
    private readonly RelogioDoTenant _relogio;

    public LeituraCapacidade(ITenantContext contexto, IConsultasCapacidade consultas, ICalendarios calendarios, RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(calendarios);
        ArgumentNullException.ThrowIfNull(relogio);
        _contexto = contexto;
        _consultas = consultas;
        _calendarios = calendarios;
        _relogio = relogio;
    }

    public async Task<CapacidadeSemanaDto> ObterAsync(DateOnly? semana, CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.TarefaVer);
        var referencia = semana ?? await _relogio.HojeAsync(cancellationToken);
        var segunda = referencia.AddDays(-(((int)referencia.DayOfWeek + 6) % 7));
        var domingo = segunda.AddDays(6);

        var tarefas = await _consultas.TarefasAsync(segunda, domingo, cancellationToken);
        IReadOnlyList<Guid?>? solicitacoes = null;
        if (_contexto.Permissoes.Contains(Permissoes.SolicitacaoVer))
        {
            var fuso = (await _calendarios.DoTenantAsync(cancellationToken)).Fuso;
            solicitacoes = await _consultas.SolicitacoesAsync(Instante(segunda, fuso), Instante(domingo.AddDays(1), fuso), cancellationToken);
        }

        var equipe = await _consultas.EquipeAsync(cancellationToken);
        var pessoas = equipe.Select(m =>
        {
            var capacidade = (m.Capacidade ?? Vinculo.CapacidadePadrao).Minutos;
            var carga = Carga(segunda, tarefas.Where(t => t.ResponsavelId == m.Pessoa.Id), solicitacoes?.Count(r => r == m.Pessoa.Id));
            int? percentual = capacidade == 0 ? null : (int)(carga.PlanejadoMinutos * 100L / capacidade);
            return new CapacidadePessoaDto(m.Pessoa, capacidade, percentual, carga);
        }).ToList();

        // Responsável que saiu da equipe conta como sem responsável: a faixa dele não existe mais.
        var daEquipe = equipe.Select(m => m.Pessoa.Id).ToHashSet();
        var semResponsavel = Carga(
            segunda,
            tarefas.Where(t => t.ResponsavelId is not { } id || !daEquipe.Contains(id)),
            solicitacoes?.Count(r => r is not { } id || !daEquipe.Contains(id)));
        return new CapacidadeSemanaDto(segunda, domingo, pessoas, semResponsavel);
    }

    private static CargaDto Carga(DateOnly segunda, IEnumerable<TarefaCapacidade> tarefas, int? solicitacoes)
    {
        var lista = tarefas.ToList();
        var porDia = Enumerable.Range(0, 7)
            .Select(i => segunda.AddDays(i))
            .Select(d => new CargaDiaDto(d, lista.Where(t => t.Dia == d).Sum(t => t.EstimativaMinutos ?? 0)))
            .ToList();
        return new CargaDto(
            lista.Sum(t => t.EstimativaMinutos ?? 0),
            lista.Count,
            lista.Count(t => t.EstimativaMinutos is null),
            solicitacoes,
            porDia);
    }

    private static DateTimeOffset Instante(DateOnly dia, TimeZoneInfo fuso)
    {
        var local = dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, fuso.GetUtcOffset(local)).ToUniversalTime();
    }
}
