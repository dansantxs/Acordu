using Acordu.Api.Seguranca;
using Acordu.Application.Clientes;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;

namespace Acordu.Api.Endpoints;

/// <summary>Clientes e contatos (contrato §4.1–4.2). O acesso de contatos ao portal entra no marco do portal.</summary>
internal static class ClientesEndpoints
{
    public static void MapClientes(this RouteGroupBuilder interno)
    {
        var clientes = interno.MapGroup("/clientes").WithTags("Clientes");

        clientes.MapGet(string.Empty, async (
            string? busca, bool? ativo, int? pagina, int? tamanhoPagina, IConsultasCliente consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ListarAsync(busca, ativo, new Paginacao(pagina, tamanhoPagina), cancellationToken)))
            .RequirePermissao(Permissoes.ClienteVer)
            .Produces<ResultadoPaginado<ClienteResumoDto>>();

        clientes.MapGet("/{id:guid}", async (Guid id, IConsultasCliente consultas, CancellationToken cancellationToken) =>
            Results.Ok(await DetalheAsync(consultas, id, cancellationToken)))
            .RequirePermissao(Permissoes.ClienteVer)
            .Produces<ClienteDto>();

        clientes.MapPost(string.Empty, async (
            ClienteRequest corpo, ComandosCliente comandos, IConsultasCliente consultas, CancellationToken cancellationToken) =>
        {
            var id = await comandos.CriarAsync(corpo, cancellationToken);
            return Results.Created($"/api/v1/clientes/{id}", await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.ClienteGerenciar)
            .Produces<ClienteDto>(StatusCodes.Status201Created);

        clientes.MapPatch("/{id:guid}", async (
            Guid id, ClienteRequest corpo, HttpRequest requisicao, ComandosCliente comandos, IConsultasCliente consultas,
            CancellationToken cancellationToken) =>
        {
            await comandos.EditarAsync(id, corpo, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.ClienteGerenciar)
            .Produces<ClienteDto>();

        clientes.MapDelete("/{id:guid}", async (
            Guid id, HttpRequest requisicao, ComandosCliente comandos, CancellationToken cancellationToken) =>
        {
            await comandos.InativarAsync(id, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.ClienteGerenciar);

        clientes.MapPost("/{id:guid}/contatos", async (
            Guid id, ContatoRequest corpo, ComandosCliente comandos, IConsultasCliente consultas, CancellationToken cancellationToken) =>
        {
            var contatoId = await comandos.AdicionarContatoAsync(id, corpo, cancellationToken);
            var cliente = await DetalheAsync(consultas, id, cancellationToken);
            return Results.Created($"/api/v1/clientes/{id}", cliente.Contatos.Single(c => c.Id == contatoId));
        })
            .RequirePermissao(Permissoes.ClienteGerenciar)
            .Produces<ContatoDto>(StatusCodes.Status201Created);

        clientes.MapPatch("/{id:guid}/contatos/{contatoId:guid}", async (
            Guid id, Guid contatoId, ContatoRequest corpo, HttpRequest requisicao, ComandosCliente comandos,
            IConsultasCliente consultas, CancellationToken cancellationToken) =>
        {
            await comandos.EditarContatoAsync(id, contatoId, corpo, Versoes.IfMatch(requisicao), cancellationToken);
            var cliente = await DetalheAsync(consultas, id, cancellationToken);
            return Results.Ok(cliente.Contatos.Single(c => c.Id == contatoId));
        })
            .RequirePermissao(Permissoes.ClienteGerenciar)
            .Produces<ContatoDto>();

        clientes.MapDelete("/{id:guid}/contatos/{contatoId:guid}", async (
            Guid id, Guid contatoId, HttpRequest requisicao, ComandosCliente comandos, CancellationToken cancellationToken) =>
        {
            await comandos.InativarContatoAsync(id, contatoId, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.ClienteGerenciar);
    }

    private static async Task<ClienteDto> DetalheAsync(IConsultasCliente consultas, Guid id, CancellationToken cancellationToken) =>
        await consultas.DetalharAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Cliente não encontrado.");
}
