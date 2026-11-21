using Acordu.Domain.Entities;

namespace Acordu.Application.Nebrys;

/// <summary>
/// Mudança de situação de uma empresa pela área da Nebrys (ADR 0014, item 5; ADR 0015, item 2). A implementação, na
/// Infrastructure, roda num escopo próprio com o tenant da empresa fixado, como os jobs, e marcado para a mudança de
/// situação: a guarda de só leitura deixa a empresa VENCIDA ou CANCELADA mudar só os campos da situação. A trilha vai na
/// mesma transação, sem tenant e com o operador (<see cref="IOperadorAtual"/>).
/// </summary>
public interface ISituacaoDasEmpresas
{
    /// <summary>
    /// Carrega a empresa, confere a versão (<c>If-Match</c>, design D5) antes de qualquer regra, aplica
    /// <paramref name="alterar"/> (que recebe o fuso da empresa e devolve o que vai para a trilha) e grava.
    /// <list type="bullet">
    /// <item>empresa inexistente ou já expurgada → <see cref="Common.NaoEncontradoException"/> (F14-106);</item>
    /// <item><paramref name="versaoEsperada"/> diferente da atual, ou outra gravação no meio do caminho → <c>409
    /// conflito-versao</c> (F14-107); nula, sem conferência (convenções §5);</item>
    /// <item>a transição que não vale lança no domínio (<c>422 transicao-invalida</c>, F14-69).</item>
    /// </list>
    /// Qualquer falha não grava nada.
    /// </summary>
    Task AlterarAsync(
        Guid empresaId,
        string? versaoEsperada,
        Func<Tenant, TimeZoneInfo, AcaoNaTrilha> alterar,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// O que a ação da área grava na trilha (ADR 0014, item 6; design D14): o nome (<c>nebrys.*</c>) e os <c>dados</c>, com o
/// antes e o depois. Nunca o documento.
/// </summary>
public sealed record AcaoNaTrilha(string Acao, object Dados);
