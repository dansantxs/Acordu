using Acordu.Application.EntradaEmail;
using Acordu.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.Persistence.EntradaEmail;

/// <summary>
/// Passo <c>expurgo-email-entrada</c> do job diário (spec do F15, F15-108; design D6 e D16; ADR 0016, item 11): apaga o
/// registro de entrada (<see cref="Domain.Entities.EmailRecebido"/>) com mais de <see cref="OpcoesEmailEntrada.DiasRegistro"/>
/// dias (padrão 7), de todos os tenants de uma vez, inclusive os só leitura. É expurgo de retenção de dado pessoal, como
/// <c>expurgo-auditoria</c> e <c>expurgo-notificacoes</c>, não operação da empresa: roda fora do <c>ExecutorPorTenant</c>.
/// <para>
/// Ponto revisado de <c>IgnoreQueryFilters</c> e de remoção por <c>ExecuteDelete</c>, fora das guardas de isolamento e de
/// só leitura (convenções §1; ADR 0015, itens 3 e 4): declara que toca só o <c>EmailRecebido</c> vencido. O expurgo da
/// triagem (F15k) entra neste mesmo passo.
/// </para>
/// </summary>
public sealed class ExpurgoEmailEntrada
{
    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _relogio;
    private readonly OpcoesEmailEntrada _opcoes;

    public ExpurgoEmailEntrada(IServiceScopeFactory escopos, TimeProvider relogio, OpcoesEmailEntrada opcoes)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(opcoes);
        _escopos = escopos;
        _relogio = relogio;
        _opcoes = opcoes;
    }

    /// <summary>Quantos registros de entrada apagou (<c>emailsRecebidosRemovidos</c> no diário).</summary>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var limite = _relogio.GetUtcNow().AddDays(-_opcoes.DiasRegistro);
        await using var escopo = _escopos.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<AcorduDbContext>().EmailsRecebidos
            .IgnoreQueryFilters([FiltrosGlobais.Tenant, FiltrosGlobais.Cliente])
            .Where(e => e.RecebidoEm < limite)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
