using Acordu.Api.Seguranca;
using Acordu.Application.Abstractions;
using Acordu.Application.Apontamentos;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;

namespace Acordu.Api.Endpoints;

/// <summary>Apontamentos e cronômetro do usuário (contrato §9).</summary>
internal static class ApontamentosEndpoints
{
    public static void MapApontamentos(this RouteGroupBuilder interno)
    {
        var apontamentos = interno.MapGroup("/apontamentos").WithTags("Apontamentos");

        apontamentos.MapGet(string.Empty, async (
            Guid? usuarioId, Guid? clienteId, Guid? contratoId, Guid? tarefaId, DateOnly? de, DateOnly? ate, bool? faturavel,
            int? pagina, int? tamanhoPagina, IConsultasApontamento consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ListarAsync(
                new FiltroApontamentos(usuarioId, clienteId, contratoId, tarefaId, de, ate, faturavel),
                new Paginacao(pagina, tamanhoPagina),
                cancellationToken)))
            .RequirePermissao(Permissoes.ApontamentoVer)
            .Produces<ResultadoPaginado<ApontamentoDto>>();

        apontamentos.MapGet("/{id:guid}", async (Guid id, IConsultasApontamento consultas, CancellationToken cancellationToken) =>
            Results.Ok(await DetalheAsync(consultas, id, cancellationToken)))
            .RequirePermissao(Permissoes.ApontamentoVer)
            .Produces<ApontamentoDto>();

        apontamentos.MapPost(string.Empty, async (
            NovoApontamentoRequest corpo, ComandosApontamento comandos, IConsultasApontamento consultas, CancellationToken cancellationToken) =>
        {
            var id = await comandos.RegistrarAsync(corpo, cancellationToken);
            return Results.Created($"/api/v1/apontamentos/{id}", await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.ApontamentoCriar)
            .Produces<ApontamentoDto>(StatusCodes.Status201Created);

        apontamentos.MapPatch("/{id:guid}", async (
            Guid id, EdicaoApontamentoRequest corpo, HttpRequest requisicao, ComandosApontamento comandos, IConsultasApontamento consultas,
            CancellationToken cancellationToken) =>
        {
            await comandos.EditarAsync(id, corpo, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.ApontamentoCriar)
            .Produces<ApontamentoDto>();

        apontamentos.MapDelete("/{id:guid}", async (
            Guid id, HttpRequest requisicao, ComandosApontamento comandos, CancellationToken cancellationToken) =>
        {
            await comandos.ExcluirAsync(id, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.ApontamentoCriar);

        var cronometro = interno.MapGroup("/cronometro").WithTags("Apontamentos");

        cronometro.MapGet(string.Empty, async (ITenantContext contexto, IConsultasApontamento consultas, CancellationToken cancellationToken) =>
            await consultas.CronometroAsync(contexto.Usuario(), cancellationToken) is { } ativo ? Results.Ok(ativo) : Results.NoContent())
            .RequirePermissao(Permissoes.ApontamentoCriar)
            .Produces<CronometroDto>();

        cronometro.MapPost("/inicio", async (
            InicioCronometroRequest corpo, ITenantContext contexto, ComandosApontamento comandos, IConsultasApontamento consultas,
            CancellationToken cancellationToken) =>
        {
            await comandos.IniciarCronometroAsync(corpo, cancellationToken);
            return Results.Created("/api/v1/cronometro", await consultas.CronometroAsync(contexto.Usuario(), cancellationToken));
        })
            .RequirePermissao(Permissoes.ApontamentoCriar)
            .Produces<CronometroDto>(StatusCodes.Status201Created);

        cronometro.MapPost("/parada", async (
            HttpRequest requisicao, ComandosApontamento comandos, IConsultasApontamento consultas, CancellationToken cancellationToken) =>
        {
            var corpo = await CorpoOpcional.LerAsync<ParadaCronometroRequest>(requisicao, cancellationToken);
            var id = await comandos.PararCronometroAsync(corpo, cancellationToken);
            return Results.Created($"/api/v1/apontamentos/{id}", await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.ApontamentoCriar)
            .Produces<ApontamentoDto>(StatusCodes.Status201Created);

        cronometro.MapDelete(string.Empty, async (ComandosApontamento comandos, CancellationToken cancellationToken) =>
        {
            await comandos.DescartarCronometroAsync(cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.ApontamentoCriar);
    }

    private static async Task<ApontamentoDto> DetalheAsync(IConsultasApontamento consultas, Guid id, CancellationToken cancellationToken) =>
        await consultas.DetalharAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Apontamento não encontrado.");
}
