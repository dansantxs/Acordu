using System.Globalization;
using Acordu.Api.Seguranca;
using Acordu.Application.Common;
using Acordu.Application.Contratos;
using Acordu.Domain.Acesso;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Api.Endpoints;

/// <summary>Contratos, ajustes de saldo, consumo e fechamento de período (contrato §5).</summary>
internal static class ContratosEndpoints
{
    public static void MapContratos(this RouteGroupBuilder interno)
    {
        var contratos = interno.MapGroup("/contratos").WithTags("Contratos");

        contratos.MapGet(string.Empty, async (
            Guid? clienteId, string? status, int? pagina, int? tamanhoPagina, IConsultasContrato consultas,
            CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ListarAsync(
                clienteId, EnumsDaApi.Ler<StatusContrato>(status, nameof(status)), new Paginacao(pagina, tamanhoPagina), cancellationToken)))
            .RequirePermissao(Permissoes.ContratoVer)
            .Produces<ResultadoPaginado<ContratoResumoDto>>();

        contratos.MapGet("/{id:guid}", async (
            Guid id, IConsultasContrato consultas, RelogioDoTenant relogio, CancellationToken cancellationToken) =>
            Results.Ok(await DetalheAsync(consultas, relogio, id, cancellationToken)))
            .RequirePermissao(Permissoes.ContratoVer)
            .Produces<ContratoDto>();

        contratos.MapPost(string.Empty, async (
            NovoContratoRequest corpo, ComandosContrato comandos, IConsultasContrato consultas, RelogioDoTenant relogio,
            CancellationToken cancellationToken) =>
        {
            var id = await comandos.CriarAsync(corpo, cancellationToken);
            return Results.Created($"/api/v1/contratos/{id}", await DetalheAsync(consultas, relogio, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.ContratoGerenciar)
            .Produces<ContratoDto>(StatusCodes.Status201Created);

        contratos.MapPatch("/{id:guid}", async (
            Guid id, EdicaoContratoRequest corpo, HttpRequest requisicao, ComandosContrato comandos, IConsultasContrato consultas,
            RelogioDoTenant relogio, CancellationToken cancellationToken) =>
        {
            await comandos.EditarAsync(id, corpo, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, relogio, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.ContratoGerenciar)
            .Produces<ContratoDto>();

        MapTransicao(contratos, "/suspensao", (c, id, versao, _, ct) => c.SuspenderAsync(id, versao, ct));
        MapTransicao(contratos, "/reativacao", (c, id, versao, _, ct) => c.ReativarAsync(id, versao, ct));
        MapTransicao(contratos, "/encerramento", async (c, id, versao, requisicao, ct) =>
        {
            var corpo = await CorpoOpcional.LerAsync<EncerramentoRequest>(requisicao, ct);
            await c.EncerrarAsync(id, corpo?.Data, versao, ct);
        });

        contratos.MapPost("/{id:guid}/ajustes", async (
            Guid id, AjusteSaldoRequest corpo, ComandosContrato comandos, CancellationToken cancellationToken) =>
        {
            var ajuste = await comandos.AjustarSaldoAsync(id, corpo, cancellationToken);
            return Results.Created($"/api/v1/contratos/{id}", ajuste);
        })
            .RequirePermissao(Permissoes.ContratoAjustarSaldo)
            .Produces<AjusteSaldoDto>(StatusCodes.Status201Created);

        contratos.MapGet("/{id:guid}/consumo", async (
            Guid id, string? periodo, IConsultasContrato consultas, RelogioDoTenant relogio, CancellationToken cancellationToken) =>
        {
            var hoje = await relogio.HojeAsync(cancellationToken);
            var mes = periodo is null ? MesReferencia.De(hoje) : Mes(periodo);
            return Results.Ok(await consultas.ConsumoAsync(id, mes, hoje, cancellationToken)
                ?? throw new NaoEncontradoException("Contrato não encontrado."));
        })
            .RequirePermissao(Permissoes.ContratoVer)
            .Produces<ConsumoContratoDto>();

        contratos.MapPost("/{id:guid}/periodos/{periodo}/fechamento", async (
            Guid id, string periodo, ComandosContrato comandos, CancellationToken cancellationToken) =>
        {
            await comandos.FecharPeriodoAsync(id, Mes(periodo), cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.ContratoFecharPeriodo);
    }

    /// <summary>Ação de status (convenções §4): <c>If-Match</c> opcional, devolve o contrato atualizado.</summary>
    private static void MapTransicao(
        RouteGroupBuilder contratos,
        string acao,
        Func<ComandosContrato, Guid, string?, HttpRequest, CancellationToken, Task> executar) =>
        contratos.MapPost("/{id:guid}" + acao, async (
            Guid id, HttpRequest requisicao, ComandosContrato comandos, IConsultasContrato consultas,
            RelogioDoTenant relogio, CancellationToken cancellationToken) =>
        {
            await executar(comandos, id, Versoes.IfMatch(requisicao), requisicao, cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, relogio, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.ContratoGerenciar)
            .Produces<ContratoDto>();

    private static MesReferencia Mes(string periodo) =>
        DateOnly.TryParseExact(periodo + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia)
            ? MesReferencia.De(dia)
            : throw new BadHttpRequestException("Período no formato yyyy-MM.");

    private static async Task<ContratoDto> DetalheAsync(
        IConsultasContrato consultas, RelogioDoTenant relogio, Guid id, CancellationToken cancellationToken) =>
        await consultas.DetalharAsync(id, await relogio.HojeAsync(cancellationToken), cancellationToken)
            ?? throw new NaoEncontradoException("Contrato não encontrado.");
}
