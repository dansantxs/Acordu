namespace Acordu.Application.Abstractions.Operacao;

/// <summary>
/// Um erro inesperado, pela impressão digital (F13-05): serviço, tipo da exceção, primeiro quadro do código do Acordu e
/// rota modelo com o método. Sem a mensagem da exceção.
/// </summary>
public sealed record ErroInesperado(string Servico, string Tipo, string? Quadro, string Rota, string Versao);

/// <summary>
/// Aviso de erro em #alertas, com deduplicação (RNF-OPS-02, ADR 0012): na primeira vez, "Erro novo"; quieto por 24 h;
/// depois, "Erro de novo" com a contagem; teto por hora. Nunca lança.
/// </summary>
public interface IAvisosDeErro
{
    Task AvisarAsync(ErroInesperado erro, CancellationToken cancellationToken = default);
}
