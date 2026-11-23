namespace Acordu.Application.Autenticacao;

/// <summary>
/// Identidade e sessões (segurança §4–5): registro, login, renovação, troca de tenant e logout. A Api transforma a
/// <see cref="Sessao"/> em access token (JWT) e o refresh token em cookie.
/// </summary>
public interface IServicoAutenticacao
{
    /// <summary>
    /// Cria usuário, tenant, papéis padrão, vínculo Dono e calendário padrão numa transação, e já abre a sessão. Depois do
    /// commit, envia a confirmação de e-mail (falha no envio não desfaz o registro: há o reenvio).
    /// Entrada inválida → <see cref="ArgumentException"/> com o campo. O <paramref name="documento"/> (CPF ou CNPJ) é
    /// obrigatório e vale um teste só (RF-AUT-09): já usado e sem liberação disponível → <see cref="ArgumentException"/> no
    /// campo <c>documento</c>, com o canal da Nebrys (F14-57); com a liberação, ela fica usada na mesma transação (F14-59).
    /// Com os textos legais publicados, <paramref name="versaoTermos"/> e <paramref name="versaoPrivacidade"/> precisam ser as
    /// vigentes (F14-80) e o aceite é gravado na mesma transação (F14-81); sem eles publicados, são ignorados (F14-115,
    /// F14-116).
    /// O <paramref name="convite"/> é o token do convite de empresa (F14n, design D11): no modo convite
    /// (<c>Cadastro__Modo</c>), sem ele → <see cref="ArgumentException"/> no campo <c>convite</c> (F14-45). Presente, em
    /// qualquer modo: inexistente, vencido, usado ou revogado → o mesmo campo, sem distinguir (F14-45, F14-48); de outro
    /// e-mail → campo <c>email</c> (F14-49). Usado na mesma transação da empresa (F14-47), e qualquer recusa o deixa
    /// valendo (F14-105). Pelo convite, a conta nasce com o e-mail confirmado e a empresa liberada para enviar (o token
    /// chegou pelo e-mail), sem e-mail de confirmação.
    /// </summary>
    Task<Sessao> RegistrarAsync(
        string nome,
        string email,
        string senha,
        string nomeEmpresa,
        string? documento,
        string? versaoTermos = null,
        string? versaoPrivacidade = null,
        string? convite = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// O convite de empresa do token, para a tela de cadastro (F14-46): o e-mail dele e se já tem conta. Nulo se o token não
    /// vale (inexistente, vencido, usado ou revogado), sem dizer o motivo.
    /// </summary>
    Task<ConviteEmpresaInfo?> ConsultarConviteEmpresaAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Nulo para credenciais inválidas, conta bloqueada ou usuário sem vínculo, sem distinguir o motivo.</summary>
    Task<Sessao?> EntrarAsync(string email, string senha, CancellationToken cancellationToken = default);

    /// <summary>Troca o refresh token por uma sessão nova (rotação). Nulo se inválido; reuso revoga a família.</summary>
    Task<Sessao?> RenovarAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Abre sessão em outro tenant do usuário, revogando o refresh atual. Nulo se não há vínculo nele.</summary>
    Task<Sessao?> TrocarTenantAsync(Guid usuarioId, Guid tenantId, string? refreshAtual, CancellationToken cancellationToken = default);

    /// <summary>Revoga o refresh token, se existir e estiver ativo.</summary>
    Task SairAsync(string? refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Dados do convite do portal para a tela de aceite. Nulo se o token não vale (sem dizer o motivo).</summary>
    Task<ConvitePortalInfo?> ConsultarConviteAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aceita o convite do portal e abre a sessão no tenant do prestador. E-mail sem conta: cria o usuário com
    /// <paramref name="nome"/> (ou o nome do contato) e a senha. Com conta: a senha precisa ser a dela. Nulo se o token não
    /// vale; senha errada ou fraca → <see cref="ArgumentException"/> no campo.
    /// </summary>
    Task<Sessao?> AceitarConviteAsync(string token, string? nome, string senha, CancellationToken cancellationToken = default);

    /// <summary>
    /// Troca o token de um link de acesso (RF-POR-08) por uma sessão restrita à solicitação, sem refresh. Nulo se o token
    /// não vale (inexistente, expirado, revogado, contato ou cliente inativo), sem distinguir.
    /// </summary>
    Task<SessaoLink?> TrocarLinkAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Dados do convite para a equipe (tela de aceite). Nulo se o token não vale.</summary>
    Task<ConviteEquipeInfo?> ConsultarConviteEquipeAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aceita o convite para a equipe e abre a sessão no tenant que convidou: e-mail sem conta cria o usuário (nome
    /// obrigatório); com conta, a senha precisa ser a dela. Nulo se o token não vale; e-mail que já tem vínculo no tenant
    /// → regra de negócio (422). O aceite dos textos segue a regra do <see cref="RegistrarAsync"/> (F14-82, F14-117).
    /// </summary>
    Task<Sessao?> AceitarConviteEquipeAsync(
        string token,
        string? nome,
        string senha,
        string? versaoTermos = null,
        string? versaoPrivacidade = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// "Ciente" dos textos legais (F14-83, F14-119): grava a versão vigente de cada texto pendente da conta, com a data;
    /// nada pendente, nada a fazer. Com os textos não publicados → <see cref="Common.NaoEncontradoException"/>, sem gravar
    /// (F14-118).
    /// </summary>
    Task RegistrarCienteAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma o e-mail pelo token do link (uso único, 48 h). Falso se o token não vale, sem distinguir o motivo.
    /// </summary>
    Task<bool> ConfirmarEmailAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Reenvia a confirmação (invalida o link anterior). E-mail já confirmado: nada a fazer.</summary>
    Task ReenviarConfirmacaoAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia o link de redefinição de senha (1 h; invalida o anterior) se o e-mail tiver conta. Não revela se tem: quem
    /// chama responde igual nos dois casos.
    /// </summary>
    Task SolicitarRedefinicaoSenhaAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Troca a senha pelo token do link, confirma o e-mail, desbloqueia a conta e revoga todas as sessões (todos os
    /// tenants). Falso se o token não vale; senha fraca → <see cref="ArgumentException"/> e o token continua valendo.
    /// </summary>
    Task<bool> RedefinirSenhaAsync(string token, string novaSenha, CancellationToken cancellationToken = default);

    /// <summary>Dados do <c>/auth/eu</c>, lidos do banco (não do token). Nulo se o vínculo não existe mais.</summary>
    Task<PerfilUsuario?> PerfilAsync(Guid usuarioId, Guid tenantId, CancellationToken cancellationToken = default);
}
