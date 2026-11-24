using System.Data;
using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Common;
using Acordu.Application.Exemplos;
using Acordu.Application.Nebrys;
using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence.Auditoria;
using Acordu.Infrastructure.Persistence.Cadastros;
using Acordu.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.Persistence.Demonstracao;

/// <summary>
/// A empresa de demonstração (RF-NEB-02; F14-87 a F14-90, F14-110; design do F14, D13), pela área da Nebrys. A sessão de
/// operação não tem tenant e o escopo da requisição continua vazio (ADR 0014, item 3): cada ação que grava roda num escopo
/// próprio com o tenant da demonstração fixado, como o <c>SituacaoDasEmpresas</c>, com as guardas e os filtros valendo, e
/// grava a linha da trilha sem tenant, com o operador, na mesma transação.
/// <list type="bullet">
/// <item>As contas fictícias (as pessoas e o contato do portal do <c>demonstracao.json</c>, em <c>.invalid</c>) nascem sem
/// senha: ninguém entra com elas. Uma conta com senha num desses e-mails nunca é reaproveitada.</item>
/// <item>Pessoa indicada: conta de outra pessoa (com senha) que tem vínculo de equipe na demonstração. A indicação só
/// aceita conta que já existe (decisão 17 do G2): nada é criado e nenhum e-mail sai.</item>
/// <item>Nada vai à outbox: os eventos das raízes criadas são descartados (<see cref="SemeaduraDaDemonstracao"/>), e a
/// gravação é pelo <c>SaveChanges</c>, sem a unidade de trabalho.</item>
/// </list>
/// Ponto revisado de <c>IgnoreQueryFilters</c> (convenções §1): a busca da demonstração e das pessoas indicadas, sem
/// contexto de empresa, lê só o <c>Tenant</c>, os vínculos dela e as contas.
/// </summary>
public sealed class EmpresaDeDemonstracao : IEmpresaDeDemonstracao
{
    private const string AlvoEmpresa = "empresa";
    private const string NaoExiste = "A empresa de demonstração ainda não foi criada.";

    private static readonly string[] Filtros = [FiltrosGlobais.Tenant, FiltrosGlobais.Cliente];

    private readonly IServiceScopeFactory _escopos;
    private readonly AcorduDbContext _db;
    private readonly ApagadorDeEmpresa _apagador;
    private readonly IOperadorAtual _operador;
    private readonly IOrigemRequisicao _origem;
    private readonly TimeProvider _relogio;

    public EmpresaDeDemonstracao(
        IServiceScopeFactory escopos,
        AcorduDbContext db,
        ApagadorDeEmpresa apagador,
        IOperadorAtual operador,
        IOrigemRequisicao origem,
        TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(apagador);
        ArgumentNullException.ThrowIfNull(operador);
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(relogio);
        _escopos = escopos;
        _db = db;
        _apagador = apagador;
        _operador = operador;
        _origem = origem;
        _relogio = relogio;
    }

    public async Task<DemonstracaoNebrysDto> ObterAsync(CancellationToken cancellationToken = default)
    {
        var demonstracao = await _db.Tenants.IgnoreQueryFilters(Filtros).AsNoTracking()
            .Where(t => t.Demonstracao)
            .OrderBy(t => t.CriadoEm)
            .Select(t => new { t.Id, t.Nome, t.CriadoEm })
            .FirstOrDefaultAsync(cancellationToken);
        if (demonstracao is null)
            return new DemonstracaoNebrysDto(null, null, null, []);

        return new DemonstracaoNebrysDto(
            demonstracao.Id, demonstracao.Nome, demonstracao.CriadoEm, await IndicadasAsync(_db, demonstracao.Id, cancellationToken));
    }

    public async Task<Guid> CriarAsync(CancellationToken cancellationToken = default)
    {
        var operador = _operador.Id;
        var dados = DadosFicticios.Arquivo;
        var id = Guid.CreateVersion7();
        await using var escopo = Escopo(id);
        var servicos = escopo.ServiceProvider;
        var db = servicos.GetRequiredService<AcorduDbContext>();
        // Serializável: duas criações ao mesmo tempo não deixam duas demonstrações (a segunda falha no commit).
        await using var transacao = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (await db.Tenants.IgnoreQueryFilters(Filtros).AnyAsync(t => t.Demonstracao, cancellationToken))
        {
            throw new TransicaoInvalidaException(
                "A empresa de demonstração já existe. Para voltar aos dados iniciais, restaure-a.");
        }

        var contas = await ContasFicticiasAsync(servicos, dados);
        var agora = _relogio.GetUtcNow();
        var tenant = Tenant.CriarDemonstracao(id, dados.Empresa.Nome, agora, contas.Dono);
        db.Add(tenant);
        var clientes = await servicos.GetRequiredService<SemeaduraDaDemonstracao>()
            .SemearAsync(dados, tenant, contas.Pessoas, contas.Portal, [], cancellationToken);

        // A empresa não existia: o antes é nulo (toda ação da área leva o antes e o depois, F14-70).
        db.Add(Trilha(
            operador,
            AcoesAuditoria.NebrysDemonstracaoCriacao,
            id,
            new { antes = (object?)null, depois = new { tenant.Nome, tenant.Situacao, clientes } },
            agora));
        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return id;
    }

    public async Task RestaurarAsync(CancellationToken cancellationToken = default)
    {
        var operador = _operador.Id;
        var dados = DadosFicticios.Arquivo;
        var id = await IdAsync(cancellationToken) ?? throw new NaoEncontradoException(NaoExiste);
        await using var escopo = Escopo(id);
        var servicos = escopo.ServiceProvider;
        var db = servicos.GetRequiredService<AcorduDbContext>();
        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Id == id && t.Demonstracao, cancellationToken)
            ?? throw new NaoEncontradoException(NaoExiste);
        var antes = new { tenant.Nome, clientes = await db.Clientes.CountAsync(cancellationToken) };
        // As pessoas indicadas continuam (F14-88): lidas antes de apagar os vínculos, voltam como Dono.
        var indicadas = (await IndicadasAsync(db, id, cancellationToken)).Select(p => p.UsuarioId).ToList();

        // Só o que tem o TenantId da demonstração sai: nenhuma outra empresa é tocada (F14-88).
        var arquivos = await ApagadorDeEmpresa.ApagarParaRestaurarAsync(db, tenant, cancellationToken);
        var contas = await ContasFicticiasAsync(servicos, dados);
        tenant.RestaurarDemonstracao(dados.Empresa.Nome);
        var clientes = await servicos.GetRequiredService<SemeaduraDaDemonstracao>()
            .SemearAsync(dados, tenant, contas.Pessoas, contas.Portal, indicadas, cancellationToken);

        var agora = _relogio.GetUtcNow();
        db.Add(Trilha(
            operador, AcoesAuditoria.NebrysDemonstracaoRestauracao, id, new { antes, depois = new { tenant.Nome, clientes } }, agora));
        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        await _apagador.RemoverArquivosAsync(servicos, id, arquivos, cancellationToken);
    }

    public async Task<ResultadoIndicacao> IndicarAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var operador = _operador.Id;
        var id = await IdAsync(cancellationToken) ?? throw new NaoEncontradoException(NaoExiste);
        await using var escopo = Escopo(id);
        var servicos = escopo.ServiceProvider;
        var db = servicos.GetRequiredService<AcorduDbContext>();

        // Só conta que já existe e que alguém usa para entrar (com senha): a conta fictícia não conta (F14-110).
        var conta = await servicos.GetRequiredService<UserManager<Usuario>>().FindByEmailAsync(email);
        if (conta is null || conta.PasswordHash is null)
            return ResultadoIndicacao.SemConta;
        // Filtro de tenant: os vínculos da demonstração.
        if (await db.Vinculos.AnyAsync(v => v.UsuarioId == conta.Id, cancellationToken))
            return ResultadoIndicacao.JaIndicada;

        var dono = await db.Papeis.SingleAsync(p => p.Padrao && p.Nome == Papel.NomeDono, cancellationToken);
        db.Add(Vinculo.Criar(Guid.CreateVersion7(), conta.Id, dono));
        db.Add(Trilha(
            operador,
            AcoesAuditoria.NebrysDemonstracaoIndicacao,
            id,
            new { email, antes = (object?)null, depois = new { papel = dono.Nome } },
            _relogio.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
        return ResultadoIndicacao.Indicada;
    }

    public async Task RetirarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var operador = _operador.Id;
        var id = await IdAsync(cancellationToken) ?? throw new NaoEncontradoException(NaoExiste);
        await using var escopo = Escopo(id);
        var db = escopo.ServiceProvider.GetRequiredService<AcorduDbContext>();
        var pessoa = (await IndicadasAsync(db, id, cancellationToken)).FirstOrDefault(p => p.UsuarioId == usuarioId)
            ?? throw new NaoEncontradoException("Esta pessoa não está indicada para a demonstração.");

        var vinculo = await db.Vinculos.SingleAsync(v => v.UsuarioId == usuarioId, cancellationToken);
        var papel = await db.Papeis.Where(p => p.Id == vinculo.PapelId).Select(p => p.Nome).SingleOrDefaultAsync(cancellationToken);
        db.Remove(vinculo);
        // As sessões dela na demonstração param na renovação seguinte, como na remoção da equipe.
        var agora = _relogio.GetUtcNow();
        var sessoes = await db.Set<RefreshToken>()
            .Where(r => r.UsuarioId == usuarioId && r.TenantId == id && r.ConsumidoEm == null && r.RevogadoEm == null)
            .ToListAsync(cancellationToken);
        foreach (var sessao in sessoes)
            sessao.Revogar(agora);
        db.Add(Trilha(
            operador,
            AcoesAuditoria.NebrysDemonstracaoRetirada,
            id,
            new { pessoa.Email, antes = new { papel }, depois = (object?)null },
            agora));
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A demonstração (há uma só), lida sem contexto de empresa.</summary>
    private Task<Guid?> IdAsync(CancellationToken cancellationToken) =>
        _db.Tenants.IgnoreQueryFilters(Filtros).AsNoTracking()
            .Where(t => t.Demonstracao)
            .OrderBy(t => t.CriadoEm)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Escopo próprio com o tenant da demonstração fixado (ADR 0014, item 5).</summary>
    private AsyncServiceScope Escopo(Guid tenantId)
    {
        var escopo = _escopos.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<ContextoTenant>().DefinirEquipe(tenantId);
        return escopo;
    }

    private RegistroAuditoria Trilha(Guid operador, string acao, Guid empresa, object dados, DateTimeOffset agora) =>
        TrilhaDaOperacao.Registro(operador, acao, AlvoEmpresa, empresa, dados, _origem.Ip, agora);

    /// <summary>
    /// As pessoas indicadas: vínculos de equipe da demonstração cuja conta não é uma das fictícias. Sem contexto de empresa
    /// (a área) ou no escopo da demonstração, o filtro é o mesmo: o <c>TenantId</c> dela.
    /// </summary>
    private static async Task<IReadOnlyList<PessoaIndicadaNebrysDto>> IndicadasAsync(
        AcorduDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        var ficticias = EmailsFicticios(DadosFicticios.Arquivo);
        return await db.Vinculos.IgnoreQueryFilters(Filtros).AsNoTracking()
            .Where(v => v.TenantId == tenantId && v.ClienteId == null)
            .Join(db.Users, v => v.UsuarioId, u => u.Id, (v, u) => new { u.Id, u.Nome, u.Email, u.NormalizedEmail })
            .Where(c => c.NormalizedEmail == null || !ficticias.Contains(c.NormalizedEmail))
            .OrderBy(c => c.Nome)
            .ThenBy(c => c.Id)
            .Select(c => new PessoaIndicadaNebrysDto(c.Id, c.Nome, c.Email ?? string.Empty))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Os e-mails das contas fictícias, na forma normalizada do Identity (maiúsculas invariantes).</summary>
    private static string[] EmailsFicticios(DadosFicticios dados)
    {
        var emails = dados.Pessoas.Select(p => p.Email.ToUpperInvariant()).ToList();
        if (dados.Portal is { } portal)
            emails.Add(dados.Clientes.First(c => c.Chave == portal.Cliente).Contato.Email.ToUpperInvariant());
        return [.. emails];
    }

    /// <summary>
    /// As contas fictícias, criadas sem senha quando faltam. Conta com senha num e-mail fictício (alguém se cadastrou com
    /// ele) → <c>422</c>: ela entraria na demonstração como se fosse da equipe.
    /// </summary>
    private static async Task<ContasFicticias> ContasFicticiasAsync(IServiceProvider servicos, DadosFicticios dados)
    {
        var usuarios = servicos.GetRequiredService<UserManager<Usuario>>();
        var pessoas = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var pessoa in dados.Pessoas)
            pessoas[pessoa.Chave] = await ContaFicticiaAsync(usuarios, pessoa.Email, pessoa.Nome);

        Guid? portal = null;
        if (dados.Portal is { } acesso)
        {
            var contato = dados.Clientes.First(c => c.Chave == acesso.Cliente).Contato;
            portal = await ContaFicticiaAsync(usuarios, contato.Email, contato.Nome);
        }

        var dono = dados.Pessoas.First(p => p.Papel == Papel.NomeDono).Chave;
        return new ContasFicticias(pessoas, pessoas[dono], portal);
    }

    private static async Task<Guid> ContaFicticiaAsync(UserManager<Usuario> usuarios, string email, string nome)
    {
        if (await usuarios.FindByEmailAsync(email) is { } existente)
        {
            if (existente.PasswordHash is not null)
            {
                throw new TransicaoInvalidaException(
                    $"O e-mail fictício {email} é de uma conta que entra no Acordu: a demonstração não a usa. Fale com quem cuida da operação.");
            }

            return existente.Id;
        }

        var conta = new Usuario(email, nome);
        var criacao = await usuarios.CreateAsync(conta);
        if (!criacao.Succeeded)
        {
            throw new InvalidOperationException(
                $"A conta fictícia {email} não foi criada: {string.Join(", ", criacao.Errors.Select(e => e.Code))}.");
        }

        return conta.Id;
    }

    /// <param name="Pessoas">Chave da pessoa no arquivo → conta.</param>
    /// <param name="Dono">A conta da pessoa com o papel Dono: quem "criou" a demonstração.</param>
    /// <param name="Portal">A conta do contato com acesso ao portal, se o arquivo tem portal.</param>
    private sealed record ContasFicticias(IReadOnlyDictionary<string, Guid> Pessoas, Guid Dono, Guid? Portal);
}
