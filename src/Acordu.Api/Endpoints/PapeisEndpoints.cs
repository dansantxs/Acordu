using Acordu.Api.Seguranca;
using Acordu.Application.Common;
using Acordu.Application.Papeis;
using Acordu.Domain.Acesso;

namespace Acordu.Api.Endpoints;

/// <summary>Papéis do tenant (contrato §3): padrões imutáveis e papéis sob medida, todos com <c>papel.gerenciar</c>.</summary>
internal static class PapeisEndpoints
{
    public static void MapPapeis(this RouteGroupBuilder interno)
    {
        var papeis = interno.MapGroup("/papeis").WithTags("Equipe").RequirePermissao(Permissoes.PapelGerenciar);

        papeis.MapGet(string.Empty, async (IConsultasPapel consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ListarAsync(cancellationToken)))
            .Produces<IReadOnlyList<PapelDto>>();

        papeis.MapGet("/permissoes", () => Results.Ok(ComandosPapel.Catalogo))
            .Produces<IReadOnlyList<string>>();

        papeis.MapPost(string.Empty, async (
            PapelRequest corpo, ComandosPapel comandos, IConsultasPapel consultas, CancellationToken cancellationToken) =>
        {
            var id = await comandos.CriarAsync(corpo, cancellationToken);
            return Results.Created($"/api/v1/papeis/{id}", await DetalheAsync(consultas, id, cancellationToken));
        })
            .Produces<PapelDto>(StatusCodes.Status201Created);

        papeis.MapPatch("/{id:guid}", async (
            Guid id, PapelRequest corpo, ComandosPapel comandos, IConsultasPapel consultas, CancellationToken cancellationToken) =>
        {
            await comandos.EditarAsync(id, corpo, cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .Produces<PapelDto>();

        papeis.MapDelete("/{id:guid}", async (Guid id, ComandosPapel comandos, CancellationToken cancellationToken) =>
        {
            await comandos.ExcluirAsync(id, cancellationToken);
            return Results.NoContent();
        });
    }

    private static async Task<PapelDto> DetalheAsync(IConsultasPapel consultas, Guid id, CancellationToken cancellationToken) =>
        await consultas.DetalharAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Papel não encontrado.");
}
