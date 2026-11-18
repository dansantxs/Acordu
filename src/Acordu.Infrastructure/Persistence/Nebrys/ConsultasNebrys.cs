using System.Globalization;
using Acordu.Application.Common;
using Acordu.Application.Empresas;
using Acordu.Application.Nebrys;
using Acordu.Domain.Enums;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Nebrys;

/// <summary>
/// Consultas da área da Nebrys (ADR 0014, item 3; read side "Área da Nebrys"). A sessão de operação não tem tenant, então
/// os filtros globais não devolvem nada: este é um ponto revisado de <c>IgnoreQueryFilters</c> (convenções §1), que lê só
/// o <c>Tenant</c>, a conta de quem o criou e a contagem dos vínculos da equipe, e projeta só os campos da relação com a
/// Nebrys. Nenhuma entidade de negócio do prestador é lida aqui (teste de arquitetura).
/// </summary>
public sealed class ConsultasNebrys : IConsultasNebrys
{
    private static readonly string[] Filtros = [FiltrosGlobais.Tenant, FiltrosGlobais.Cliente];

    private readonly AcorduDbContext _db;
    private readonly OpcoesEmpresas _empresas;

    public ConsultasNebrys(AcorduDbContext db, OpcoesEmpresas empresas)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(empresas);
        _db = db;
        _empresas = empresas;
    }

    public async Task<ResultadoPaginado<EmpresaResumoNebrysDto>> ListarEmpresasAsync(
        SituacaoEmpresa? situacao, Paginacao paginacao, CancellationToken cancellationToken = default)
    {
        var empresas = _db.Tenants.IgnoreQueryFilters(Filtros).AsNoTracking();
        if (situacao is { } filtro)
            empresas = empresas.Where(t => t.Situacao == filtro);

        var total = await empresas.CountAsync(cancellationToken);
        var linhas = await Projetar(empresas
                .OrderByDescending(t => t.CriadoEm)
                .ThenByDescending(t => t.Id)
                .Skip(paginacao.Pular)
                .Take(paginacao.TamanhoPagina))
            .ToListAsync(cancellationToken);
        return new ResultadoPaginado<EmpresaResumoNebrysDto>(
            linhas.Select(l => new EmpresaResumoNebrysDto(
                l.Id, l.Nome, l.Documento, l.Situacao, l.CriadoEm, l.FimDoTeste, l.SomenteLeituraDesde, l.CanceladaEm,
                ExpurgoPrevistoEm(l), Criador(l), l.PessoasNaEquipe)).ToList(),
            paginacao.Pagina,
            paginacao.TamanhoPagina,
            total);
    }

    public async Task<EmpresaNebrysDto?> ObterEmpresaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var l = await Projetar(_db.Tenants.IgnoreQueryFilters(Filtros).AsNoTracking().Where(t => t.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
        return l is null
            ? null
            : new EmpresaNebrysDto(
                l.Id, l.Nome, l.Documento, l.Situacao, l.CriadoEm, l.FimDoTeste, l.SomenteLeituraDesde, l.CanceladaEm,
                ExpurgoPrevistoEm(l), Criador(l), l.PessoasNaEquipe, l.Versao.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Os campos da relação, numa consulta só: a conta de quem criou (nome e e-mail) e as pessoas da equipe (vínculos sem
    /// cliente; os contatos do portal não contam).
    /// </summary>
    private IQueryable<LinhaEmpresa> Projetar(IQueryable<Domain.Entities.Tenant> empresas) =>
        empresas.Select(t => new LinhaEmpresa
        {
            Id = t.Id,
            Nome = t.Nome,
            Documento = t.Documento,
            Situacao = t.Situacao,
            CriadoEm = t.CriadoEm,
            FimDoTeste = t.FimDoTeste,
            SomenteLeituraDesde = t.SomenteLeituraDesde,
            CanceladaEm = t.CanceladaEm,
            CriadorNome = _db.Users.Where(u => u.Id == t.CriadoPorUsuarioId).Select(u => u.Nome).FirstOrDefault(),
            CriadorEmail = _db.Users.Where(u => u.Id == t.CriadoPorUsuarioId).Select(u => u.Email).FirstOrDefault(),
            PessoasNaEquipe = _db.Vinculos.IgnoreQueryFilters(Filtros).Count(v => v.TenantId == t.Id && v.ClienteId == null),
            Versao = EF.Property<uint>(t, ConfiguracaoRaiz.Versao),
        });

    /// <summary>Como o <c>/auth/eu</c> (<c>Tenant.ExpurgoPrevistoEm</c>): início do só leitura + o prazo; nulo fora dele.</summary>
    private DateTimeOffset? ExpurgoPrevistoEm(LinhaEmpresa linha) =>
        linha.Situacao.SomenteLeitura() && linha.SomenteLeituraDesde is { } desde ? desde + _empresas.PrazoAteExpurgo : null;

    private static CriadorEmpresaDto? Criador(LinhaEmpresa linha) =>
        linha.CriadorNome is null ? null : new CriadorEmpresaDto(linha.CriadorNome, linha.CriadorEmail);

    private sealed class LinhaEmpresa
    {
        public Guid Id { get; init; }

        public required string Nome { get; init; }

        public string? Documento { get; init; }

        public SituacaoEmpresa Situacao { get; init; }

        public DateTimeOffset CriadoEm { get; init; }

        public DateTimeOffset? FimDoTeste { get; init; }

        public DateTimeOffset? SomenteLeituraDesde { get; init; }

        public DateTimeOffset? CanceladaEm { get; init; }

        public string? CriadorNome { get; init; }

        public string? CriadorEmail { get; init; }

        public int PessoasNaEquipe { get; init; }

        public uint Versao { get; init; }
    }
}
