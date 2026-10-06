using Acordu.Api.Seguranca;
using Acordu.Application.Abstractions;
using Acordu.Application.Categorias;
using Acordu.Application.Common;
using Acordu.Application.Configuracoes;
using Acordu.Domain.Acesso;

namespace Acordu.Api.Endpoints;

/// <summary>Calendário, configurações do portal e categorias (contrato §6).</summary>
internal static class ConfiguracoesEndpoints
{
    public static void MapConfiguracoes(this RouteGroupBuilder interno)
    {
        var configuracoes = interno.MapGroup("/configuracoes").WithTags("Configurações");

        // Leitura liberada à equipe: prazos e datas na UI dependem do fuso e do expediente.
        configuracoes.MapGet("/calendario", async (ICalendarios calendarios, CancellationToken cancellationToken) =>
            Results.Ok(ComandosConfiguracao.Calendario(await calendarios.DoTenantAsync(cancellationToken))))
            .Produces<CalendarioDto>();

        configuracoes.MapPut("/calendario", async (
            CalendarioRequest corpo, ComandosConfiguracao comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.AtualizarCalendarioAsync(corpo, cancellationToken)))
            .RequirePermissao(Permissoes.ConfiguracaoGerenciar)
            .Produces<CalendarioDto>();

        configuracoes.MapGet("/portal", async (ComandosConfiguracao comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.ObterPortalAsync(cancellationToken)))
            .RequirePermissao(Permissoes.ConfiguracaoGerenciar)
            .Produces<ConfiguracaoPortalDto>();

        configuracoes.MapPut("/portal", async (
            ConfiguracaoPortalRequest corpo, ComandosConfiguracao comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.AtualizarPortalAsync(corpo, cancellationToken)))
            .RequirePermissao(Permissoes.ConfiguracaoGerenciar)
            .Produces<ConfiguracaoPortalDto>();

        var categorias = interno.MapGroup("/categorias").WithTags("Configurações");

        // Leitura liberada à equipe: quem abre solicitação escolhe a categoria.
        categorias.MapGet(string.Empty, async (bool? ativa, IConsultasCategoria consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ListarAsync(ativa, cancellationToken)))
            .Produces<IReadOnlyList<CategoriaDto>>();

        categorias.MapGet("/{id:guid}", async (Guid id, IConsultasCategoria consultas, CancellationToken cancellationToken) =>
            Results.Ok(await DetalheAsync(consultas, id, cancellationToken)))
            .Produces<CategoriaDto>();

        categorias.MapPost(string.Empty, async (
            CategoriaRequest corpo, ComandosCategoria comandos, IConsultasCategoria consultas, CancellationToken cancellationToken) =>
        {
            var id = await comandos.CriarAsync(corpo, cancellationToken);
            return Results.Created($"/api/v1/categorias/{id}", await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.ConfiguracaoGerenciar)
            .Produces<CategoriaDto>(StatusCodes.Status201Created);

        categorias.MapPatch("/{id:guid}", async (
            Guid id, CategoriaRequest corpo, ComandosCategoria comandos, IConsultasCategoria consultas, CancellationToken cancellationToken) =>
        {
            await comandos.EditarAsync(id, corpo, cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.ConfiguracaoGerenciar)
            .Produces<CategoriaDto>();

        categorias.MapDelete("/{id:guid}", async (Guid id, ComandosCategoria comandos, CancellationToken cancellationToken) =>
        {
            await comandos.InativarAsync(id, cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.ConfiguracaoGerenciar);
    }

    private static async Task<CategoriaDto> DetalheAsync(IConsultasCategoria consultas, Guid id, CancellationToken cancellationToken) =>
        await consultas.DetalharAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Categoria não encontrada.");
}
