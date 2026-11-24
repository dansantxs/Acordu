using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Cadastro;
using Acordu.Application.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.Nebrys;

/// <summary>
/// Convites de empresa pela área da Nebrys (RF-AUT-08; F14-72 a F14-74; decisões 4 e 7 do G2 do F14; design D12): emitir
/// para um e-mail, reenviar e revogar. O link (<c>{App__UrlPublica}/cadastro/convite/{token}</c>) vale
/// <c>Cadastro__DiasConvite</c> dias (7) a partir do envio, para um cadastro só e com o e-mail do convite. O e-mail sai na
/// própria requisição, depois do commit, como os da conta: não há tenant para a outbox, e o token não pode ficar em claro
/// em lugar nenhum. A falha no envio não desfaz a ação e volta como <c>emailEnviado: false</c> (a Nebrys reenvia). Toda
/// ação vai para a trilha sem tenant, com o operador e o e-mail afetado (D14), nunca o token.
/// </summary>
public sealed class ConvitesEmpresaNebrys
{
    /// <summary>Campo do e-mail no corpo da emissão.</summary>
    public const string CampoEmail = "email";

    /// <summary>Categoria do e-mail do convite (log do envio, sem conteúdo).</summary>
    public const string CategoriaEmail = "convite-empresa";

    private const string AlvoConvite = "convite";

    private readonly IConvitesEmpresa _convites;
    private readonly ITrilhaDaOperacao _trilha;
    private readonly IUnitOfWork _unidade;
    private readonly TimeProvider _relogio;
    private readonly IEnviadorEmail _email;
    private readonly EnderecosPublicos _enderecos;
    private readonly OpcoesCadastro _cadastro;

    public ConvitesEmpresaNebrys(
        IConvitesEmpresa convites,
        ITrilhaDaOperacao trilha,
        IUnitOfWork unidade,
        TimeProvider relogio,
        IEnviadorEmail email,
        EnderecosPublicos enderecos,
        OpcoesCadastro cadastro)
    {
        ArgumentNullException.ThrowIfNull(convites);
        ArgumentNullException.ThrowIfNull(trilha);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(enderecos);
        ArgumentNullException.ThrowIfNull(cadastro);
        _convites = convites;
        _trilha = trilha;
        _unidade = unidade;
        _relogio = relogio;
        _email = email;
        _enderecos = enderecos;
        _cadastro = cadastro;
    }

    /// <summary>
    /// Emite o convite e envia o link (F14-72): o convite fica pendente, com a validade. E-mail vazio ou inválido →
    /// <c>400</c> no campo <c>email</c>; e-mail que já tem um convite em aberto (nem usado nem revogado, vencido ou não) →
    /// <c>422 transicao-invalida</c>: a Nebrys reenvia ou revoga o que existe.
    /// </summary>
    public async Task<ConviteEnviado> EmitirAsync(ConviteEmpresaRequest corpo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(corpo);
        var agora = _relogio.GetUtcNow();
        var (convite, token) = ConviteEmpresa.Emitir(Guid.CreateVersion7(), corpo.Email ?? string.Empty, _cadastro.ValidadeConvite, agora);
        if (await _convites.HaEmAbertoAsync(convite.Email, cancellationToken))
        {
            throw new TransicaoInvalidaException(
                "Este e-mail já tem um convite em aberto: reenvie o convite que existe ou revogue antes de emitir outro.");
        }

        _convites.Adicionar(convite);
        _trilha.Registrar(
            AcoesAuditoria.NebrysConviteEmissao, AlvoConvite, convite.Id, new { convite.Email, depois = Estado(convite, agora) });
        await _unidade.SalvarAsync(cancellationToken);
        return new ConviteEnviado(convite.Id, await EnviarAsync(convite, token, cancellationToken));
    }

    /// <summary>
    /// Reenvia (F14-73): link novo, validade reiniciada, e o anterior para de valer. Vale para o convite pendente ou vencido;
    /// usado ou revogado → <c>422 transicao-invalida</c>; inexistente → <c>404</c>.
    /// </summary>
    public async Task<ConviteEnviado> ReenviarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var convite = await CarregarAsync(id, cancellationToken);
        var agora = _relogio.GetUtcNow();
        var antes = Estado(convite, agora);
        var token = convite.Reenviar(_cadastro.ValidadeConvite, agora);
        _trilha.Registrar(
            AcoesAuditoria.NebrysConviteReenvio, AlvoConvite, convite.Id, new { convite.Email, antes, depois = Estado(convite, agora) });
        await _unidade.SalvarAsync(cancellationToken);
        return new ConviteEnviado(convite.Id, await EnviarAsync(convite, token, cancellationToken));
    }

    /// <summary>
    /// Revoga (F14-74): o link para de valer, e o cadastro com ele é recusado (F14-45). Usado ou já revogado →
    /// <c>422 transicao-invalida</c>; inexistente → <c>404</c>.
    /// </summary>
    public async Task RevogarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var convite = await CarregarAsync(id, cancellationToken);
        var agora = _relogio.GetUtcNow();
        var antes = Estado(convite, agora);
        convite.Revogar(agora);
        _trilha.Registrar(
            AcoesAuditoria.NebrysConviteRevogacao, AlvoConvite, convite.Id, new { convite.Email, antes, depois = Estado(convite, agora) });
        await _unidade.SalvarAsync(cancellationToken);
    }

    private async Task<ConviteEmpresa> CarregarAsync(Guid id, CancellationToken cancellationToken) =>
        await _convites.ObterAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Convite não encontrado.");

    /// <summary>O que vai para a trilha de cada lado da ação: a situação e a validade (nunca o token nem o hash).</summary>
    private static object Estado(ConviteEmpresa convite, DateTimeOffset agora) =>
        new { situacao = convite.SituacaoEm(agora), convite.ExpiraEm };

    private async Task<bool> EnviarAsync(ConviteEmpresa convite, string token, CancellationToken cancellationToken)
    {
        var mensagem = new MensagemEmail(
            convite.Email,
            "Seu convite para o Acordu",
            $"""
            Olá!

            A Nebrys convidou você para cadastrar a sua empresa no Acordu e começar o período de teste.

            Para se cadastrar, acesse o link abaixo (válido por {_cadastro.DiasConvite} dias). O cadastro é feito com este
            e-mail ({convite.Email}) e o link vale para um cadastro só:
            {_enderecos.ConviteEmpresa(token)}

            Se você não esperava este convite, pode ignorar este e-mail.
            """,
            CategoriaEmail);
        try
        {
            await _email.EnviarAsync(mensagem, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }
}

/// <summary>Corpo do <c>POST /nebrys/convites</c>: o e-mail de quem vai se cadastrar.</summary>
public sealed record ConviteEmpresaRequest(string? Email);

/// <summary>O convite emitido ou reenviado e se o e-mail saiu (a Api responde com o detalhe lido pela consulta).</summary>
public sealed record ConviteEnviado(Guid Id, bool EmailEnviado);

/// <summary>
/// Um convite de empresa na área (F14-72): o e-mail, a situação no instante da leitura e as datas. Nunca o token nem o
/// hash dele.
/// </summary>
public sealed record ConviteEmpresaNebrysDto(
    Guid Id,
    string Email,
    SituacaoConviteEmpresa Situacao,
    DateTimeOffset CriadoEm,
    DateTimeOffset EnviadoEm,
    DateTimeOffset ExpiraEm,
    DateTimeOffset? UsadoEm,
    DateTimeOffset? RevogadoEm);

/// <summary>Resposta da emissão e do reenvio: o convite e se o e-mail com o link saiu (falso: a Nebrys reenvia).</summary>
public sealed record EnvioConviteEmpresaDto(ConviteEmpresaNebrysDto Convite, bool EmailEnviado);
