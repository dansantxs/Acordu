using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;

namespace Acordu.Domain.Entities;

/// <summary>
/// Convite da Nebrys para uma empresa nova se cadastrar no Acordu (RF-AUT-08, F14n; decisões 4, 6 e 7 do G2 do F14; design
/// D12): global, fora dos tenants (a empresa ainda não existe), sem referência a empresa (ADR 0014, item 7). Vale para um
/// e-mail e um cadastro só, com o e-mail do cadastro igual ao do convite (F14-49). O token tem 32 bytes e só o SHA-256
/// fica gravado (convenções §1); ele é gerado na emissão e no reenvio e vai só no e-mail, que sai na própria requisição,
/// depois do commit (não há tenant para a outbox). Reenviar troca o token e reinicia a validade: o link anterior para de
/// valer (F14-73).
/// </summary>
public sealed class ConviteEmpresa
{
    // Só o EF materializa.
    private ConviteEmpresa()
    {
        Email = null!;
        TokenHash = null!;
    }

    private ConviteEmpresa(Guid id, string email, DateTimeOffset criadoEm)
    {
        Id = id;
        Email = email;
        CriadoEm = criadoEm;
        TokenHash = null!;
    }

    public Guid Id { get; private set; }

    /// <summary>Normalizado por <see cref="Common.Email"/> (aparado, minúsculo).</summary>
    public string Email { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary>SHA-256 do token do último envio (emissão ou reenvio), em hexadecimal minúsculo.</summary>
    public string TokenHash { get; private set; }

    /// <summary>Quando o último link foi gerado (emissão ou reenvio).</summary>
    public DateTimeOffset EnviadoEm { get; private set; }

    /// <summary>Fim da validade do último link: o envio mais a validade da configuração (<c>Cadastro__DiasConvite</c>).</summary>
    public DateTimeOffset ExpiraEm { get; private set; }

    /// <summary>Quando um cadastro usou o convite (F14-47); nulo enquanto não usado.</summary>
    public DateTimeOffset? UsadoEm { get; private set; }

    /// <summary>Quando a Nebrys revogou (F14-74); nulo sem revogação.</summary>
    public DateTimeOffset? RevogadoEm { get; private set; }

    /// <summary>Nem usado nem revogado: pode ser reenviado ou revogado (vencido também, para reenviar).</summary>
    public bool Pendente => UsadoEm is null && RevogadoEm is null;

    /// <summary>
    /// A Nebrys emite o convite para o e-mail (F14-72): o token volta só aqui, em claro, para o e-mail; o convite guarda o
    /// hash. E-mail inválido → <see cref="ArgumentException"/> no parâmetro <c>email</c>.
    /// </summary>
    public static (ConviteEmpresa Convite, string Token) Emitir(Guid id, string email, TimeSpan validade, DateTimeOffset agora)
    {
        var convite = new ConviteEmpresa(Ids.Obrigatorio(id, nameof(id)), Common.Email.Normalizar(email, nameof(email)), agora.ToUniversalTime());
        var token = convite.NovoToken(validade, agora);
        return (convite, token);
    }

    /// <summary>
    /// Reenvio (F14-73): token novo e validade reiniciada; o link anterior para de valer. Usado ou revogado →
    /// <see cref="TransicaoInvalidaException"/>. Vencido pode ser reenviado (é para isso que o reenvio existe).
    /// </summary>
    public string Reenviar(TimeSpan validade, DateTimeOffset agora)
    {
        if (!Pendente)
            throw new TransicaoInvalidaException(UsadoEm is not null
                ? "Este convite já foi usado num cadastro: não há o que reenviar."
                : "Este convite foi revogado: emita um novo.");
        return NovoToken(validade, agora);
    }

    /// <summary>Revogação (F14-74): o link para de valer. Usado ou já revogado → <see cref="TransicaoInvalidaException"/>.</summary>
    public void Revogar(DateTimeOffset agora)
    {
        if (!Pendente)
            throw new TransicaoInvalidaException(UsadoEm is not null
                ? "Este convite já foi usado num cadastro: não dá para revogar."
                : "Este convite já foi revogado.");
        RevogadoEm = agora.ToUniversalTime();
    }

    /// <summary>Pendente, dentro da validade e com o token do último envio (comparação em tempo constante).</summary>
    public bool Validar(string? token, DateTimeOffset agora) =>
        Pendente && agora < ExpiraEm && TokenSeguro.Confere(token, TokenHash);

    /// <summary>O e-mail do cadastro é o do convite (F14-49), comparado já normalizado.</summary>
    public bool EhDoEmail(string emailNormalizado) => string.Equals(Email, emailNormalizado, StringComparison.Ordinal);

    /// <summary>
    /// Um cadastro usa o convite (F14-47): fica usado e não vale para outro (F14-48). Token que não vale ou e-mail de outro →
    /// <see cref="TransicaoInvalidaException"/> (quem chama confere antes com <see cref="Validar"/> e <see cref="EhDoEmail"/>).
    /// </summary>
    public void Usar(string token, string emailNormalizado, DateTimeOffset agora)
    {
        if (!Validar(token, agora) || !EhDoEmail(emailNormalizado))
            throw new TransicaoInvalidaException("Convite inválido, vencido, já usado ou de outro e-mail.");
        UsadoEm = agora.ToUniversalTime();
    }

    /// <summary>Situação no instante dado (não gravada): usado e revogado antes de vencido.</summary>
    public SituacaoConviteEmpresa SituacaoEm(DateTimeOffset agora) =>
        UsadoEm is not null ? SituacaoConviteEmpresa.Usado
        : RevogadoEm is not null ? SituacaoConviteEmpresa.Revogado
        : agora < ExpiraEm ? SituacaoConviteEmpresa.Pendente
        : SituacaoConviteEmpresa.Vencido;

    private string NovoToken(TimeSpan validade, DateTimeOffset agora)
    {
        if (validade <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(validade), validade, "A validade do convite precisa ser positiva.");
        var token = TokenSeguro.Gerar();
        TokenHash = TokenSeguro.Hash(token);
        EnviadoEm = agora.ToUniversalTime();
        ExpiraEm = EnviadoEm + validade;
        return token;
    }
}
