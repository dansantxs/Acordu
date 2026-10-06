using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;

namespace Acordu.Domain.Entities;

/// <summary>
/// Convite de um contato para o portal com conta (segurança §4). Nasce sem token: o token é gerado no envio do e-mail
/// (<see cref="GerarToken"/>), então nunca fica em claro no banco nem na outbox; a validade conta a partir dali. Aceito,
/// vira o <see cref="Vinculo"/> com papel Cliente e o escopo do convite. Um convite pendente por contato: reemitir revoga
/// o anterior (orquestrado pela Application).
/// </summary>
public sealed class ConvitePortal : RaizComCliente
{
    /// <summary>Validade do link de aceite, contada a partir do envio.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromDays(7);

    private ConvitePortal(Guid id, Guid tenantId, Guid clienteId)
        : base(id, tenantId, clienteId)
    {
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private ConvitePortal()
    {
    }

    public Guid ContatoId { get; private set; }

    public EscopoPortal Escopo { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary>SHA-256 do token do último envio; nulo até o primeiro envio.</summary>
    public string? TokenHash { get; private set; }

    public DateTimeOffset? ExpiraEm { get; private set; }

    public DateTimeOffset? AceitoEm { get; private set; }

    /// <summary>Usuário que aceitou (criado no aceite ou já existente).</summary>
    public Guid? UsuarioId { get; private set; }

    public DateTimeOffset? RevogadoEm { get; private set; }

    /// <summary>Nem aceito nem revogado (pode estar expirado: reemitir resolve).</summary>
    public bool Pendente => AceitoEm is null && RevogadoEm is null;

    public static ConvitePortal Criar(
        Guid id, Guid tenantId, Guid clienteId, Guid contatoId, EscopoPortal escopo, DateTimeOffset agora)
    {
        var convite = new ConvitePortal(id, tenantId, clienteId)
        {
            ContatoId = Ids.Obrigatorio(contatoId, nameof(contatoId)),
            Escopo = ValidarEscopo(escopo),
            CriadoEm = agora.ToUniversalTime(),
        };
        convite.Registrar(new ConvitePortalCriado(convite.Id, tenantId, clienteId, convite.ContatoId, convite.CriadoEm));
        return convite;
    }

    /// <summary>
    /// Gera o token do e-mail (só aqui em claro) e reinicia a validade. Chamado de novo, invalida o token anterior.
    /// </summary>
    public string GerarToken(DateTimeOffset agora)
    {
        if (!Pendente)
            throw new AcessoPortalException("Convite já aceito ou revogado.");

        var token = TokenSeguro.Gerar();
        TokenHash = TokenSeguro.Hash(token);
        ExpiraEm = agora.ToUniversalTime() + Validade;
        return token;
    }

    /// <summary>Pendente, já enviado, dentro da validade e com o token certo (comparação em tempo constante).</summary>
    public bool Validar(string? token, DateTimeOffset agora) =>
        Pendente && ExpiraEm is { } expira && agora < expira && TokenSeguro.Confere(token, TokenHash);

    public void Aceitar(string token, Guid usuarioId, DateTimeOffset agora)
    {
        if (!Validar(token, agora))
            throw new AcessoPortalException("Convite inválido, expirado ou já utilizado.");

        UsuarioId = Ids.Obrigatorio(usuarioId, nameof(usuarioId));
        AceitoEm = agora.ToUniversalTime();
    }

    public void AlterarEscopo(EscopoPortal escopo)
    {
        if (!Pendente)
            throw new AcessoPortalException("Convite já aceito ou revogado.");
        Escopo = ValidarEscopo(escopo);
    }

    /// <summary>Revoga o convite pendente; aceito ou já revogado, nada muda (o aceite é histórico).</summary>
    public void Revogar(DateTimeOffset agora)
    {
        if (Pendente)
            RevogadoEm = agora.ToUniversalTime();
    }

    private static EscopoPortal ValidarEscopo(EscopoPortal escopo) =>
        Enum.IsDefined(escopo) ? escopo : throw new ArgumentOutOfRangeException(nameof(escopo), escopo, "Escopo inválido.");
}
