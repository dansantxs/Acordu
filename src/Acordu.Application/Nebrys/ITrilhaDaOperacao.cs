namespace Acordu.Application.Nebrys;

/// <summary>
/// Trilha das ações da área da Nebrys (ADR 0014, item 6; ADR 0008; design D14): <b>sem tenant</b>, com o operador
/// (<see cref="IOperadorAtual"/>) em <c>usuario</c> e o IP da requisição. Como o <c>IAuditoria</c>, o registro entra na
/// unidade de trabalho corrente e é gravado no próximo <c>SalvarAsync</c>, na mesma transação. Fica só para a operação:
/// nenhuma empresa o vê, e ele sobrevive ao expurgo dela (12 meses, segurança §8).
/// </summary>
public interface ITrilhaDaOperacao
{
    /// <param name="acao">Uma das ações <c>nebrys.*</c> de <see cref="Auditoria.AcoesAuditoria"/>.</param>
    /// <param name="alvoTipo">Tipo do alvo (<c>empresa</c>, <c>documento</c>...).</param>
    /// <param name="alvoId">Id do alvo, quando ele tem um (o documento não tem: só o HMAC, que não vai para a trilha).</param>
    /// <param name="dados">Antes e depois, serializados em JSON. Nunca o documento.</param>
    void Registrar(string acao, string alvoTipo, Guid? alvoId, object? dados = null);
}
