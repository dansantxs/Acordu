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

/// <summary>Quem é o contato no cliente do contexto e o que enxerga.</summary>
public sealed record ContatoDoPortal(Guid ContatoId, Guid ClienteId, EscopoPortal Escopo)
{
    /// <summary>Extrato e contratos só com TODAS_DO_CLIENTE (segurança §3.2: o extrato expõe pedidos de colegas).</summary>
    public bool VeContratos => Escopo == EscopoPortal.TodasDoCliente;
}
