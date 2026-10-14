using Acordu.Api.Seguranca;
using Acordu.Application.Dashboard;
using Acordu.Domain.Acesso;

namespace Acordu.Api.Endpoints;

/// <summary>Tela inicial (contrato §11). Blocos sem permissão de leitura da área vêm nulos.</summary>
internal static class DashboardEndpoints
{
    public static void MapDashboard(this RouteGroupBuilder interno) =>
        interno.MapGet("/dashboard", async (DateOnly? de, DateOnly? ate, LeituraDashboard leitura, CancellationToken cancellationToken) =>
            Results.Ok(await leitura.ObterAsync(de, ate, cancellationToken)))
            .WithTags("Dashboard")
            .RequirePermissao(Permissoes.DashboardVer)
            .Produces<DashboardDto>();
}
