using Acordu.Domain.Common;

namespace Acordu.Domain.Entities;

/// <summary>
/// Link sem senha para um contato acessar uma única solicitação (RF-POR-08, segurança §5.3). O token em claro existe só
/// no retorno de <see cref="Emitir"/> (vai no e-mail); no banco fica o SHA-256. "Reemitir revoga o anterior" e a
/// revogação ao remover o contato são orquestradas pela Application.
/// </summary>
public sealed class AcessoPortal : RaizComCliente
{
    private AcessoPortal(Guid id, Guid tenantId, Guid clienteId)
        : base(id, tenantId, clienteId)
    {
        TokenHash = null!;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private AcessoPortal()
    {
        TokenHash = null!;
    }

    public Guid SolicitacaoId { get; private set; }

    public Guid ContatoId { get; private set; }

    /// <summary>SHA-256 do token, em hexadecimal minúsculo (64 caracteres). Chave de busca na troca do link por sessão.</summary>
    public string TokenHash { get; private set; }

    public DateTimeOffset EmitidoEm { get; private set; }

    public DateTimeOffset ExpiraEm { get; private set; }

    public DateTimeOffset? RevogadoEm { get; private set; }

    public bool Revogado => RevogadoEm is not null;

    /// <summary>Emite o acesso com um token aleatório de 32 bytes (CSPRNG, base64url) válido por <paramref name="validade"/>.</summary>
    public static AcessoPortalEmitido Emitir(
        Guid id,
        Guid tenantId,
        Guid clienteId,
        Guid solicitacaoId,
        Guid contatoId,
        DateTimeOffset agora,
        TimeSpan validade)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(validade, TimeSpan.Zero);
        var token = TokenSeguro.Gerar();
        var emitidoEm = agora.ToUniversalTime();
        var acesso = new AcessoPortal(id, tenantId, clienteId)
        {
            SolicitacaoId = Ids.Obrigatorio(solicitacaoId, nameof(solicitacaoId)),
            ContatoId = Ids.Obrigatorio(contatoId, nameof(contatoId)),
            TokenHash = HashDoToken(token),
            EmitidoEm = emitidoEm,
            ExpiraEm = emitidoEm + validade,
        };
        return new AcessoPortalEmitido(acesso, token);
    }

    /// <summary>Hash usado para persistir e para localizar o acesso a partir do token recebido.</summary>
    public static string HashDoToken(string token) => TokenSeguro.Hash(token);

    /// <summary>Hash confere (comparação em tempo constante), não revogado e <paramref name="agora"/> antes da expiração.</summary>
    public bool Validar(string token, DateTimeOffset agora) =>
        !Revogado && agora < ExpiraEm && TokenSeguro.Confere(token, TokenHash);

    /// <summary>Revoga o acesso; revogar de novo mantém o instante original.</summary>
    public void Revogar(DateTimeOffset agora) => RevogadoEm ??= agora.ToUniversalTime();
}
