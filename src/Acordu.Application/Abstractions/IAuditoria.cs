namespace Acordu.Application.Abstractions;

/// <summary>
/// Trilha de auditoria (RNF-AUD-01, segurança §8). O registro entra na unidade de trabalho corrente e é gravado no
/// próximo <see cref="IUnitOfWork.SalvarAsync"/>, na mesma transação: só fica auditado o que foi efetivado. Quem, tenant,
/// quando e IP vêm do contexto.
/// </summary>
public interface IAuditoria
{
    /// <param name="acao">Uma das <see cref="Auditoria.AcoesAuditoria"/>.</param>
    /// <param name="alvoTipo">Tipo do alvo (<c>contrato</c>, <c>apontamento</c>...).</param>
    /// <param name="dados">Detalhes serializados em JSON (ex.: valor anterior). Nunca segredo nem token.</param>
    void Registrar(string acao, string alvoTipo, Guid? alvoId, object? dados = null);
}
