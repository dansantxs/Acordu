using Acordu.Api.Seguranca;
using Acordu.Application.Abstractions;
using Acordu.Application.Categorias;
using Acordu.Application.Common;
using Acordu.Application.Configuracoes;
using Acordu.Application.Exemplos;
using Acordu.Domain.Acesso;

namespace Acordu.Api.Endpoints;

/// <summary>Calendário, empresa, configurações do portal, dados de exemplo e categorias (contrato §6).</summary>
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

        // Leitura liberada à equipe: o cadastro de contrato sugere o SLA da empresa.
        configuracoes.MapGet("/empresa", async (ComandosConfiguracao comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.ObterEmpresaAsync(cancellationToken)))
            .Produces<ConfiguracaoEmpresaDto>();

        configuracoes.MapPut("/empresa", async (
            ConfiguracaoEmpresaRequest corpo, ComandosConfiguracao comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.AtualizarEmpresaAsync(corpo, cancellationToken)))
            .RequirePermissao(Permissoes.ConfiguracaoGerenciar)
            .Produces<ConfiguracaoEmpresaDto>();

        // Recebimento por e-mail (F15-03 a F15-07, F15-93, F15-94, F15-96): ligar ou desligar; o endereço e o estado também
        // vêm no GET /configuracoes/empresa, liberado à equipe (F15-95).
        configuracoes.MapPut("/recebimento-email", async (
            RecebimentoEmailRequest corpo, ComandosRecebimentoEmail comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.AlterarAsync(corpo, cancellationToken)))
            .RequirePermissao(Permissoes.ConfiguracaoGerenciar)
            .Produces<RecebimentoEmailDto>();

        configuracoes.MapGet("/portal", async (ComandosConfiguracao comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.ObterPortalAsync(cancellationToken)))
            .RequirePermissao(Permissoes.ConfiguracaoGerenciar)
            .Produces<ConfiguracaoPortalDto>();

        configuracoes.MapPut("/portal", async (
            ConfiguracaoPortalRequest corpo, ComandosConfiguracao comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.AtualizarPortalAsync(corpo, cancellationToken)))
            .RequirePermissao(Permissoes.ConfiguracaoGerenciar)
            .Produces<ConfiguracaoPortalDto>();

        // Logotipo do portal (RF-POR-09): multipart com o campo "logo"; PNG, JPEG ou WebP até 512 KB.
        configuracoes.MapPut("/portal/logo", async (
            HttpRequest requisicao, LogotipoPortal logotipo, ComandosConfiguracao comandos, CancellationToken cancellationToken) =>
        {
            var formulario = await FormularioComAnexos.LerAsync(requisicao, "logo", 1024 * 1024, cancellationToken)
                ?? throw new BadHttpRequestException("Envie o logotipo em multipart/form-data, no campo logo.");
            await logotipo.EnviarAsync(formulario.Arquivos.Count > 0 ? formulario.Arquivos[0] : null, cancellationToken);
            return Results.Ok(await comandos.ObterPortalAsync(cancellationToken));
        })
            .RequirePermissao(Permissoes.ConfiguracaoGerenciar)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ConfiguracaoPortalDto>();

        configuracoes.MapDelete("/portal/logo", async (
            LogotipoPortal logotipo, ComandosConfiguracao comandos, CancellationToken cancellationToken) =>
        {
            await logotipo.RemoverAsync(cancellationToken);
            return Results.Ok(await comandos.ObterPortalAsync(cancellationToken));
        })
            .RequirePermissao(Permissoes.ConfiguracaoGerenciar)
            .Produces<ConfiguracaoPortalDto>();

        // Dados de exemplo (RF-EMP-05, F14r): a oferta da Hoje, carregar e apagar. Só o Dono (configuracao.gerenciar).
        configuracoes.MapGet("/exemplos", async (ComandosExemplos comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.ObterAsync(cancellationToken)))
            .RequirePermissao(Permissoes.ConfiguracaoGerenciar)
            .Produces<DadosExemploDto>();

        configuracoes.MapPost("/exemplos", async (ComandosExemplos comandos, CancellationToken cancellationToken) =>
        {
            await comandos.CarregarAsync(cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.ConfiguracaoGerenciar)
            .Produces(StatusCodes.Status204NoContent);

        configuracoes.MapDelete("/exemplos", async (ComandosExemplos comandos, CancellationToken cancellationToken) =>
        {
            await comandos.ApagarAsync(cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.ConfiguracaoGerenciar)
            .Produces(StatusCodes.Status204NoContent);

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
