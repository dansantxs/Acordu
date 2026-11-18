using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Nebrys;
using Acordu.Domain.Common;
using Acordu.Infrastructure.Persistence;
using Acordu.Infrastructure.Persistence.Auditoria;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Seguranca;

/// <summary>
/// Sessão de operação da Nebrys sobre o Identity (ADR 0014, itens 1, 2 e 6). Acontece sem tenant no contexto e não o
/// define: só lê a conta e grava a trilha sem tenant, como o login com falha (ADR 0008, item 4).
/// </summary>
public sealed class ServicoOperacao : IServicoOperacao
{
    private readonly AcorduDbContext _db;
    private readonly UserManager<Usuario> _usuarios;
    private readonly OperadoresNebrys _operadores;
    private readonly IOrigemRequisicao _origem;
    private readonly TimeProvider _relogio;

    public ServicoOperacao(
        AcorduDbContext db,
        UserManager<Usuario> usuarios,
        OperadoresNebrys operadores,
        IOrigemRequisicao origem,
        TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(usuarios);
        ArgumentNullException.ThrowIfNull(operadores);
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(relogio);
        _db = db;
        _usuarios = usuarios;
        _operadores = operadores;
        _origem = origem;
        _relogio = relogio;
    }

    public async Task<Guid?> AbrirSessaoAsync(string? email, string? senha, CancellationToken cancellationToken = default)
    {
        // Sem operador configurado, a área não existe: nada é lido nem gravado (nem a trilha, que viraria ruído).
        if (_operadores.Vazia)
            return null;

        // Todo caminho de recusa calcula um hash de senha (o real ou o descartável), para o tempo de resposta não separar
        // e-mail sem conta, senha vazia e conta bloqueada da senha errada (segurança §5.5).
        var usuario = string.IsNullOrWhiteSpace(email) ? null : await _usuarios.FindByEmailAsync(email.Trim());
        if (usuario is null)
        {
            HashDescartavel(senha);
            return await FalhaAsync(null, email, "email-desconhecido", cancellationToken);
        }

        if (string.IsNullOrEmpty(senha))
        {
            HashDescartavel(senha);
            return await FalhaAsync(usuario, email, "senha", cancellationToken);
        }

        // Bloqueio e contagem de falhas do Identity valem como no login (5 falhas, 15 min; segurança §4).
        if (await _usuarios.IsLockedOutAsync(usuario))
        {
            HashDescartavel(senha);
            return await FalhaAsync(usuario, email, "bloqueado", cancellationToken);
        }
        if (!await _usuarios.CheckPasswordAsync(usuario, senha))
        {
            await _usuarios.AccessFailedAsync(usuario);
            return await FalhaAsync(usuario, email, "senha", cancellationToken);
        }

        if (!_operadores.Contem(usuario.Email))
            return await FalhaAsync(usuario, email, "nao-operador", cancellationToken);
        if (!usuario.EmailConfirmed)
            return await FalhaAsync(usuario, email, "email-nao-confirmado", cancellationToken);

        await _usuarios.ResetAccessFailedCountAsync(usuario);
        _db.Add(RegistroAuditoria.Criar(
            null, usuario.Id, AcoesAuditoria.NebrysSessao, null, null, null, _origem.Ip, _relogio.GetUtcNow()));
        await _db.SaveChangesAsync(cancellationToken);
        return usuario.Id;
    }

    public async Task<bool> EhOperadorAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (_operadores.Vazia || usuarioId == Guid.Empty)
            return false;
        var conta = await _db.Users.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => new { u.Email, u.EmailConfirmed })
            .SingleOrDefaultAsync(cancellationToken);
        return conta is { EmailConfirmed: true } && _operadores.Contem(conta.Email);
    }

    /// <summary>
    /// Confere a senha digitada contra um hash fixo, gerado uma vez pelo mesmo hasher do Identity (mesmo custo do
    /// <c>CheckPasswordAsync</c>). O resultado é descartado.
    /// </summary>
    private void HashDescartavel(string? senha)
    {
        var hasher = _usuarios.PasswordHasher;
        var hash = LazyInitializer.EnsureInitialized(
            ref _hashDescartavel, () => hasher.HashPassword(ContaDescartavel, Guid.NewGuid().ToString("N")));
        _ = hasher.VerifyHashedPassword(ContaDescartavel, hash, senha ?? string.Empty);
    }

    private static readonly Usuario ContaDescartavel = new("descartavel@acordu.invalid", "Descartável");

    private static string? _hashDescartavel;

    /// <summary>Recusa: na trilha sem tenant, com o e-mail digitado e o motivo; a resposta não distingue.</summary>
    private async Task<Guid?> FalhaAsync(Usuario? usuario, string? email, string motivo, CancellationToken cancellationToken)
    {
        var digitado = email?.Trim() ?? string.Empty;
        if (digitado.Length > LimitesTexto.Email)
            digitado = digitado[..LimitesTexto.Email];
        _db.Add(RegistroAuditoria.Criar(
            null,
            usuario?.Id,
            AcoesAuditoria.NebrysSessaoFalha,
            null,
            null,
            TrilhaDeAuditoria.Serializar(new { email = digitado, motivo }),
            _origem.Ip,
            _relogio.GetUtcNow()));
        await _db.SaveChangesAsync(cancellationToken);
        return null;
    }
}
