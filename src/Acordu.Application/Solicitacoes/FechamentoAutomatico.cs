using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.Solicitacoes;

/// <summary>
/// Fechamento automático (RF-SOL-09), no tenant do contexto: RESOLVIDA sem aprovação nem reprovação do cliente há
/// <see cref="Tenant.DiasFechamentoAutomatico"/> dias corridos (desde <c>ResolvidaEm</c>) vai para FECHADA. Agido pelo
/// sistema (job), sem usuário. Idempotente: rodar de novo não acha mais nada.
/// </summary>
public sealed class FechamentoAutomatico
{
    private readonly ITenantAtual _tenant;
    private readonly IConsultasSolicitacao _consultas;
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly RelogioDoTenant _relogio;
    private readonly IUnitOfWork _unidade;

    public FechamentoAutomatico(
        ITenantAtual tenant,
        IConsultasSolicitacao consultas,
        IRepositorio<Solicitacao> solicitacoes,
        RelogioDoTenant relogio,
        IUnitOfWork unidade)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        _tenant = tenant;
        _consultas = consultas;
        _solicitacoes = solicitacoes;
        _relogio = relogio;
        _unidade = unidade;
    }

    /// <summary>Fecha as vencidas numa unidade de trabalho e devolve quantas fechou.</summary>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await _tenant.ObterAsync(cancellationToken);
        var agora = _relogio.Agora;
        var limite = agora - TimeSpan.FromDays(tenant.DiasFechamentoAutomatico);

        var fechadas = 0;
        foreach (var id in await _consultas.ResolvidasAteAsync(limite, cancellationToken))
        {
            var solicitacao = await _solicitacoes.ObterPorIdAsync(id, cancellationToken);
            if (solicitacao?.Status != StatusSolicitacao.Resolvida)
                continue;
            solicitacao.Fechar(agora);
            fechadas++;
        }

        if (fechadas > 0)
            await _unidade.SalvarAsync(cancellationToken);
        return fechadas;
    }
}
