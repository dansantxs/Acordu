using Acordu.Application.Abstractions;
using Acordu.Application.Anexos;
using Acordu.Application.Common;
using Acordu.Application.Configuracoes;
using Acordu.Application.Portal.Extrato;
using Acordu.Domain.Acesso;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Portal;

/// <summary>
/// Leituras do portal com as regras de acesso do contato (contrato §12, segurança §3.2): permissão do papel Cliente,
/// escopo (PROPRIAS sem contratos e extrato) e o contato atual lido do banco.
/// </summary>
public sealed class LeituraPortal
{
    private readonly ITenantContext _contexto;
    private readonly IPortalAtual _portal;
    private readonly IConsultasPortal _consultas;
    private readonly IConsultaExtratoPortal _extrato;
    private readonly RelogioDoTenant _relogio;

    private readonly RecebimentoAnexos _anexos;
    private readonly LogotipoPortal _logotipo;

    public LeituraPortal(
        ITenantContext contexto,
        IPortalAtual portal,
        IConsultasPortal consultas,
        IConsultaExtratoPortal extrato,
        RelogioDoTenant relogio,
        RecebimentoAnexos anexos,
        LogotipoPortal logotipo)
    {
        ArgumentNullException.ThrowIfNull(logotipo);
        _logotipo = logotipo;
        ArgumentNullException.ThrowIfNull(anexos);
        _anexos = anexos;
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(portal);
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(extrato);
        ArgumentNullException.ThrowIfNull(relogio);
        _contexto = contexto;
        _portal = portal;
        _consultas = consultas;
        _extrato = extrato;
        _relogio = relogio;
    }

    public async Task<PortalEuDto> EuAsync(CancellationToken cancellationToken = default)
    {
        var contato = await _portal.ContatoAsync(cancellationToken);
        var eu = await _consultas.EuAsync(contato, cancellationToken) ?? throw new AcessoNegadoException("Acesso ao portal removido.");
        return eu with { Prestador = eu.Prestador with { LogoUrl = await _logotipo.UrlAsync(cancellationToken) } };
    }

    public async Task<ResultadoPaginado<SolicitacaoPortalResumoDto>> ListarAsync(
        IReadOnlyCollection<StatusExterno>? status, Paginacao paginacao, CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.PortalSolicitacaoVer);
        var contato = await _portal.ContatoAsync(cancellationToken);
        var pagina = await _consultas.ListarSolicitacoesAsync(contato, status, PodeAprovar, paginacao, cancellationToken);
        return PodeAprovar ? pagina : pagina with { Itens = [.. pagina.Itens.Select(s => s with { PodeAprovar = false })] };
    }

    public async Task<SolicitacaoPortalDto> DetalharAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.PortalSolicitacaoVer);
        var contato = await _portal.ContatoAsync(cancellationToken);
        var solicitacao = await _consultas.DetalharSolicitacaoAsync(contato, id, PodeAprovar, cancellationToken)
            ?? throw new NaoEncontradoException("Solicitação não encontrada.");
        return PodeAprovar ? solicitacao : solicitacao with { PodeAprovar = false };
    }

    public async Task<IReadOnlyList<InteracaoPortalDto>> InteracoesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.PortalSolicitacaoVer);
        var contato = await _portal.ContatoAsync(cancellationToken);
        return await _consultas.InteracoesAsync(contato, id, cancellationToken)
            ?? throw new NaoEncontradoException("Solicitação não encontrada.");
    }

    /// <summary>URL assinada de um anexo de interação EXTERNA de solicitação visível ao contato (✓L); senão 404.</summary>
    public async Task<Uri> UrlDoAnexoAsync(Guid anexoId, CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.PortalSolicitacaoVer);
        var contato = await _portal.ContatoAsync(cancellationToken);
        var anexo = await _consultas.AnexoAsync(contato, anexoId, cancellationToken)
            ?? throw new NaoEncontradoException("Anexo não encontrado.");
        return _anexos.UrlDeDownload(anexo);
    }

    public async Task<IReadOnlyList<ContratoPortalDto>> ContratosAsync(CancellationToken cancellationToken = default)
    {
        await ExigirContratosAsync(cancellationToken);
        return await _consultas.ContratosAsync(await _relogio.HojeAsync(cancellationToken), cancellationToken);
    }

    /// <summary>Extrato do período (padrão: mês corrente no fuso do tenant).</summary>
    public async Task<ExtratoPortalDto> ExtratoAsync(Guid contratoId, MesReferencia? periodo, CancellationToken cancellationToken = default)
    {
        await ExigirContratosAsync(cancellationToken);
        var hoje = await _relogio.HojeAsync(cancellationToken);
        var mes = periodo ?? MesReferencia.De(hoje);
        var saldo = await _consultas.SaldoAsync(contratoId, mes, hoje, cancellationToken);
        var extrato = saldo is null ? null : await _extrato.ExtratoAsync(contratoId, mes, cancellationToken);
        if (saldo is null || extrato is null)
            throw new NaoEncontradoException("Contrato não encontrado.");

        return new ExtratoPortalDto(
            new ContratoExtratoPortalDto(saldo.Tipo, saldo.FranquiaMinutos),
            mes,
            extrato.ConsumidoMinutos,
            saldo.SaldoMinutos,
            saldo.Percentual,
            [.. extrato.Linhas.Select(l => new ItemExtratoPortalDto(l.Data, l.SolicitacaoNumero, l.Titulo, l.Descricao, l.DuracaoMinutos))]);
    }

    private bool PodeAprovar => _contexto.Permissoes.Contains(Permissoes.PortalSolicitacaoAprovar);

    private async Task ExigirContratosAsync(CancellationToken cancellationToken)
    {
        _contexto.Exigir(Permissoes.PortalExtratoVer);
        var contato = await _portal.ContatoAsync(cancellationToken);
        if (!contato.VeContratos)
            throw new AcessoNegadoException("Contratos e extrato ficam disponíveis só para quem vê todas as solicitações do cliente.");
    }
}

/// <summary>Contato atual obrigatório: sem ele, o acesso foi removido no meio da sessão.</summary>
internal static class PortalAtualExtensions
{
    public static async Task<ContatoDoPortal> ContatoAsync(this IPortalAtual portal, CancellationToken cancellationToken) =>
        await portal.ObterAsync(cancellationToken) ?? throw new AcessoNegadoException("Acesso ao portal removido.");
}
