using Acordu.Application.Abstractions;
using Acordu.Domain.Enums;

namespace Acordu.Application.Empresas;

/// <summary>
/// Passo <c>vencimento-testes</c> do job diário (F14-03 a F14-06; deploy-ci §5), no tenant do contexto: a empresa em
/// TESTE com o fim do teste já alcançado no instante do diário passa a VENCIDA. Agido pelo sistema, sem usuário.
/// Idempotente: a já vencida (e a ATIVA, a CANCELADA, a de demonstração) não muda nem conta.
/// </summary>
public sealed class VencimentoTestes
{
    private readonly ITenantAtual _tenant;
    private readonly TimeProvider _relogio;
    private readonly IUnitOfWork _unidade;

    public VencimentoTestes(ITenantAtual tenant, TimeProvider relogio, IUnitOfWork unidade)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        _tenant = tenant;
        _relogio = relogio;
        _unidade = unidade;
    }

    /// <summary>1 se a empresa do contexto venceu agora; 0 se nada mudou.</summary>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await _tenant.ObterAsync(cancellationToken);
        var agora = _relogio.GetUtcNow();
        if (tenant.Situacao != SituacaoEmpresa.Teste || tenant.FimDoTeste is not { } fim || agora < fim)
            return 0;

        tenant.Vencer(agora);
        await _unidade.SalvarAsync(cancellationToken);
        return 1;
    }
}
