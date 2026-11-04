using Acordu.Application.Abstractions;

namespace Acordu.Application.Common;

/// <summary>
/// Barreira do envio a terceiros (RF-AUT-06, F9): convites e links só saem de empresa liberada. A permissão de quem age
/// é conferida antes (sem ela, 403); nos casos de uso, esta checagem vem depois da carga do alvo (inexistente, 404) e
/// antes das regras de negócio.
/// </summary>
public sealed class EnvioATerceiros
{
    private readonly ITenantAtual _tenant;

    public EnvioATerceiros(ITenantAtual tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        _tenant = tenant;
    }

    /// <summary>Empresa não liberada → <see cref="EmailNaoConfirmadoException"/>.</summary>
    public async Task GarantirLiberadoAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await _tenant.ObterAsync(cancellationToken);
        if (!tenant.EnvioLiberado)
            throw new EmailNaoConfirmadoException();
    }
}
