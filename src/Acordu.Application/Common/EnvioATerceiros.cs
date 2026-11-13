using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Domain.Entities;

namespace Acordu.Application.Common;

/// <summary>
/// Barreira do envio a terceiros (RF-AUT-06, F9): convites e links só saem de empresa liberada; avisos automáticos, só
/// de empresa liberada e fora do só leitura (RF-EMP-02, F14d). A permissão de quem age
/// é conferida antes (sem ela, 403); nos casos de uso, esta checagem vem depois da carga do alvo (inexistente, 404) e
/// antes das regras de negócio.
/// </summary>
public sealed class EnvioATerceiros
{
    private readonly ITenantAtual _tenant;
    private readonly IEnviosBloqueados _bloqueados;
    private readonly IOrigemDoEvento _origem;
    private Tenant? _carregado;

    public EnvioATerceiros(ITenantAtual tenant, IEnviosBloqueados bloqueados, IOrigemDoEvento origem)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(bloqueados);
        ArgumentNullException.ThrowIfNull(origem);
        _tenant = tenant;
        _bloqueados = bloqueados;
        _origem = origem;
    }

    /// <summary>Empresa não liberada → <see cref="EmailNaoConfirmadoException"/>.</summary>
    public async Task GarantirLiberadoAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await TenantAsync(cancellationToken);
        if (!tenant.EnvioLiberado)
            throw new EmailNaoConfirmadoException();
    }

    /// <summary>
    /// Aviso automático por e-mail (F9b): sai de empresa liberada e, antes disso, só para o próprio criador (spec do F9,
    /// decisão 8). Bloqueado é descartado, não adiado (decisão 6), e fica registrado. Vale o estado da empresa na hora do
    /// evento: uma nova tentativa da outbox depois da liberação não entrega o aviso de antes dela.
    /// </summary>
    /// <param name="usuarioId">Destinatário da equipe; nulo para quem não é da equipe (contato).</param>
    public async Task<bool> PodeAvisarAsync(string categoria, Guid? usuarioId, CancellationToken cancellationToken = default)
    {
        var tenant = await TenantAsync(cancellationToken);
        // Empresa só leitura não envia nada (RF-EMP-02, ADR 0015, item 5): vale o estado atual, sem envio retroativo.
        if (tenant.SomenteLeitura)
        {
            _bloqueados.Registrar(categoria);
            return false;
        }

        // Fora da outbox (jobs que avaliam no momento) não há instante do evento: vale o estado atual.
        var liberadaNoEvento = tenant.EnvioLiberadoEm is { } liberadaEm
            && (_origem.OcorridoEm is not { } ocorridoEm || ocorridoEm >= liberadaEm);
        if (liberadaNoEvento || (usuarioId is { } id && id == tenant.CriadoPorUsuarioId))
            return true;

        _bloqueados.Registrar(categoria);
        return false;
    }

    /// <summary>Escopo próprio por requisição e por mensagem da outbox: guardar o tenant evita uma consulta por destinatário.</summary>
    private async Task<Tenant> TenantAsync(CancellationToken cancellationToken) =>
        _carregado ??= await _tenant.ObterAsync(cancellationToken);
}
