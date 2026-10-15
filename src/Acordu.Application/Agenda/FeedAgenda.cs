using System.Globalization;
using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Enums;
using Acordu.Domain.Services;

namespace Acordu.Application.Agenda;

/// <summary>
/// Feed ICS da agenda assinável de uma pessoa (contrato §11d, RF-COL-04): de 30 dias atrás a 90 dias à frente, só o que
/// é dela e do que ela pode ver. Tarefas em aberto no dia planejado (ou no prazo) e próximas ocorrências das recorrências
/// como dia inteiro; solicitações em aberto no instante do prazo de solução, com 30 minutos. No modo discreto, títulos
/// genéricos e sem descrição.
/// </summary>
public sealed class FeedAgenda
{
    public const int DiasAtras = 30;
    public const int DiasAFrente = 90;
    private static readonly TimeSpan DuracaoPrazo = TimeSpan.FromMinutes(30);

    private readonly IConsultasAgenda _consultas;
    private readonly IPessoasDaEquipe _pessoas;
    private readonly RelogioDoTenant _relogio;
    private readonly EnderecosPublicos _enderecos;

    public FeedAgenda(IConsultasAgenda consultas, IPessoasDaEquipe pessoas, RelogioDoTenant relogio, EnderecosPublicos enderecos)
    {
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(pessoas);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(enderecos);
        _consultas = consultas;
        _pessoas = pessoas;
        _relogio = relogio;
        _enderecos = enderecos;
    }

    /// <summary>O feed em texto; nulo se a pessoa não é mais da equipe do tenant do contexto.</summary>
    public async Task<string?> GerarAsync(Guid usuarioId, bool discreta, CancellationToken cancellationToken = default)
    {
        if (await _pessoas.ObterAsync(usuarioId, cancellationToken) is not { } pessoa)
            return null;

        var agora = _relogio.Agora;
        var hoje = await _relogio.HojeAsync(cancellationToken);
        var de = hoje.AddDays(-DiasAtras);
        var ate = hoje.AddDays(DiasAFrente);
        var filtro = new FiltroAgenda(null, usuarioId);
        var ics = new CalendarioIcs(discreta ? "Acordu" : $"Acordu · {pessoa.Nome}");

        if (await PodeAsync(usuarioId, Permissoes.TarefaVer, cancellationToken))
        {
            foreach (var t in await _consultas.TarefasAsync(de, ate, false, filtro, cancellationToken))
            {
                if (t.Status is not (StatusTarefa.AFazer or StatusTarefa.Fazendo))
                    continue;
                var dia = (t.PlanejadaPara ?? t.Prazo)!.Value;
                var titulo = discreta ? "Tarefa" : Com(t.Titulo, t.Cliente?.Nome);
                var descricao = discreta ? null : t.Prazo is { } prazo ? "Prazo: " + prazo.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : null;
                ics.DiaInteiro($"tarefa-{t.Id}@acordu", dia, titulo, descricao, _enderecos.TarefaInterna(t.Id), agora);
            }
        }

        if (await PodeAsync(usuarioId, Permissoes.SolicitacaoVer, cancellationToken))
        {
            var antes = agora.AddDays(DiasAFrente + 1);
            foreach (var s in await _consultas.SolicitacoesAsync(antes, filtro, cancellationToken))
            {
                if (s.PrazoResolucao < agora.AddDays(-DiasAtras))
                    continue;
                var titulo = discreta ? "Prazo de solicitação" : Com($"Prazo #{s.Numero} {s.Titulo}", s.Cliente.Nome);
                var descricao = discreta ? null : s.Pausada ? "Prazo pausado: aguardando o cliente ou terceiro." : null;
                ics.Instante(
                    $"solicitacao-{s.Id}@acordu", s.PrazoResolucao, DuracaoPrazo, titulo, descricao, _enderecos.SolicitacaoInterna(s.Id), agora);
            }
        }

        if (await PodeAsync(usuarioId, Permissoes.RecorrenciaVer, cancellationToken))
        {
            foreach (var r in await _consultas.RecorrenciasAsync(ate, null, cancellationToken))
            {
                if (r.Responsavel?.Id != usuarioId)
                    continue;
                var desde = r.ProximaData > hoje ? r.ProximaData : hoje;
                foreach (var data in GeradorDeDatas.Ocorrencias(r.Regra, desde, ate))
                {
                    var titulo = discreta ? "Tarefa recorrente" : Com(r.Titulo, r.Cliente.Nome) + " (prevista)";
                    ics.DiaInteiro(
                        $"recorrencia-{r.Id}-{data.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}@acordu", data, titulo, null, _enderecos.RecorrenciaInterna(r.Id), agora);
                }
            }
        }

        return ics.Fechar();
    }

    private static string Com(string titulo, string? cliente) => cliente is null ? titulo : $"{titulo} · {cliente}";

    private async Task<bool> PodeAsync(Guid usuarioId, string permissao, CancellationToken cancellationToken) =>
        (await _pessoas.ComPermissaoAsync(permissao, cancellationToken)).Any(p => p.UsuarioId == usuarioId);
}
