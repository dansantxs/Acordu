using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;

namespace Acordu.Domain.Entities;

/// <summary>
/// Convite de uma pessoa para a equipe do prestador, com um papel INTERNO (segurança §4). Mesmo desenho do
/// <see cref="ConvitePortal"/>: nasce sem token, o token é gerado no envio do e-mail (só o hash fica gravado) e vale
/// 7 dias a partir dali. Aceito, vira o <see cref="Vinculo"/> com o papel do convite. Um pendente por e-mail no tenant:
/// reconvidar revoga o anterior (Application).
/// </summary>
public sealed class ConviteEquipe : EntidadeBase
{
    /// <summary>Validade do link de aceite, contada a partir do envio.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromDays(7);

    private ConviteEquipe(Guid id, Guid tenantId)
        : base(id, tenantId)
    {
        Email = null!;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private ConviteEquipe()
    {
        Email = null!;
    }

    /// <summary>Normalizado em minúsculas.</summary>
    public string Email { get; private set; }

    public Guid PapelId { get; private set; }

    /// <summary>Quem convidou.</summary>
    public Guid ConvidadoPor { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary>SHA-256 do token do último envio; nulo até o primeiro envio.</summary>
    public string? TokenHash { get; private set; }

    public DateTimeOffset? ExpiraEm { get; private set; }

    public DateTimeOffset? AceitoEm { get; private set; }

    public Guid? UsuarioId { get; private set; }

    public DateTimeOffset? RevogadoEm { get; private set; }

    public bool Pendente => AceitoEm is null && RevogadoEm is null;

    /// <summary>Recebe o papel só para validar (INTERNO, do mesmo tenant); guarda o Id.</summary>
    public static ConviteEquipe Criar(Guid id, string email, Papel papel, Guid convidadoPor, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(papel);
        if (papel.Tipo != TipoPapel.Interno)
            throw new EquipeException("Contatos de clientes entram pelo convite do portal, não pela equipe.");

        var convite = new ConviteEquipe(id, papel.TenantId)
        {
            Email = Common.Email.Normalizar(email, nameof(email)),
            PapelId = papel.Id,
            ConvidadoPor = Ids.Obrigatorio(convidadoPor, nameof(convidadoPor)),
            CriadoEm = agora.ToUniversalTime(),
        };
        convite.Registrar(new ConviteEquipeCriado(convite.Id, convite.TenantId, convite.CriadoEm));
        return convite;
    }

    /// <summary>Gera o token do e-mail (só aqui em claro) e reinicia a validade; chamado de novo, invalida o anterior.</summary>
    public string GerarToken(DateTimeOffset agora)
    {
        if (!Pendente)
            throw new EquipeException("Convite já aceito ou revogado.");

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
            throw new EquipeException("Convite inválido, expirado ou já utilizado.");

        UsuarioId = Ids.Obrigatorio(usuarioId, nameof(usuarioId));
        AceitoEm = agora.ToUniversalTime();
    }

    /// <summary>Revoga o convite pendente; aceito ou já revogado, nada muda.</summary>
    public void Revogar(DateTimeOffset agora)
    {
        if (Pendente)
            RevogadoEm = agora.ToUniversalTime();
    }
}
