using Acordu.Application.Abstractions;
using Acordu.Application.Nebrys;

namespace Acordu.Infrastructure.Persistence.Auditoria;

/// <summary>
/// <see cref="ITrilhaDaOperacao"/> sobre o DbContext da requisição: o registro <b>sem tenant</b>, com o operador da
/// Nebrys em <c>usuario</c>, entra no próximo SaveChanges (mesma transação), como o login com falha e a sessão de operação
/// (ADR 0008, item 4; ADR 0014, item 6). Invisível pelos filtros: nenhuma empresa o vê.
/// </summary>
public sealed class TrilhaDaOperacao : ITrilhaDaOperacao
{
    private readonly AcorduDbContext _db;
    private readonly IOperadorAtual _operador;
    private readonly IOrigemRequisicao _origem;
    private readonly TimeProvider _relogio;

    public TrilhaDaOperacao(AcorduDbContext db, IOperadorAtual operador, IOrigemRequisicao origem, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(operador);
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(relogio);
        _db = db;
        _operador = operador;
        _origem = origem;
        _relogio = relogio;
    }

    public void Registrar(string acao, string alvoTipo, Guid? alvoId, object? dados = null) =>
        _db.Add(Registro(_operador.Id, acao, alvoTipo, alvoId, dados, _origem.Ip, _relogio.GetUtcNow()));

    /// <summary>A linha da trilha de uma ação da área: sem tenant, com o operador.</summary>
    internal static RegistroAuditoria Registro(
        Guid operador, string acao, string alvoTipo, Guid? alvoId, object? dados, string? ip, DateTimeOffset agora) =>
        RegistroAuditoria.Criar(null, operador, acao, alvoTipo, alvoId, TrilhaDeAuditoria.Serializar(dados), ip, agora);
}
