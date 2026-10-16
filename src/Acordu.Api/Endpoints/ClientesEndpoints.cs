using Acordu.Api.Seguranca;
using Acordu.Application.Clientes;
using Acordu.Application.Common;
using Acordu.Application.AcessosPortal;
using Acordu.Domain.Acesso;

namespace Acordu.Api.Endpoints;

/// <summary>Clientes, contatos e o acesso dos contatos ao portal (contrato §4.1–4.2).</summary>
internal static class ClientesEndpoints
{
    public static void MapClientes(this RouteGroupBuilder interno)
    {
        var clientes = interno.MapGroup("/clientes").WithTags("Clientes");

        clientes.MapGet(string.Empty, async (
            string? busca, bool? ativo, string? etiqueta, Guid? responsavelId, int? pagina, int? tamanhoPagina,
            IConsultasCliente consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ListarAsync(
                busca, ativo, etiqueta, responsavelId, new Paginacao(pagina, tamanhoPagina), cancellationToken)))
            .RequirePermissao(Permissoes.ClienteVer)
            .Produces<ResultadoPaginado<ClienteResumoDto>>();

        // Etiquetas em uso (16/10): sugestões no formulário e opções do filtro da lista.
        clientes.MapGet("/etiquetas", async (IConsultasCliente consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.EtiquetasAsync(cancellationToken)))
            .RequirePermissao(Permissoes.ClienteVer)
            .Produces<IReadOnlyList<string>>();

        // Aba de arquivos (contrato §4.3): anexos de interações e de comentários de tarefa do cliente.
        clientes.MapGet("/{id:guid}/arquivos", async (
            Guid id, int? pagina, int? tamanhoPagina, LeituraArquivosCliente leitura, CancellationToken cancellationToken) =>
            Results.Ok(await leitura.ListarAsync(id, new Paginacao(pagina, tamanhoPagina), cancellationToken)))
            .RequireAlgumaPermissao(Permissoes.SolicitacaoVer, Permissoes.TarefaVer)
            .Produces<ResultadoPaginado<ArquivoClienteDto>>();

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

        // Reativação (16/10): POST de ação, como as transições da solicitação.
        clientes.MapPost("/{id:guid}/reativacao", async (
            Guid id, HttpRequest requisicao, ComandosCliente comandos, IConsultasCliente consultas, CancellationToken cancellationToken) =>
        {
            await comandos.ReativarAsync(id, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.ClienteGerenciar)
            .Produces<ClienteDto>();

        clientes.MapPost("/{id:guid}/contatos/{contatoId:guid}/reativacao", async (
            Guid id, Guid contatoId, HttpRequest requisicao, ComandosCliente comandos, IConsultasCliente consultas,
            CancellationToken cancellationToken) =>
        {
            await comandos.ReativarContatoAsync(id, contatoId, Versoes.IfMatch(requisicao), cancellationToken);
            var cliente = await DetalheAsync(consultas, id, cancellationToken);
            return Results.Ok(cliente.Contatos.Single(c => c.Id == contatoId));
        })
            .RequirePermissao(Permissoes.ClienteGerenciar)
            .Produces<ContatoDto>();

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

        MapAcessoPortal(clientes);
    }

    private static void MapAcessoPortal(RouteGroupBuilder clientes)
    {
        const string Rota = "/{id:guid}/contatos/{contatoId:guid}/acesso-portal";

        // 202: o convite sai por e-mail depois da resposta (outbox).
        clientes.MapPost(Rota, async (
            Guid id, Guid contatoId, AcessoPortalRequest corpo, ComandosAcessoPortal comandos, IConsultasCliente consultas,
            CancellationToken cancellationToken) =>
        {
            await comandos.ConvidarAsync(id, contatoId, corpo, cancellationToken);
            return Results.Accepted($"/api/v1/clientes/{id}", await ContatoAsync(consultas, id, contatoId, cancellationToken));
        })
            .RequirePermissao(Permissoes.PortalGerenciarAcessos)
            .Produces<ContatoDto>(StatusCodes.Status202Accepted);

        clientes.MapPatch(Rota, async (
            Guid id, Guid contatoId, AcessoPortalRequest corpo, ComandosAcessoPortal comandos, IConsultasCliente consultas,
            CancellationToken cancellationToken) =>
        {
            await comandos.AlterarEscopoAsync(id, contatoId, corpo, cancellationToken);
            return Results.Ok(await ContatoAsync(consultas, id, contatoId, cancellationToken));
        })
            .RequirePermissao(Permissoes.PortalGerenciarAcessos)
            .Produces<ContatoDto>();

        clientes.MapDelete(Rota, async (Guid id, Guid contatoId, ComandosAcessoPortal comandos, CancellationToken cancellationToken) =>
        {
            await comandos.RevogarAsync(id, contatoId, cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.PortalGerenciarAcessos);
    }

    private static async Task<ContatoDto> ContatoAsync(
        IConsultasCliente consultas, Guid id, Guid contatoId, CancellationToken cancellationToken) =>
        (await DetalheAsync(consultas, id, cancellationToken)).Contatos.Single(c => c.Id == contatoId);

    private static async Task<ClienteDto> DetalheAsync(IConsultasCliente consultas, Guid id, CancellationToken cancellationToken) =>
        await consultas.DetalharAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Cliente não encontrado.");
}
