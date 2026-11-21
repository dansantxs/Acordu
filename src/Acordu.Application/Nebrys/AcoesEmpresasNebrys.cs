using Acordu.Application.Auditoria;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.Nebrys;

/// <summary>
/// Ações da Nebrys sobre a situação de uma empresa (RF-NEB-01; F14-65 a F14-69, F14-106, F14-107): ativar, prorrogar o
/// teste, cancelar e reativar, pelas transições do <see cref="Tenant"/> (modelo §4.11.3). Cada uma leva o <c>If-Match</c>
/// com a versão lida no detalhe (design D5) e vai para a trilha sem tenant, com o antes e o depois da situação (D14). A
/// volta a gravar vale na requisição seguinte: a guarda lê a situação do banco a cada escopo (ADR 0015, item 1).
/// </summary>
public sealed class AcoesEmpresasNebrys
{
    /// <summary>Campo do último dia do teste no corpo da prorrogação.</summary>
    public const string CampoAte = "ate";

    private readonly ISituacaoDasEmpresas _situacoes;
    private readonly TimeProvider _relogio;

    public AcoesEmpresasNebrys(ISituacaoDasEmpresas situacoes, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(situacoes);
        ArgumentNullException.ThrowIfNull(relogio);
        _situacoes = situacoes;
        _relogio = relogio;
    }

    /// <summary>TESTE ou VENCIDA → ATIVA, sem fim de teste (F14-65).</summary>
    public Task AtivarAsync(Guid empresaId, string? versaoEsperada, CancellationToken cancellationToken = default) =>
        AlterarAsync(empresaId, versaoEsperada, AcoesAuditoria.NebrysEmpresaAtivacao, (t, _, agora) => t.Ativar(agora), cancellationToken);

    /// <summary>
    /// TESTE ou VENCIDA → TESTE até o fim do dia <see cref="ProrrogacaoTesteRequest.Ate"/> no fuso da empresa (F14-66); dia
    /// já passado → <c>422</c> (F14-69). Sem o dia → <c>400</c> no campo <c>ate</c>.
    /// </summary>
    public Task ProrrogarAsync(
        Guid empresaId, ProrrogacaoTesteRequest corpo, string? versaoEsperada, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(corpo);
        var ate = corpo.Ate ?? throw new ArgumentException("Informe o último dia do teste.", CampoAte);
        return AlterarAsync(
            empresaId, versaoEsperada, AcoesAuditoria.NebrysEmpresaProrrogacao, (t, fuso, agora) => t.Prorrogar(ate, fuso, agora),
            cancellationToken);
    }

    /// <summary>TESTE, ATIVA ou VENCIDA → CANCELADA, só leitura com a data do cancelamento (F14-67).</summary>
    public Task CancelarAsync(Guid empresaId, string? versaoEsperada, CancellationToken cancellationToken = default) =>
        AlterarAsync(empresaId, versaoEsperada, AcoesAuditoria.NebrysEmpresaCancelamento, (t, _, agora) => t.Cancelar(agora), cancellationToken);

    /// <summary>CANCELADA ainda não expurgada → ATIVA (F14-68).</summary>
    public Task ReativarAsync(Guid empresaId, string? versaoEsperada, CancellationToken cancellationToken = default) =>
        AlterarAsync(empresaId, versaoEsperada, AcoesAuditoria.NebrysEmpresaReativacao, (t, _, agora) => t.Reativar(agora), cancellationToken);

    private Task AlterarAsync(
        Guid empresaId,
        string? versaoEsperada,
        string acao,
        Action<Tenant, TimeZoneInfo, DateTimeOffset> transicao,
        CancellationToken cancellationToken)
    {
        var agora = _relogio.GetUtcNow();
        return _situacoes.AlterarAsync(
            empresaId,
            versaoEsperada,
            (tenant, fuso) =>
            {
                var antes = SituacaoNaTrilha.De(tenant);
                transicao(tenant, fuso, agora);
                return new AcaoNaTrilha(acao, new { antes, depois = SituacaoNaTrilha.De(tenant) });
            },
            cancellationToken);
    }
}

/// <summary>Corpo do <c>POST /nebrys/empresas/{id}/prorrogacao</c>: o último dia do teste (data civil no fuso da empresa).</summary>
public sealed record ProrrogacaoTesteRequest(DateOnly? Ate);

/// <summary>
/// A situação de uma empresa no antes e no depois da trilha das ações da área (D14): só os campos da relação com a
/// Nebrys, sem o documento.
/// </summary>
public sealed record SituacaoNaTrilha(
    SituacaoEmpresa Situacao,
    DateTimeOffset? FimDoTeste,
    DateTimeOffset? SomenteLeituraDesde,
    DateTimeOffset? CanceladaEm)
{
    public static SituacaoNaTrilha De(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return new SituacaoNaTrilha(tenant.Situacao, tenant.FimDoTeste, tenant.SomenteLeituraDesde, tenant.CanceladaEm);
    }
}
