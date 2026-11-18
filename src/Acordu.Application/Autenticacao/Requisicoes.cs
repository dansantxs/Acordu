namespace Acordu.Application.Autenticacao;

/// <summary>
/// Corpo do <c>POST /auth/registro</c>. <see cref="Documento"/>: CPF ou CNPJ (com ou sem pontuação; o CNPJ alfanumérico
/// em qualquer caixa), obrigatório, um teste por documento (RF-AUT-09, F14g). <see cref="TokenTurnstile"/>: o do desafio
/// do Cloudflare (RNF-SEG-10), exigido só com o Turnstile ligado no servidor. <see cref="VersaoTermos"/> e
/// <see cref="VersaoPrivacidade"/>: as versões dos textos legais que a pessoa aceitou (F14h, design D10 e D16), opcionais
/// no contrato porque só são exigidas com os textos publicados (<c>TextosLegais__Publicados</c>); sem eles, ignoradas.
/// </summary>
public sealed record RegistroRequest(
    string Nome,
    string Email,
    string Senha,
    string NomeEmpresa,
    string Documento,
    string? TokenTurnstile = null,
    string? VersaoTermos = null,
    string? VersaoPrivacidade = null);

/// <summary>Corpo do <c>POST /auth/login</c>.</summary>
public sealed record LoginRequest(string Email, string Senha);

/// <summary>Corpo do <c>POST /auth/tenant-ativo</c>.</summary>
public sealed record TrocaTenantRequest(Guid TenantId);

/// <summary>Corpo do <c>POST /auth/portal/convite/consulta</c>.</summary>
public sealed record ConsultaConviteRequest(string Token);

/// <summary>Corpo do <c>POST /auth/portal/convite</c>. <see cref="Nome"/> só é usado quando a conta é criada.</summary>
public sealed record AceiteConviteRequest(string Token, string? Nome, string Senha);

/// <summary>
/// Corpo do <c>POST /auth/convite</c> (convite da equipe). <see cref="Nome"/> só é usado quando a conta é criada.
/// <see cref="VersaoTermos"/> e <see cref="VersaoPrivacidade"/> como no <see cref="RegistroRequest"/> (F14-82, F14-117):
/// o convite do portal não leva aceite (F14-120).
/// </summary>
public sealed record AceiteConviteEquipeRequest(
    string Token, string? Nome, string Senha, string? VersaoTermos = null, string? VersaoPrivacidade = null);

/// <summary>
/// Tela de aceite do convite: quem convidou e se o e-mail já tem conta (pede só a senha) ou não (pede nome e senha).
/// </summary>
public sealed record ConvitePortalInfo(string NomePrestador, string NomeContato, string Email, bool TemConta);

/// <summary>Corpo do <c>POST /auth/portal/link</c>.</summary>
public sealed record LinkPortalRequest(string Token);

/// <summary>Resposta do <c>POST /auth/portal/link</c>: access token de 60 min, sem refresh, de uma solicitação.</summary>
public sealed record TokenDeLink(string AccessToken, DateTimeOffset ExpiraEm, Guid SolicitacaoId, string Tipo);

/// <summary>Tela de aceite do convite para a equipe: quem convidou, com que papel e se o e-mail já tem conta.</summary>
public sealed record ConviteEquipeInfo(string NomeTenant, string Email, string Papel, bool TemConta);

/// <summary>Corpo do <c>POST /auth/confirmacao-email</c>.</summary>
public sealed record ConfirmacaoEmailRequest(string Token);

/// <summary>Corpo do <c>POST /auth/senha/esqueci</c>.</summary>
public sealed record EsqueciSenhaRequest(string Email);

/// <summary>Corpo do <c>POST /auth/senha/redefinir</c>.</summary>
public sealed record RedefinicaoSenhaRequest(string Token, string NovaSenha);

/// <summary>Resposta das rotas que abrem sessão; o refresh token vai em cookie HttpOnly, nunca no corpo.</summary>
public sealed record TokenDeAcesso(string AccessToken, DateTimeOffset ExpiraEm);
