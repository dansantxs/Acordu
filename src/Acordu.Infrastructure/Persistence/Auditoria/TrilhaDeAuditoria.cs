using System.Text.Json;
using Acordu.Application.Abstractions;

namespace Acordu.Infrastructure.Persistence.Auditoria;

/// <summary>
/// <see cref="IAuditoria"/> sobre o DbContext da requisição: o registro é adicionado e vai junto no próximo
/// SaveChanges da unidade de trabalho (mesma transação). Exige tenant no contexto.
/// </summary>
public sealed class TrilhaDeAuditoria : IAuditoria
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AcorduDbContext _db;
    private readonly ITenantContext _contexto;
    private readonly IOrigemRequisicao _origem;
    private readonly TimeProvider _relogio;

    public TrilhaDeAuditoria(AcorduDbContext db, ITenantContext contexto, IOrigemRequisicao origem, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(relogio);
        _db = db;
        _contexto = contexto;
        _origem = origem;
        _relogio = relogio;
    }

    public void Registrar(string acao, string alvoTipo, Guid? alvoId, object? dados = null)
    {
        var tenant = _contexto.TenantId
            ?? throw new InvalidOperationException("Auditoria exige tenant no contexto.");
        _db.Add(RegistroAuditoria.Criar(
            tenant,
            _contexto.UsuarioId,
            acao,
            alvoTipo,
            alvoId,
            Serializar(dados),
            _origem.Ip,
            _relogio.GetUtcNow()));
    }

    internal static string? Serializar(object? dados) => dados is null ? null : JsonSerializer.Serialize(dados, Json);
}

/// <summary>Fora de requisição (jobs, testes de integração): sem IP.</summary>
public sealed class SemOrigemRequisicao : IOrigemRequisicao
{
    public string? Ip => null;
}
