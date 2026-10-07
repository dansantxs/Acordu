using Acordu.Domain.Enums;

namespace Acordu.Application.Portal;

/// <summary>
/// Contato que está usando o portal, lido do banco a cada requisição (não do token): revogar o acesso vale na hora e o
/// escopo é sempre o atual do vínculo. Nulo quando o acesso não existe mais.
/// </summary>
public interface IPortalAtual
{
    Task<ContatoDoPortal?> ObterAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Quem é o contato no cliente do contexto e o que enxerga. Na sessão de link, <see cref="SolicitacaoPermitida"/> é a
/// única solicitação visível (vale sobre o escopo) e o escopo é PROPRIAS.
/// </summary>
public sealed record ContatoDoPortal(Guid ContatoId, Guid ClienteId, EscopoPortal Escopo, Guid? SolicitacaoPermitida = null)
{
    /// <summary>
    /// Extrato e contratos só com TODAS_DO_CLIENTE e conta (segurança §3.2: o extrato expõe pedidos de colegas; o link
    /// vale para uma solicitação).
    /// </summary>
    public bool VeContratos => Escopo == EscopoPortal.TodasDoCliente && SolicitacaoPermitida is null;

    /// <summary>Regra de visibilidade de uma solicitação do cliente: link → só a dele; PROPRIAS → só em que é o contato.</summary>
    public bool Ve(Guid solicitacaoId, Guid? contatoDaSolicitacao) =>
        SolicitacaoPermitida is { } permitida
            ? permitida == solicitacaoId
            : Escopo == EscopoPortal.TodasDoCliente || contatoDaSolicitacao == ContatoId;
}
