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
    private readonly IEnviosBloqueados _bloqueados;

    public EnvioATerceiros(ITenantAtual tenant, IEnviosBloqueados bloqueados)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(bloqueados);
        _tenant = tenant;
        _bloqueados = bloqueados;
    }

    /// <summary>Empresa não liberada → <see cref="EmailNaoConfirmadoException"/>.</summary>
    public async Task GarantirLiberadoAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await _tenant.ObterAsync(cancellationToken);
        if (!tenant.EnvioLiberado)
            throw new EmailNaoConfirmadoException();
    }

    /// <summary>
    /// Aviso automático por e-mail (F9b): sai de empresa liberada e, antes disso, só para o próprio criador (spec do F9,
    /// decisão 8). Bloqueado é descartado, não adiado (decisão 6), e fica registrado.
    /// </summary>
    /// <param name="usuarioId">Destinatário da equipe; nulo para quem não é da equipe (contato).</param>
    public async Task<bool> PodeAvisarAsync(string categoria, Guid? usuarioId, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenant.ObterAsync(cancellationToken);
        if (tenant.EnvioLiberado || (usuarioId is { } id && id == tenant.CriadoPorUsuarioId))
            return true;

        _bloqueados.Registrar(categoria);
        return false;
    }
}
