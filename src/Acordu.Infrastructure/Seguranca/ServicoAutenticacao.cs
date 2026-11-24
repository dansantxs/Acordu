using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Autenticacao;
using Acordu.Application.Cadastro;
using Acordu.Application.Common;
using Acordu.Application.Empresas;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence;
using Acordu.Infrastructure.Persistence.Aceites;
using Acordu.Infrastructure.Persistence.Auditoria;
using Acordu.Infrastructure.Persistence.ConvitesEmpresa;
using Acordu.Infrastructure.Persistence.Documentos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Acordu.Infrastructure.Seguranca;

/// <summary>
/// Registro, login e sessões sobre o Identity (segurança §4–5). Login e renovação acontecem sem tenant no contexto,
/// então os vínculos e papéis são lidos ignorando os filtros — sempre restritos ao usuário em questão.
/// </summary>
public sealed partial class ServicoAutenticacao : IServicoAutenticacao
{
    public static readonly TimeSpan ValidadeRefresh = TimeSpan.FromDays(30);

    private static readonly string[] Filtros = [FiltrosGlobais.Tenant, FiltrosGlobais.Cliente];

    public static readonly TimeSpan ValidadeConfirmacao = TimeSpan.FromHours(48);
    public static readonly TimeSpan ValidadeRedefinicao = TimeSpan.FromHours(1);

    private readonly AcorduDbContext _db;
    private readonly UserManager<Usuario> _usuarios;
    private readonly ContextoTenant _contexto;
    private readonly TimeProvider _relogio;
    private readonly IEnviadorEmail _email;
    private readonly EnderecosPublicos _enderecos;
    private readonly ILogger<ServicoAutenticacao> _logger;
    private readonly IOrigemRequisicao _origem;
    private readonly OpcoesEmpresas _empresas;
    private readonly RegistroDocumentos _documentos;
    private readonly OpcoesNebrys _nebrys;
    private readonly RegistroAceites _aceites;
    private readonly OpcoesTextosLegais _textos;
    private readonly OpcoesCadastro _cadastro;
    private readonly RegistroConvitesEmpresa _convitesEmpresa;
    private readonly IDominiosDescartaveis _descartaveis;

    public ServicoAutenticacao(
        AcorduDbContext db,
        UserManager<Usuario> usuarios,
        ContextoTenant contexto,
        TimeProvider relogio,
        IEnviadorEmail email,
        EnderecosPublicos enderecos,
        ILogger<ServicoAutenticacao> logger,
        IOrigemRequisicao origem,
        OpcoesEmpresas empresas,
        RegistroDocumentos documentos,
        OpcoesNebrys nebrys,
        RegistroAceites aceites,
        OpcoesTextosLegais textos,
        OpcoesCadastro cadastro,
        RegistroConvitesEmpresa convitesEmpresa,
        IDominiosDescartaveis descartaveis)
    {
        ArgumentNullException.ThrowIfNull(descartaveis);
        _descartaveis = descartaveis;
        ArgumentNullException.ThrowIfNull(cadastro);
        ArgumentNullException.ThrowIfNull(convitesEmpresa);
        _cadastro = cadastro;
        _convitesEmpresa = convitesEmpresa;
        ArgumentNullException.ThrowIfNull(aceites);
        ArgumentNullException.ThrowIfNull(textos);
        _aceites = aceites;
        _textos = textos;
        ArgumentNullException.ThrowIfNull(empresas);
        ArgumentNullException.ThrowIfNull(documentos);
        ArgumentNullException.ThrowIfNull(nebrys);
        _empresas = empresas;
        _documentos = documentos;
        _nebrys = nebrys;
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(usuarios);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(enderecos);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(origem);
        _origem = origem;
        _db = db;
        _usuarios = usuarios;
        _contexto = contexto;
        _relogio = relogio;
        _email = email;
        _enderecos = enderecos;
        _logger = logger;
    }

    public async Task<Sessao> RegistrarAsync(
        string nome,
        string email,
        string senha,
        string nomeEmpresa,
        string? documento,
        string? versaoTermos = null,
        string? versaoPrivacidade = null,
        string? convite = null,
        CancellationToken cancellationToken = default)
    {
        // Primeiro o que não consulta o banco; depois o convite, a conta (e-mail em uso) e o documento (design do F14, §1.2).
        var nomeValido = Obrigatorio(nome, Usuario.TamanhoNome, nameof(nome));
        var empresaValida = Obrigatorio(nomeEmpresa, LimitesTexto.Nome, nameof(nomeEmpresa));
        var emailValido = Obrigatorio(email, LimitesTexto.Email, nameof(email));
        // Mesmo formato dos contatos e convites (sem acento antes do @): a confirmação precisa chegar.
        var emailNormalizado = Domain.Common.Email.Normalizar(emailValido, nameof(email));
        // E-mail de domínio descartável (RF-AUT-10, F14-60; design D9): recusado sem consultar o banco, antes do convite,
        // com ou sem ele e em qualquer modo.
        MensagensDeCadastro.RecusarDescartavel(_descartaveis, emailNormalizado, MensagensDeCadastro.CampoEmail);
        if (string.IsNullOrEmpty(senha))
            throw new ArgumentException("A senha é obrigatória.", nameof(senha));
        // CPF ou CNPJ obrigatório no cadastro (RF-AUT-09, F14-56), normalizado pelo domínio: com e sem pontuação, e o CNPJ
        // alfanumérico em qualquer caixa, viram o mesmo texto (F14-58).
        var documentoValido = Documento.Normalizar(documento, MensagensDeCadastro.CampoDocumento)
            ?? throw new ArgumentException(MensagensDeCadastro.DocumentoObrigatorio, MensagensDeCadastro.CampoDocumento);
        // Aceite dos textos legais (RF-AUT-11, F14-80): só com os textos publicados (D16); sem eles, o que vier é ignorado e
        // nada é gravado (F14-115, F14-116). Ainda sem consultar o banco.
        var gravarAceite = AceiteDosTextos.Conferir(_textos, versaoTermos, versaoPrivacidade);
        // Modo do cadastro (RF-AUT-08, design D11): no modo convite, sem convite é recusado sem consultar o banco (F14-45).
        // Presente, o convite vale em qualquer modo, com o e-mail dele (F14-47, F14-49).
        var tokenConvite = string.IsNullOrWhiteSpace(convite) ? null : convite.Trim();
        if (tokenConvite is null && _cadastro.Modo == ModoCadastro.Convite)
            throw ErroDeCampo.Criar(MensagensDeCadastro.CampoConvite, MensagensDeCadastro.SoPorConvite(_nebrys));

        var tenantId = Guid.CreateVersion7();
        var agora = _relogio.GetUtcNow();
        _contexto.DefinirEquipe(tenantId);

        await using var transacao = await _db.Database.BeginTransactionAsync(cancellationToken);
        // O convite antes da conta: quem não tem um convite que vale não descobre se o e-mail tem conta. O convite válido
        // para um e-mail que já tem conta cai na recusa do e-mail em uso, logo abaixo, sem ser usado (F14-105).
        if (tokenConvite is not null)
            await ConferirConviteAsync(tokenConvite, emailNormalizado, agora, cancellationToken);

        // Conta de quem chegou pelo convite: o token veio por este e-mail, então ele já nasce confirmado, como nos convites
        // da equipe e do portal (segurança §4); a empresa sai liberada para enviar (RF-AUT-06) e não recebe a confirmação.
        var usuario = new Usuario(emailValido, nomeValido) { EmailConfirmed = tokenConvite is not null };
        IdentityResult criacao;
        try
        {
            criacao = await _usuarios.CreateAsync(usuario, senha);
        }
        catch (DbUpdateException excecao) when (excecao.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dois cadastros com o mesmo e-mail ao mesmo tempo (como os que chegam com o mesmo convite, F14-48): o índice
            // único da conta barra o segundo, que recebe a mesma recusa do e-mail em uso, sem nada criado.
            throw new ArgumentException("Este e-mail já tem cadastro.", nameof(email));
        }

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

        // Um teste por documento (RF-AUT-09): o registro global entra na mesma transação da empresa (design do F14, D8).
        // O MESMO `agora` vai ao registro (PrimeiroTesteEm) e à empresa (CriadoEm): é por essa igualdade que o expurgo do
        // cadastro abandonado acha a linha que este cadastro criou (RegistroDocumentos.DesfazerTesteDoCadastroAsync,
        // F14-104). Não troque um dos dois por outro instante; o teste
        // ExpurgoEmpresasTests.Registro_DocumentoNovo_RegistroEEmpresaComOMesmoInstante confere.
        if (!await _documentos.ReservarTesteAsync(documentoValido, agora, cancellationToken))
            throw DocumentoJaTeveTeste();

        // O convite fica usado na mesma transação da empresa (F14-47): recusa depois daqui o deixa valendo (F14-49, F14-105).
        if (tokenConvite is not null && !await _convitesEmpresa.UsarAsync(tokenConvite, emailNormalizado, agora, cancellationToken))
            throw ConviteInvalido();

        var papeis = Papel.Padroes(tenantId);
        var dono = papeis.Single(p => p.Nome == Papel.NomeDono);
        var vinculo = Vinculo.Criar(Guid.CreateVersion7(), usuario.Id, dono);
        // Nasce em TESTE, com o fim do teste no cadastro mais a duração da configuração (F14-01).
        _db.Add(Tenant.Criar(tenantId, empresaValida, agora, usuario.Id, _empresas.DuracaoDoTeste, documentoValido));
        _db.AddRange(papeis);
        _db.Add(vinculo);
        _db.Add(CalendarioUtil.Padrao(tenantId));
        // A versão de cada texto e a data, na mesma transação: recusa depois daqui não deixa aceite de conta que não existe.
        if (gravarAceite)
            await _aceites.RegistrarVigentesAsync(usuario.Id, FormaAceiteTexto.Aceite, agora, cancellationToken);

        var sessao = Abrir(usuario, vinculo, dono, agora);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException excecao) when (RegistroDocumentos.Disputa(excecao))
        {
            // Outro cadastro com o mesmo documento passou na frente: a transação volta inteira (nada criado).
            throw DocumentoJaTeveTeste();
        }
        catch (DbUpdateException excecao) when (RegistroConvitesEmpresa.Disputa(excecao))
        {
            // Outro cadastro usou o mesmo convite, ou a Nebrys o reenviou ou revogou, no meio: nada criado (F14-48).
            throw ConviteInvalido();
        }

        if (tokenConvite is not null)
            await LiberarEmpresasCriadasAsync(usuario.Id, agora, cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        if (tokenConvite is null)
            await EnviarConfirmacaoAsync(usuario, cancellationToken);
        return sessao;
    }

    private ArgumentException DocumentoJaTeveTeste() =>
        new(MensagensDeCadastro.DocumentoJaTeveTeste(_nebrys), MensagensDeCadastro.CampoDocumento);

    private ArgumentException ConviteInvalido() =>
        ErroDeCampo.Criar(MensagensDeCadastro.CampoConvite, MensagensDeCadastro.ConviteInvalido(_nebrys));

    /// <summary>
    /// O convite de empresa do cadastro (F14-45, F14-48, F14-49): inexistente, vencido, usado ou revogado → <c>400</c> no
    /// campo <c>convite</c>, sem distinguir; de outro e-mail → <c>400</c> no <c>email</c>, sem dizer qual é o do convite.
    /// </summary>
    private async Task ConferirConviteAsync(string token, string emailNormalizado, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var conferencia = await _convitesEmpresa.ConferirAsync(token, emailNormalizado, agora, cancellationToken);
        if (conferencia == ConferenciaConvite.Invalido)
            throw ConviteInvalido();
        if (conferencia == ConferenciaConvite.OutroEmail)
            throw ErroDeCampo.Criar(MensagensDeCadastro.CampoEmail, MensagensDeCadastro.EmailDiferenteDoConvite);
    }

    public async Task<ConviteEmpresaInfo?> ConsultarConviteEmpresaAsync(string token, CancellationToken cancellationToken = default)
    {
        var email = await _convitesEmpresa.EmailDoConviteValidoAsync(token, _relogio.GetUtcNow(), cancellationToken);
        if (email is null)
            return null;
        return new ConviteEmpresaInfo(email, await _usuarios.FindByEmailAsync(email) is not null);
    }

    public async Task<Sessao?> EntrarAsync(string email, string senha, CancellationToken cancellationToken = default)
    {
        var usuario = string.IsNullOrWhiteSpace(email) ? null : await _usuarios.FindByEmailAsync(email.Trim());
        if (usuario is null)
            return await FalhaNoLoginAsync(null, email, "email-desconhecido", cancellationToken);
        if (string.IsNullOrEmpty(senha))
            return await FalhaNoLoginAsync(usuario, email, "senha", cancellationToken);
        if (await _usuarios.IsLockedOutAsync(usuario))
            return await FalhaNoLoginAsync(usuario, email, "bloqueado", cancellationToken);
        if (!await _usuarios.CheckPasswordAsync(usuario, senha))
        {
            await _usuarios.AccessFailedAsync(usuario);
            return await FalhaNoLoginAsync(usuario, email, "senha", cancellationToken);
        }

        await _usuarios.ResetAccessFailedCountAsync(usuario);
        var vinculos = await _db.Vinculos.IgnoreQueryFilters(Filtros)
            .Where(v => v.UsuarioId == usuario.Id)
            .OrderBy(v => v.Id) // Ids v7: ordem de criação
            .ToListAsync(cancellationToken);
        var vinculo = vinculos.FirstOrDefault(v => v.TenantId == usuario.UltimoTenantId) ?? vinculos.FirstOrDefault();
        if (vinculo is null)
            return await FalhaNoLoginAsync(usuario, email, "sem-vinculo", cancellationToken);

        var agora = _relogio.GetUtcNow();
        var sessao = Abrir(usuario, vinculo, await PapelAsync(vinculo, cancellationToken), agora);
        // Sucesso fica no tenant da sessão emitida: o Dono dele vê (segurança §8).
        _db.Add(RegistroAuditoria.Criar(
            vinculo.TenantId, usuario.Id, AcoesAuditoria.LoginSucesso, null, null, null, _origem.Ip, agora));
        await _db.SaveChangesAsync(cancellationToken);
        return sessao;
    }

    /// <summary>
    /// Login recusado: auditado sem tenant (só a operação vê), com o e-mail digitado e o motivo; a resposta continua
    /// sem distinguir o motivo.
    /// </summary>
    private async Task<Sessao?> FalhaNoLoginAsync(Usuario? usuario, string? email, string motivo, CancellationToken cancellationToken)
    {
        var digitado = email?.Trim() ?? string.Empty;
        if (digitado.Length > LimitesTexto.Email)
            digitado = digitado[..LimitesTexto.Email];
        _db.Add(RegistroAuditoria.Criar(
            null,
            usuario?.Id,
            AcoesAuditoria.LoginFalha,
            null,
            null,
            TrilhaDeAuditoria.Serializar(new { email = digitado, motivo }),
            _origem.Ip,
            _relogio.GetUtcNow()));
        await _db.SaveChangesAsync(cancellationToken);
        return null;
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

    public async Task<ConvitePortalInfo?> ConsultarConviteAsync(string token, CancellationToken cancellationToken = default)
    {
        var convite = await ConviteValidoAsync(token, cancellationToken);
        if (convite is null)
            return null;

        var temConta = await _usuarios.FindByEmailAsync(convite.Contato.Email) is not null;
        return new ConvitePortalInfo(
            convite.Tenant.NomePortal ?? convite.Tenant.Nome, convite.Contato.Nome, convite.Contato.Email, temConta);
    }

    public async Task<Sessao?> AceitarConviteAsync(
        string token, string? nome, string senha, CancellationToken cancellationToken = default)
    {
        var dados = await ConviteValidoAsync(token, cancellationToken);
        if (dados is null)
            return null;
        if (string.IsNullOrEmpty(senha))
            throw new ArgumentException("A senha é obrigatória.", nameof(senha));

        var (convite, cliente, contato, _) = dados;
        var agora = _relogio.GetUtcNow();
        _contexto.DefinirEquipe(convite.TenantId);

        // Conta existente: a senha é conferida antes da transação, para a contagem de falhas (lockout) ficar gravada.
        var usuario = await _usuarios.FindByEmailAsync(contato.Email);
        if (usuario is not null)
            await ConferirSenhaAsync(usuario, senha);

        await using var transacao = await _db.Database.BeginTransactionAsync(cancellationToken);
        usuario ??= await CriarUsuarioDoConviteAsync(
            contato.Email, string.IsNullOrWhiteSpace(nome) ? contato.Nome : nome, senha);

        if (await _db.Vinculos.AnyAsync(v => v.UsuarioId == usuario.Id, cancellationToken))
            throw new AcessoPortalException("Este e-mail já tem acesso a este prestador, pela equipe ou por outro cliente.");

        var papel = await _db.Papeis.SingleAsync(p => p.Tipo == TipoPapel.Cliente && p.Padrao, cancellationToken);
        var vinculo = Vinculo.Criar(Guid.CreateVersion7(), usuario.Id, papel, cliente.Id, convite.Escopo);
        _db.Add(vinculo);
        cliente.VincularUsuarioAoContato(contato.Id, usuario.Id);
        convite.Aceitar(token, usuario.Id, agora);

        var sessao = Abrir(usuario, vinculo, papel, agora);
        await _db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return sessao;
    }

    public async Task<ConviteEquipeInfo?> ConsultarConviteEquipeAsync(string token, CancellationToken cancellationToken = default)
    {
        var convite = await ConviteEquipeValidoAsync(token, cancellationToken);
        if (convite is null)
            return null;

        var temConta = await _usuarios.FindByEmailAsync(convite.Convite.Email) is not null;
        return new ConviteEquipeInfo(convite.Tenant.Nome, convite.Convite.Email, convite.Papel.Nome, temConta);
    }

    public async Task<Sessao?> AceitarConviteEquipeAsync(
        string token,
        string? nome,
        string senha,
        string? versaoTermos = null,
        string? versaoPrivacidade = null,
        CancellationToken cancellationToken = default)
    {
        var dados = await ConviteEquipeValidoAsync(token, cancellationToken);
        if (dados is null)
            return null;
        if (string.IsNullOrEmpty(senha))
            throw new ArgumentException("A senha é obrigatória.", nameof(senha));
        // Aceite dos textos, como no cadastro (F14-82): antes de conferir a senha, para a recusa não contar no bloqueio.
        // Não publicados, ignorado (F14-117).
        var gravarAceite = AceiteDosTextos.Conferir(_textos, versaoTermos, versaoPrivacidade);

        var (convite, papel, _) = dados;
        var agora = _relogio.GetUtcNow();
        _contexto.DefinirEquipe(convite.TenantId);

        // Conta existente: a senha é conferida antes da transação, para a contagem de falhas (lockout) ficar gravada.
        var usuario = await _usuarios.FindByEmailAsync(convite.Email);
        if (usuario is not null)
            await ConferirSenhaAsync(usuario, senha);

        await using var transacao = await _db.Database.BeginTransactionAsync(cancellationToken);
        if (usuario is null)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new ArgumentException("Informe seu nome.", nameof(nome));
            usuario = await CriarUsuarioDoConviteAsync(convite.Email, nome, senha);
        }

        if (await _db.Vinculos.AnyAsync(v => v.UsuarioId == usuario.Id, cancellationToken))
            throw new EquipeException("Este e-mail já tem acesso a este prestador, pela equipe ou como contato de um cliente.");

        var vinculo = Vinculo.Criar(Guid.CreateVersion7(), usuario.Id, papel);
        _db.Add(vinculo);
        convite.Aceitar(token, usuario.Id, agora);
        // Conta nova ou existente sem a versão vigente: grava o aceite na mesma transação do vínculo (F14-82); a que já tem
        // a vigente fica como está.
        if (gravarAceite)
            await _aceites.RegistrarVigentesAsync(usuario.Id, FormaAceiteTexto.Aceite, agora, cancellationToken);

        var sessao = Abrir(usuario, vinculo, papel, agora);
        await _db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return sessao;
    }

    public async Task<SessaoLink?> TrocarLinkAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var hash = AcessoPortal.HashDoToken(token);
        var acesso = await _db.AcessosPortal.IgnoreQueryFilters(Filtros).AsNoTracking()
            .SingleOrDefaultAsync(a => a.TokenHash == hash, cancellationToken);
        if (acesso is null || !acesso.Validar(token, _relogio.GetUtcNow()))
            return null;

        var contatoAtivo = await _db.Clientes.IgnoreQueryFilters(Filtros).AsNoTracking()
            .Where(c => c.Id == acesso.ClienteId && c.TenantId == acesso.TenantId && c.Ativo)
            .SelectMany(c => c.Contatos)
            .AnyAsync(c => c.Id == acesso.ContatoId && c.Ativo, cancellationToken);
        if (!contatoAtivo)
            return null;

        _db.Add(RegistroAuditoria.Criar(
            acesso.TenantId,
            null,
            AcoesAuditoria.LinkPortalTroca,
            "solicitacao",
            acesso.SolicitacaoId,
            TrilhaDeAuditoria.Serializar(new { acessoPortalId = acesso.Id, contatoId = acesso.ContatoId }),
            _origem.Ip,
            _relogio.GetUtcNow()));
        await _db.SaveChangesAsync(cancellationToken);
        return new SessaoLink(acesso.TenantId, acesso.ClienteId, acesso.Id, acesso.SolicitacaoId);
    }

    public async Task<bool> ConfirmarEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        var registro = await TokenValidoAsync(token, FinalidadeTokenConta.ConfirmacaoEmail, cancellationToken);
        var usuario = registro is null ? null : await _usuarios.FindByIdAsync(registro.UsuarioId.ToString());
        if (registro is null || usuario is null)
            return false;

        var agora = _relogio.GetUtcNow();
        await using var transacao = await _db.Database.BeginTransactionAsync(cancellationToken);
        registro.Usar(agora);
        usuario.EmailConfirmed = true;
        await LiberarEmpresasCriadasAsync(usuario.Id, agora, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// O e-mail do criador ficou confirmado: a empresa dele passa a enviar a terceiros (RF-AUT-06; spec do F9, decisão 2).
    /// Atualização direta, fora do rastreamento: a rota é pública (ou o contexto é de outra empresa do usuário), e a
    /// condição já restringe às empresas que ele criou e que ainda não foram liberadas, o que a torna idempotente.
    /// </summary>
    private async Task LiberarEmpresasCriadasAsync(Guid usuarioId, DateTimeOffset agora, CancellationToken cancellationToken) =>
        await _db.Tenants.IgnoreQueryFilters(Filtros)
            .Where(t => t.CriadoPorUsuarioId == usuarioId && t.EnvioLiberadoEm == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.EnvioLiberadoEm, (DateTimeOffset?)agora), cancellationToken);

    public async Task ReenviarConfirmacaoAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString());
        if (usuario is { EmailConfirmed: false })
            await EnviarConfirmacaoAsync(usuario, cancellationToken);
    }

    public async Task SolicitarRedefinicaoSenhaAsync(string email, CancellationToken cancellationToken = default)
    {
        var usuario = string.IsNullOrWhiteSpace(email) ? null : await _usuarios.FindByEmailAsync(email.Trim());
        if (usuario is null)
            return;

        var token = await EmitirTokenAsync(usuario.Id, FinalidadeTokenConta.RedefinicaoSenha, ValidadeRedefinicao, cancellationToken);
        await EnviarAsync(MensagensDeConta.Redefinicao(
            usuario.Email!, usuario.Nome, _enderecos.RedefinicaoSenha(token), ValidadeRedefinicao), cancellationToken);
    }

    public async Task<bool> RedefinirSenhaAsync(string token, string novaSenha, CancellationToken cancellationToken = default)
    {
        var registro = await TokenValidoAsync(token, FinalidadeTokenConta.RedefinicaoSenha, cancellationToken);
        var usuario = registro is null ? null : await _usuarios.FindByIdAsync(registro.UsuarioId.ToString());
        if (registro is null || usuario is null)
            return false;

        // Senha validada antes de consumir o token: senha fraca deixa o link valendo para nova tentativa.
        if (string.IsNullOrEmpty(novaSenha))
            throw new ArgumentException("A nova senha é obrigatória.", nameof(novaSenha));
        foreach (var validador in _usuarios.PasswordValidators)
        {
            if (!(await validador.ValidateAsync(_usuarios, usuario, novaSenha)).Succeeded)
                throw new ArgumentException($"A senha precisa de ao menos {OpcoesIdentidade.TamanhoMinimoSenha} caracteres.", nameof(novaSenha));
        }

        var agora = _relogio.GetUtcNow();
        await using var transacao = await _db.Database.BeginTransactionAsync(cancellationToken);
        usuario.PasswordHash = _usuarios.PasswordHasher.HashPassword(usuario, novaSenha);
        usuario.EmailConfirmed = true; // o link chegou pelo e-mail
        await LiberarEmpresasCriadasAsync(usuario.Id, agora, cancellationToken);
        usuario.AccessFailedCount = 0;
        usuario.LockoutEnd = null;
        await _usuarios.UpdateSecurityStampAsync(usuario);
        registro.Usar(agora);
        var sessoes = await _db.Set<RefreshToken>()
            .Where(r => r.UsuarioId == usuario.Id && r.ConsumidoEm == null && r.RevogadoEm == null)
            .ToListAsync(cancellationToken);
        foreach (var sessao in sessoes)
            sessao.Revogar(agora);
        await _db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return true;
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
        // Pendentes só para a equipe: o contato do portal nunca recebe o aviso (F14-120, decisão 12 do G2).
        IReadOnlyList<TextoPendente> pendentes = ativo.ClienteId is null
            ? await _aceites.PendentesAsync(usuarioId, cancellationToken)
            : [];

        return new PerfilUsuario(
            usuario.Id,
            usuario.Nome,
            usuario.Email ?? string.Empty,
            usuario.EmailConfirmed,
            new TenantAtivo(
                tenant.Id,
                tenant.Nome,
                fuso,
                tenant.EnvioLiberado,
                tenant.Situacao,
                tenant.FimDoTeste,
                tenant.SomenteLeitura ? tenant.SomenteLeituraDesde : null,
                tenant.ExpurgoPrevistoEm(_empresas.PrazoAteExpurgo),
                _empresas.DiasFaixaTeste),
            papel.Nome,
            [.. papel.Permissoes],
            ativo.ClienteId,
            ativo.EscopoPortal,
            [.. vinculos
                .Where(v => tenants.ContainsKey(v.TenantId) && papeis.ContainsKey(v.PapelId))
                .Select(v => new TenantDoUsuario(v.TenantId, tenants[v.TenantId].Nome, papeis[v.PapelId].Nome))],
            pendentes);
    }

    public async Task RegistrarCienteAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        // Sem os textos publicados, não há aviso nem página para ler: a rota responde como inexistente (F14-118, D16).
        if (!_textos.Publicados)
            throw new NaoEncontradoException("Recurso não encontrado.");

        // A conta, não a empresa: segue numa empresa só leitura (decisão 10 do G2) e vale em todas as empresas dela.
        await _aceites.RegistrarVigentesAsync(usuarioId, FormaAceiteTexto.Ciente, _relogio.GetUtcNow(), cancellationToken);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException excecao) when (RegistroAceites.Duplicado(excecao))
        {
            // Dois "ciente" ao mesmo tempo (duas abas): o outro já gravou a mesma versão.
        }
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

    /// <summary>
    /// Convite pelo hash do token, válido, com cliente e contato ativos (revogar ou inativar já revoga o convite; aqui é
    /// a segunda barreira). Rastreado, para o aceite gravar. Nulo em qualquer falha, sem distinguir o motivo.
    /// </summary>
    private async Task<ConviteCarregado?> ConviteValidoAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var hash = TokenSeguro.Hash(token);
        var convite = await _db.ConvitesPortal.IgnoreQueryFilters(Filtros)
            .SingleOrDefaultAsync(c => c.TokenHash == hash, cancellationToken);
        if (convite is null || !convite.Validar(token, _relogio.GetUtcNow()))
            return null;

        var cliente = await _db.Clientes.IgnoreQueryFilters(Filtros)
            .SingleOrDefaultAsync(c => c.Id == convite.ClienteId && c.TenantId == convite.TenantId, cancellationToken);
        var contato = cliente?.Contatos.FirstOrDefault(c => c.Id == convite.ContatoId);
        if (cliente is null || contato is null || !cliente.Ativo || !contato.Ativo)
            return null;

        var tenant = await _db.Tenants.IgnoreQueryFilters(Filtros).SingleAsync(t => t.Id == convite.TenantId, cancellationToken);
        return new ConviteCarregado(convite, cliente, contato, tenant);
    }

    /// <summary>
    /// Convite para a equipe pelo hash do token, válido e com o papel ainda existente. Rastreado, para o aceite gravar.
    /// Nulo em qualquer falha, sem distinguir o motivo.
    /// </summary>
    private async Task<ConviteEquipeCarregado?> ConviteEquipeValidoAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var hash = TokenSeguro.Hash(token);
        var convite = await _db.ConvitesEquipe.IgnoreQueryFilters(Filtros)
            .SingleOrDefaultAsync(c => c.TokenHash == hash, cancellationToken);
        if (convite is null || !convite.Validar(token, _relogio.GetUtcNow()))
            return null;

        var papel = await _db.Papeis.IgnoreQueryFilters(Filtros)
            .SingleOrDefaultAsync(p => p.Id == convite.PapelId && p.TenantId == convite.TenantId, cancellationToken);
        if (papel is null)
            return null;

        var tenant = await _db.Tenants.IgnoreQueryFilters(Filtros).SingleAsync(t => t.Id == convite.TenantId, cancellationToken);
        return new ConviteEquipeCarregado(convite, papel, tenant);
    }

    /// <summary>Conta criada no aceite de um convite: o token chegou por e-mail, então o e-mail já nasce confirmado.</summary>
    private async Task<Usuario> CriarUsuarioDoConviteAsync(string email, string nome, string senha)
    {
        var usuario = new Usuario(email, Obrigatorio(nome, Usuario.TamanhoNome, nameof(nome))) { EmailConfirmed = true };
        var criacao = await _usuarios.CreateAsync(usuario, senha);
        if (!criacao.Succeeded)
        {
            throw ErroDeRegistro(criacao) == ErroRegistro.Senha
                ? new ArgumentException($"A senha precisa de ao menos {OpcoesIdentidade.TamanhoMinimoSenha} caracteres.", nameof(senha))
                : new InvalidOperationException("Não foi possível criar a conta: " + string.Join(", ", criacao.Errors.Select(e => e.Code)));
        }

        return usuario;
    }

    /// <summary>Gera um token da conta (revogando os anteriores da mesma finalidade) e grava o hash.</summary>
    private async Task<string> EmitirTokenAsync(
        Guid usuarioId, FinalidadeTokenConta finalidade, TimeSpan validade, CancellationToken cancellationToken)
    {
        var agora = _relogio.GetUtcNow();
        var anteriores = await _db.Set<TokenConta>()
            .Where(t => t.UsuarioId == usuarioId && t.Finalidade == finalidade && t.UsadoEm == null && t.RevogadoEm == null)
            .ToListAsync(cancellationToken);
        foreach (var anterior in anteriores)
            anterior.Revogar(agora);

        var (registro, token) = TokenConta.Emitir(usuarioId, finalidade, agora, validade);
        _db.Add(registro);
        await _db.SaveChangesAsync(cancellationToken);
        return token;
    }

    private async Task<TokenConta?> TokenValidoAsync(string? token, FinalidadeTokenConta finalidade, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var hash = TokenSeguro.Hash(token);
        var registro = await _db.Set<TokenConta>().SingleOrDefaultAsync(t => t.Hash == hash, cancellationToken);
        return registro is not null && registro.Finalidade == finalidade && registro.Valido(token, _relogio.GetUtcNow())
            ? registro
            : null;
    }

    private async Task EnviarConfirmacaoAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        var token = await EmitirTokenAsync(usuario.Id, FinalidadeTokenConta.ConfirmacaoEmail, ValidadeConfirmacao, cancellationToken);
        await EnviarAsync(MensagensDeConta.Confirmacao(
            usuario.Email!, usuario.Nome, _enderecos.ConfirmacaoEmail(token), ValidadeConfirmacao), cancellationToken);
    }

    /// <summary>
    /// E-mails da conta saem na própria requisição, depois do commit: são pedidos do próprio usuário, repetíveis (reenvio,
    /// novo pedido) e sem tenant para a outbox. Falha no envio fica no log e não muda a resposta.
    /// </summary>
    private async Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancellationToken)
    {
        try
        {
            await _email.EnviarAsync(mensagem, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            FalhaNoEmail(_logger, mensagem.Categoria, ex);
        }
    }

    [LoggerMessage(EventId = 300, Level = LogLevel.Warning, Message = "Falha ao enviar o e-mail '{Categoria}' da conta.")]
    private static partial void FalhaNoEmail(ILogger logger, string categoria, Exception excecao);

    private async Task ConferirSenhaAsync(Usuario usuario, string senha)
    {
        if (await _usuarios.IsLockedOutAsync(usuario))
            throw new ArgumentException("Conta bloqueada por tentativas de senha. Tente de novo em alguns minutos.", nameof(senha));
        if (!await _usuarios.CheckPasswordAsync(usuario, senha))
        {
            await _usuarios.AccessFailedAsync(usuario);
            throw new ArgumentException("Senha incorreta. Este e-mail já tem conta no Acordu: use a senha dela.", nameof(senha));
        }

        await _usuarios.ResetAccessFailedCountAsync(usuario);
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

    private sealed record ConviteEquipeCarregado(ConviteEquipe Convite, Papel Papel, Tenant Tenant);

    private sealed record ConviteCarregado(ConvitePortal Convite, Cliente Cliente, Contato Contato, Tenant Tenant);

    private enum ErroRegistro
    {
        Senha = 1,
        EmailEmUso = 2,
        EmailInvalido = 3,
    }
}
