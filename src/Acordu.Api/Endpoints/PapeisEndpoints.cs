using Acordu.Api.Seguranca;
using Acordu.Application.Common;
using Acordu.Application.Papeis;
using Acordu.Domain.Acesso;

namespace Acordu.Api.Endpoints;

/// <summary>
/// Papéis do tenant (contrato §3): padrões imutáveis e papéis sob medida, com <c>papel.gerenciar</c>. A lista também abre
/// com <c>usuario.convidar</c>: o convite exige escolher o papel (22/10, F5c).
/// </summary>
internal static class PapeisEndpoints
{
    public static void MapPapeis(this RouteGroupBuilder interno)
    {
        var papeis = interno.MapGroup("/papeis").WithTags("Equipe");

        papeis.MapGet(string.Empty, async (IConsultasPapel consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ListarAsync(cancellationToken)))
            .RequireAlgumaPermissao(Permissoes.PapelGerenciar, Permissoes.UsuarioConvidar)
            .Produces<IReadOnlyList<PapelDto>>();

        papeis.MapGet("/permissoes", () => Results.Ok(ComandosPapel.Catalogo))
            .RequirePermissao(Permissoes.PapelGerenciar)
            .Produces<IReadOnlyList<string>>();

        papeis.MapPost(string.Empty, async (
            PapelRequest corpo, ComandosPapel comandos, IConsultasPapel consultas, CancellationToken cancellationToken) =>
        {
            var id = await comandos.CriarAsync(corpo, cancellationToken);
            return Results.Created($"/api/v1/papeis/{id}", await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.PapelGerenciar)
            .Produces<PapelDto>(StatusCodes.Status201Created);

        papeis.MapPatch("/{id:guid}", async (
            Guid id, PapelRequest corpo, ComandosPapel comandos, IConsultasPapel consultas, CancellationToken cancellationToken) =>
        {
            await comandos.EditarAsync(id, corpo, cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.PapelGerenciar)
            .Produces<PapelDto>();

        papeis.MapDelete("/{id:guid}", async (Guid id, ComandosPapel comandos, CancellationToken cancellationToken) =>
        {
            await comandos.ExcluirAsync(id, cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.PapelGerenciar);
    }

    private static async Task<PapelDto> DetalheAsync(IConsultasPapel consultas, Guid id, CancellationToken cancellationToken) =>
        await consultas.DetalharAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Papel não encontrado.");
}
