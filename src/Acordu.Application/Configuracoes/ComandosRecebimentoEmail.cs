using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Common;
using Acordu.Application.EntradaEmail;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.Configuracoes;

/// <summary>
/// Liga e desliga o recebimento por e-mail da empresa (RF-SOL-11; spec do F15, F15-03 a F15-07, F15-93, F15-94, F15-96;
/// design D9 e §1.2; contrato §6). Ordem das conferências: permissão (<c>403</c>, F15-06) → corpo (<c>400</c> em
/// <c>ligado</c>) → só leitura (<c>422 empresa-somente-leitura</c>, F15-07, como os dados de exemplo) → regra
/// (<c>422 transicao-invalida</c>: sem domínio de entrada, F15-94; demonstração, F15-96). Sem <c>If-Match</c>, como os
/// outros <c>PUT</c> de configuração: o corpo diz o estado final, e repetir dá o mesmo resultado.
/// </summary>
public sealed class ComandosRecebimentoEmail
{
    public const string CampoLigado = "ligado";

    private readonly ITenantContext _contexto;
    private readonly ITenantAtual _tenant;
    private readonly OpcoesEmailEntrada _entrada;
    private readonly IAuditoria _auditoria;
    private readonly IUnitOfWork _unidade;

    public ComandosRecebimentoEmail(
        ITenantContext contexto, ITenantAtual tenant, OpcoesEmailEntrada entrada, IAuditoria auditoria, IUnitOfWork unidade)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(auditoria);
        ArgumentNullException.ThrowIfNull(unidade);
        _contexto = contexto;
        _tenant = tenant;
        _entrada = entrada;
        _auditoria = auditoria;
        _unidade = unidade;
    }

    /// <summary>
    /// Deixa o recebimento no estado pedido. Quando muda, grava com a trilha <c>configuracao.recebimento-email</c> (antes e
    /// depois, F15-93); pedir o estado em que já está responde o mesmo, sem gravar nem auditar.
    /// </summary>
    public async Task<RecebimentoEmailDto> AlterarAsync(RecebimentoEmailRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.ConfiguracaoGerenciar);
        if (dados.Ligado is not { } ligado)
            throw ErroDeCampo.Criar(CampoLigado, "Informe se o recebimento por e-mail fica ligado.");

        var tenant = await _tenant.ObterAsync(cancellationToken);
        if (tenant.SomenteLeitura)
            throw new EmpresaSomenteLeituraException();
        if (ligado && !_entrada.Disponivel)
            throw new TransicaoInvalidaException(Tenant.MensagemRecebimentoIndisponivel);

        var antes = tenant.RecebimentoEmail;
        if (ligado)
            tenant.LigarRecebimentoEmail();
        else
            tenant.DesligarRecebimentoEmail();

        if (tenant.RecebimentoEmail != antes)
        {
            _auditoria.Registrar(
                AcoesAuditoria.ConfiguracaoRecebimentoEmail,
                "empresa",
                tenant.Id,
                new { antes = new { recebimentoEmail = antes }, depois = new { recebimentoEmail = tenant.RecebimentoEmail } });
            await _unidade.SalvarAsync(cancellationToken);
        }

        return new RecebimentoEmailDto(_entrada.EnderecoDe(tenant), tenant.RecebimentoEmail);
    }
}
