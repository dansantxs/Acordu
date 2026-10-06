using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Acordu.Application.Autenticacao;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Seguranca;

/// <summary>
/// Registro, login e sessões sobre o Identity (segurança §4–5). Login e renovação acontecem sem tenant no contexto,
/// então os vínculos e papéis são lidos ignorando os filtros — sempre restritos ao usuário em questão.
/// </summary>
public sealed class ServicoAutenticacao : IServicoAutenticacao
{
    public static readonly TimeSpan ValidadeRefresh = TimeSpan.FromDays(30);

    private static readonly string[] Filtros = [FiltrosGlobais.Tenant, FiltrosGlobais.Cliente];

    private readonly AcorduDbContext _db;
    private readonly UserManager<Usuario> _usuarios;
    private readonly ContextoTenant _contexto;
    private readonly TimeProvider _relogio;

    public ServicoAutenticacao(AcorduDbContext db, UserManager<Usuario> usuarios, ContextoTenant contexto, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(usuarios);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(relogio);
        _db = db;
        _usuarios = usuarios;
        _contexto = contexto;
        _relogio = relogio;
    }

    public async Task<Sessao> RegistrarAsync(
        string nome, string email, string senha, string nomeEmpresa, CancellationToken cancellationToken = default)
    {
        var nomeValido = Obrigatorio(nome, Usuario.TamanhoNome, nameof(nome));
        var empresaValida = Obrigatorio(nomeEmpresa, LimitesTexto.Nome, nameof(nomeEmpresa));
        var emailValido = Obrigatorio(email, LimitesTexto.Email, nameof(email));
        if (string.IsNullOrEmpty(senha))
            throw new ArgumentException("A senha é obrigatória.", nameof(senha));

        var tenantId = Guid.CreateVersion7();
        var agora = _relogio.GetUtcNow();
        _contexto.DefinirEquipe(tenantId);

        await using var transacao = await _db.Database.BeginTransactionAsync(cancellationToken);
        var usuario = new Usuario(emailValido, nomeValido);
        var criacao = await _usuarios.CreateAsync(usuario, senha);
        if (!criacao.Succeeded)
        {
            // Mensagens próprias, em português, no campo do formulário.
            throw ErroDeRegistro(criacao) switch
            {
                ErroRegistro.Senha => new ArgumentException(
                    $"A senha precisa de ao menos {OpcoesIdentidade.TamanhoMinimoSenha} caracteres.", nameof(senha)),
                ErroRegistro.EmailEmUso => new ArgumentException("Este e-mail já tem cadastro.", nameof(email)),
                _ => new ArgumentException("E-mail inválido.", nameof(email)),
            };
        }

        var papeis = Papel.Padroes(tenantId);
        var dono = papeis.Single(p => p.Nome == Papel.NomeDono);
        var vinculo = Vinculo.Criar(Guid.CreateVersion7(), usuario.Id, dono);
        _db.Add(Tenant.Criar(tenantId, empresaValida, agora));
        _db.AddRange(papeis);
        _db.Add(vinculo);
        _db.Add(CalendarioUtil.Padrao(tenantId));

        var sessao = Abrir(usuario, vinculo, dono, agora);
        await _db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return sessao;
    }

    public async Task<Sessao?> EntrarAsync(string email, string senha, CancellationToken cancellationToken = default)
    {
        var usuario = string.IsNullOrWhiteSpace(email) ? null : await _usuarios.FindByEmailAsync(email.Trim());
        if (usuario is null || string.IsNullOrEmpty(senha))
            return null;
        if (await _usuarios.IsLockedOutAsync(usuario))
            return null;
        if (!await _usuarios.CheckPasswordAsync(usuario, senha))
        {
            await _usuarios.AccessFailedAsync(usuario);
            return null;
        }

        await _usuarios.ResetAccessFailedCountAsync(usuario);
        var vinculos = await _db.Vinculos.IgnoreQueryFilters(Filtros)
            .Where(v => v.UsuarioId == usuario.Id)
            .OrderBy(v => v.Id) // Ids v7: ordem de criação
            .ToListAsync(cancellationToken);
        var vinculo = vinculos.FirstOrDefault(v => v.TenantId == usuario.UltimoTenantId) ?? vinculos.FirstOrDefault();
        if (vinculo is null)
            return null;

        var sessao = Abrir(usuario, vinculo, await PapelAsync(vinculo, cancellationToken), _relogio.GetUtcNow());
        await _db.SaveChangesAsync(cancellationToken);
        return sessao;
    }

    public async Task<Sessao?> RenovarAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var atual = await RefreshAsync(refreshToken, cancellationToken);
        if (atual is null)
            return null;

        var agora = _relogio.GetUtcNow();
        if (atual.ConsumidoEm is not null)
        {
            // Reuso de token já trocado: alguém tem uma cópia. Derruba todas as sessões do usuário nesse tenant.
            await RevogarTodosAsync(atual.UsuarioId, atual.TenantId, agora, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (!atual.Ativo(agora))
            return null;

        atual.Consumir(agora);
        var vinculo = await VinculoAsync(atual.UsuarioId, atual.TenantId, cancellationToken);
        var usuario = vinculo is null ? null : await _usuarios.FindByIdAsync(atual.UsuarioId.ToString());
        if (vinculo is null || usuario is null)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return null;
        }

        var sessao = Abrir(usuario, vinculo, await PapelAsync(vinculo, cancellationToken), agora);
        await _db.SaveChangesAsync(cancellationToken);
        return sessao;
    }

    public async Task<Sessao?> TrocarTenantAsync(
        Guid usuarioId, Guid tenantId, string? refreshAtual, CancellationToken cancellationToken = default)
    {
        var vinculo = await VinculoAsync(usuarioId, tenantId, cancellationToken);
        var usuario = vinculo is null ? null : await _usuarios.FindByIdAsync(usuarioId.ToString());
        if (vinculo is null || usuario is null)
            return null;

        var agora = _relogio.GetUtcNow();
        var atual = await RefreshAsync(refreshAtual, cancellationToken);
        if (atual is not null && atual.UsuarioId == usuarioId)
            atual.Revogar(agora);

        var sessao = Abrir(usuario, vinculo, await PapelAsync(vinculo, cancellationToken), agora);
        await _db.SaveChangesAsync(cancellationToken);
        return sessao;
    }

    public async Task SairAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        var atual = await RefreshAsync(refreshToken, cancellationToken);
        if (atual is null)
            return;

        atual.Revogar(_relogio.GetUtcNow());
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PerfilUsuario?> PerfilAsync(Guid usuarioId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
            return null;

        var vinculos = await _db.Vinculos.IgnoreQueryFilters(Filtros)
            .Where(v => v.UsuarioId == usuarioId)
            .OrderBy(v => v.Id)
            .ToListAsync(cancellationToken);
        var tenantIds = vinculos.Select(v => v.TenantId).ToList();
        var papelIds = vinculos.Select(v => v.PapelId).ToList();
        var tenants = await _db.Tenants.IgnoreQueryFilters(Filtros)
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);
        var papeis = await _db.Papeis.IgnoreQueryFilters(Filtros)
            .Where(p => papelIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var ativo = vinculos.SingleOrDefault(v => v.TenantId == tenantId);
        if (ativo is null || !tenants.TryGetValue(tenantId, out var tenant) || !papeis.TryGetValue(ativo.PapelId, out var papel))
            return null;

        var fuso = await _db.Calendarios.IgnoreQueryFilters(Filtros)
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.FusoHorario)
            .SingleOrDefaultAsync(cancellationToken) ?? CalendarioUtil.FusoPadrao;

        return new PerfilUsuario(
            usuario.Id,
            usuario.Nome,
            usuario.Email ?? string.Empty,
            new TenantAtivo(tenant.Id, tenant.Nome, fuso),
            papel.Nome,
            [.. papel.Permissoes],
            ativo.ClienteId,
            ativo.EscopoPortal,
            [.. vinculos
                .Where(v => tenants.ContainsKey(v.TenantId) && papeis.ContainsKey(v.PapelId))
                .Select(v => new TenantDoUsuario(v.TenantId, tenants[v.TenantId].Nome, papeis[v.PapelId].Nome))]);
    }

    /// <summary>SHA-256 hexadecimal do token, como fica no banco.</summary>
    public static string Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>Gera o refresh token, registra o hash e marca o tenant como o último usado.</summary>
    private Sessao Abrir(Usuario usuario, Vinculo vinculo, Papel papel, DateTimeOffset agora)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var expiraEm = agora + ValidadeRefresh;
        _db.Add(new RefreshToken(usuario.Id, vinculo.TenantId, Hash(token), agora, expiraEm));
        usuario.UltimoTenantId = vinculo.TenantId;

        return new Sessao(
            usuario.Id, vinculo.TenantId, papel.Nome, [.. papel.Permissoes], vinculo.ClienteId, vinculo.EscopoPortal, token, expiraEm);
    }

    private async Task<RefreshToken?> RefreshAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var hash = Hash(token);
        return await _db.Set<RefreshToken>().SingleOrDefaultAsync(r => r.Hash == hash, cancellationToken);
    }

    private async Task RevogarTodosAsync(Guid usuarioId, Guid tenantId, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var ativos = await _db.Set<RefreshToken>()
            .Where(r => r.UsuarioId == usuarioId && r.TenantId == tenantId && r.ConsumidoEm == null && r.RevogadoEm == null)
            .ToListAsync(cancellationToken);
        foreach (var token in ativos)
            token.Revogar(agora);
    }

    private Task<Vinculo?> VinculoAsync(Guid usuarioId, Guid tenantId, CancellationToken cancellationToken) =>
        _db.Vinculos.IgnoreQueryFilters(Filtros)
            .SingleOrDefaultAsync(v => v.UsuarioId == usuarioId && v.TenantId == tenantId, cancellationToken);

    private Task<Papel> PapelAsync(Vinculo vinculo, CancellationToken cancellationToken) =>
        _db.Papeis.IgnoreQueryFilters(Filtros)
            .SingleAsync(p => p.Id == vinculo.PapelId && p.TenantId == vinculo.TenantId, cancellationToken);

    private static string Obrigatorio(string? valor, int maximo, string parametro)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto))
            throw new ArgumentException("Campo obrigatório.", parametro);
        if (texto.Length > maximo)
            throw new ArgumentException($"Máximo de {maximo} caracteres.", parametro);
        return texto;
    }

    private static ErroRegistro ErroDeRegistro(IdentityResult resultado)
    {
        var codigos = resultado.Errors.Select(e => e.Code).ToList();
        if (codigos.Exists(c => c.StartsWith("Password", StringComparison.Ordinal)))
            return ErroRegistro.Senha;
        return codigos.Exists(c => c.StartsWith("Duplicate", StringComparison.Ordinal)) ? ErroRegistro.EmailEmUso : ErroRegistro.EmailInvalido;
    }

    private enum ErroRegistro
    {
        Senha = 1,
        EmailEmUso = 2,
        EmailInvalido = 3,
    }
}
