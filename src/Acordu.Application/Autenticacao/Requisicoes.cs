namespace Acordu.Application.Autenticacao;

/// <summary>Corpo do <c>POST /auth/registro</c>.</summary>
public sealed record RegistroRequest(string Nome, string Email, string Senha, string NomeEmpresa);

/// <summary>Corpo do <c>POST /auth/login</c>.</summary>
public sealed record LoginRequest(string Email, string Senha);

/// <summary>Corpo do <c>POST /auth/tenant-ativo</c>.</summary>
public sealed record TrocaTenantRequest(Guid TenantId);

/// <summary>Resposta das rotas que abrem sessão; o refresh token vai em cookie HttpOnly, nunca no corpo.</summary>
public sealed record TokenDeAcesso(string AccessToken, DateTimeOffset ExpiraEm);
