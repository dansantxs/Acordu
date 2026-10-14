using System.Globalization;
using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Application.Solicitacoes;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.Notificacoes;

/// <summary>
/// Alertas de SLA em risco e violado (RF-NOT-02), no tenant do contexto; rodado pelo job <c>tick</c>. Cada alerta sai uma
/// vez por (solicitação, ciclo do SLA, situação): o marcador é gravado antes do envio e desfeito se o envio falhar.
/// Reabrir inicia novo ciclo, então os alertas valem de novo. Destinatários: responsável ou, sem ele, quem tem
/// <c>solicitacao.editar</c>.
/// </summary>
public sealed class AlertasSla
{
    private readonly IConsultasSolicitacao _consultas;
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly IPessoasDaEquipe _pessoas;
    private readonly IMarcadoresDeAviso _marcadores;
    private readonly ICalendarios _calendarios;
    private readonly AvisoInterno _aviso;
    private readonly EnderecosPublicos _enderecos;
    private readonly RelogioDoTenant _relogio;

    public AlertasSla(
        IConsultasSolicitacao consultas,
        IRepositorio<Solicitacao> solicitacoes,
        IPessoasDaEquipe pessoas,
        IMarcadoresDeAviso marcadores,
        ICalendarios calendarios,
        AvisoInterno aviso,
        EnderecosPublicos enderecos,
        RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(pessoas);
        ArgumentNullException.ThrowIfNull(marcadores);
        ArgumentNullException.ThrowIfNull(calendarios);
        ArgumentNullException.ThrowIfNull(aviso);
        ArgumentNullException.ThrowIfNull(enderecos);
        ArgumentNullException.ThrowIfNull(relogio);
        _consultas = consultas;
        _solicitacoes = solicitacoes;
        _pessoas = pessoas;
        _marcadores = marcadores;
        _calendarios = calendarios;
        _aviso = aviso;
        _enderecos = enderecos;
        _relogio = relogio;
    }

    /// <summary>Envia os alertas novos e devolve quantas solicitações foram alertadas.</summary>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var agora = _relogio.Agora;
        var candidatas = await _consultas.CandidatasAAlertaDeSlaAsync(agora, cancellationToken);
        if (candidatas.Count == 0)
            return 0;

        var calendario = await _calendarios.DoTenantAsync(cancellationToken);
        var alertadas = 0;
        foreach (var id in candidatas)
        {
            var solicitacao = await _solicitacoes.ObterPorIdAsync(id, cancellationToken);
            if (solicitacao?.Sla is not { } sla)
                continue;
            var situacao = solicitacao.SituacaoSla(agora, calendario);
            if (situacao is not (SituacaoSla.EmRisco or SituacaoSla.Violado))
                continue;

            var chave = $"sla:{sla.InicioCiclo.UtcTicks.ToString(CultureInfo.InvariantCulture)}:{situacao}";
            if (!await _marcadores.MarcarAsync(solicitacao.Id, chave, cancellationToken))
                continue;
            try
            {
                await EnviarAsync(solicitacao, situacao, sla.PrazoResolucao, calendario, $"{chave}:{solicitacao.Id}", cancellationToken);
            }
            catch
            {
                await _marcadores.DesmarcarAsync(solicitacao.Id, chave, CancellationToken.None);
                throw;
            }

            alertadas++;
        }

        return alertadas;
    }

    private async Task EnviarAsync(
        Solicitacao solicitacao, SituacaoSla situacao, DateTimeOffset prazo, CalendarioUtil calendario, string chave,
        CancellationToken cancellationToken)
    {
        var prazoLocal = TimeZoneInfo.ConvertTime(prazo, calendario.Fuso).ToString("dd/MM/yyyy 'às' HH:mm", CultureInfo.InvariantCulture);
        var violado = situacao == SituacaoSla.Violado;
        var titulo = violado
            ? $"SLA violado: solicitação #{solicitacao.Numero}"
            : $"SLA em risco: solicitação #{solicitacao.Numero}";
        foreach (var pessoa in await AvisosAEquipe.DestinatariosAsync(_pessoas, solicitacao, cancellationToken))
        {
            await _aviso.AvisarAsync(
                pessoa,
                CategoriaAviso.Sla,
                chave,
                titulo,
                _enderecos.SolicitacaoInterna(solicitacao.Id),
                new MensagemEmail(
                    pessoa.Email,
                    violado
                        ? $"SLA violado: solicitação #{solicitacao.Numero}"
                        : $"SLA em risco: solicitação #{solicitacao.Numero}",
                    $"""
                    Olá, {pessoa.Nome}!

                    A solicitação #{solicitacao.Numero} — {solicitacao.Titulo} {(violado ? "passou do prazo de resolução" : "está perto do prazo de resolução")} ({prazoLocal}).

                    {_enderecos.SolicitacaoInterna(solicitacao.Id)}
                    """,
                    violado ? "aviso-sla-violado" : "aviso-sla-risco"),
                cancellationToken);
        }
    }
}
