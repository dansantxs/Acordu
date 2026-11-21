using System.Globalization;
using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Application.Nebrys;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence.Auditoria;
using Acordu.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.Persistence.Nebrys;

/// <summary>
/// Mudança de situação de uma empresa pela área da Nebrys (ADR 0014, item 5; ADR 0015, item 2). A sessão de operação não
/// tem tenant, e o escopo da requisição continua vazio: a ação roda num escopo próprio com o tenant da empresa fixado,
/// como o <c>ExecutorPorTenant</c> (o sistema agindo, com as guardas e os filtros valendo), marcado para a mudança de
/// situação (<see cref="AcorduDbContext.PermitirMudancaDeSituacao"/>). Só o <see cref="Tenant"/> e a linha da trilha, sem
/// tenant e com o operador, são gravados, num SaveChanges só (mesma transação). Nenhum dado de negócio é lido: o fuso da
/// empresa vem do calendário dela.
/// </summary>
public sealed class SituacaoDasEmpresas : ISituacaoDasEmpresas
{
    private const string AlvoEmpresa = "empresa";
    private const string EmpresaNaoEncontrada = "Empresa não encontrada.";

    private readonly IServiceScopeFactory _escopos;
    private readonly IOperadorAtual _operador;
    private readonly IOrigemRequisicao _origem;
    private readonly TimeProvider _relogio;

    public SituacaoDasEmpresas(
        IServiceScopeFactory escopos, IOperadorAtual operador, IOrigemRequisicao origem, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(operador);
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(relogio);
        _escopos = escopos;
        _operador = operador;
        _origem = origem;
        _relogio = relogio;
    }

    public async Task AlterarAsync(
        Guid empresaId,
        string? versaoEsperada,
        Func<Tenant, TimeZoneInfo, AcaoNaTrilha> alterar,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alterar);
        var esperada = Versao(versaoEsperada);
        var operador = _operador.Id;
        if (empresaId == Guid.Empty)
            throw new NaoEncontradoException(EmpresaNaoEncontrada);

        await using var escopo = _escopos.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<ContextoTenant>().DefinirEquipe(empresaId);
        var db = escopo.ServiceProvider.GetRequiredService<AcorduDbContext>();
        db.PermitirMudancaDeSituacao();

        // Empresa expurgada não existe mais (F14-106): 404, como a inexistente.
        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Id == empresaId, cancellationToken)
            ?? throw new NaoEncontradoException(EmpresaNaoEncontrada);

        // A versão lida no detalhe antes de qualquer regra (D5, F14-107): se o diário ou outra ação mudou a empresa, a Nebrys
        // vê o estado novo e decide de novo, mesmo quando a transição ainda valeria (ou já não valeria) nele. Uma gravação
        // entre esta leitura e o UPDATE cai no xmin do próprio UPDATE (token de concorrência).
        if (esperada is { } versao && db.Entry(tenant).Property<uint>(ConfiguracaoRaiz.Versao).CurrentValue != versao)
            throw new DbUpdateConcurrencyException("A empresa mudou desde a leitura. Recarregue e tente de novo.");

        var calendario = await db.Calendarios.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var fuso = calendario?.Fuso ?? TimeZoneInfo.FindSystemTimeZoneById(CalendarioUtil.FusoPadrao);
        var acao = alterar(tenant, fuso);

        db.Add(TrilhaDaOperacao.Registro(operador, acao.Acao, AlvoEmpresa, empresaId, acao.Dados, _origem.Ip, _relogio.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static uint? Versao(string? versaoEsperada)
    {
        if (versaoEsperada is null)
            return null;
        return uint.TryParse(versaoEsperada, NumberStyles.None, CultureInfo.InvariantCulture, out var versao)
            ? versao
            : throw new ArgumentException("If-Match deve trazer a versão recebida no recurso.", nameof(versaoEsperada));
    }
}
