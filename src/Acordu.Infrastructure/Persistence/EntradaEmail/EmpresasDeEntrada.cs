using Acordu.Application.EntradaEmail;
using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.EntradaEmail;

/// <summary>
/// Destino → empresa (design do F15, D14; ADR 0016, item 3): o código de entrada procurado entre todas as empresas, sem
/// tenant no contexto. Ponto revisado de <c>IgnoreQueryFilters</c> (convenções §1): só o filtro de tenant do
/// <see cref="Domain.Entities.Tenant"/>, só leitura (sem rastrear) e só o Id, a situação e a opção de recebimento; nada
/// da empresa sai daqui. O processamento continua num escopo com o tenant fixado, sob os filtros e as guardas.
/// </summary>
public sealed class EmpresasDeEntrada : IEmpresasDeEntrada
{
    private readonly AcorduDbContext _db;

    public EmpresasDeEntrada(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<EmpresaDeEntrada?> PorCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        // Fora do formato não é código de empresa nenhuma: nem vai ao banco.
        if (!CodigoDeEntrada.Valido(codigo))
            return null;

        return await _db.Tenants
            .IgnoreQueryFilters([FiltrosGlobais.Tenant])
            .AsNoTracking()
            .Where(t => t.CodigoEntrada == codigo)
            .Select(t => new EmpresaDeEntrada(
                t.Id, SituacaoEmpresaExtensions.SituacoesSomenteLeitura.Contains(t.Situacao), t.RecebimentoEmail))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
