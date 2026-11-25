using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.Persistence.ConvitesEmpresa;

/// <summary>
/// Passo <c>expurgo-convites</c> do job diário (#213): apaga o convite de empresa <see cref="RetencaoDias"/> dias depois
/// de usado (<see cref="ConviteEmpresa.UsadoEm"/>), revogado (<see cref="ConviteEmpresa.RevogadoEm"/>) ou vencido
/// (<see cref="ConviteEmpresa.ExpiraEm"/> do último envio, sem uso nem revogação). O pendente dentro da validade nunca
/// sai; o vencido reenviado ganha validade nova e conta do novo vencimento. O e-mail de quem foi convidado é dado pessoal
/// e não fica sem prazo (segurança §8). A entrada da lista de espera de origem não muda: a chave estrangeira é do lado do
/// convite (F14-77) e as entradas convidadas ficam até o descarte. Tabela global, sem filtro de tenant: este namespace é
/// um dos que podem usar o tipo (teste de arquitetura, convenções §1).
/// </summary>
public sealed class ExpurgoConvitesEmpresa
{
    /// <summary>Dias depois do uso, da revogação ou do vencimento (constante, como os outros expurgos de retenção).</summary>
    public const int RetencaoDias = 90;

    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _relogio;

    public ExpurgoConvitesEmpresa(IServiceScopeFactory escopos, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(relogio);
        _escopos = escopos;
        _relogio = relogio;
    }

    /// <summary>Quantos convites apagou.</summary>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var limite = _relogio.GetUtcNow().AddDays(-RetencaoDias);
        await using var escopo = _escopos.CreateAsyncScope();
        // Um DELETE só, sem change tracker. Se a área reenviar o convite no mesmo instante, o Postgres (READ COMMITTED)
        // reavalia o WHERE na linha já atualizada, e o convite reenviado deixa de casar: fica, sem meio-termo.
        return await escopo.ServiceProvider.GetRequiredService<AcorduDbContext>().Set<ConviteEmpresa>()
            .Where(c => (c.UsadoEm != null && c.UsadoEm < limite)
                || (c.UsadoEm == null && c.RevogadoEm != null && c.RevogadoEm < limite)
                || (c.UsadoEm == null && c.RevogadoEm == null && c.ExpiraEm < limite))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
