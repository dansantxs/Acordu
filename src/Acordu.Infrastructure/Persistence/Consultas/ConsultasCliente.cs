using System.Globalization;
using Acordu.Application.Clientes;
using Acordu.Application.Common;
using Acordu.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>Listagem e detalhe de clientes, pelos filtros globais (contato só vê o próprio cliente).</summary>
public sealed class ConsultasCliente : IConsultasCliente
{
    private readonly AcorduDbContext _db;

    public ConsultasCliente(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<ResultadoPaginado<ClienteResumoDto>> ListarAsync(
        string? busca,
        bool? ativo,
        string? etiqueta,
        Guid? responsavelId,
        Paginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var consulta = _db.Clientes.AsNoTracking().Where(c => ativo == null || c.Ativo == ativo);
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = $"%{busca.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
            // O documento fica sem pontuação e em maiúsculas (CNPJ alfanumérico): a busca compara do mesmo jeito.
            var documento = string.Concat(busca.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();
            consulta = consulta.Where(c => EF.Functions.ILike(c.Nome, termo)
                || (c.RazaoSocial != null && EF.Functions.ILike(c.RazaoSocial, termo))
                || (documento.Length > 0 && c.Documento != null && c.Documento.Contains(documento)));
        }

        if (!string.IsNullOrWhiteSpace(etiqueta))
        {
            var exata = etiqueta.Trim();
            consulta = consulta.Where(c => c.Etiquetas.Contains(exata));
        }

        if (responsavelId is { } responsavel)
            consulta = consulta.Where(c => c.ResponsavelId == responsavel);

        var total = await consulta.CountAsync(cancellationToken);
        var linhas = await consulta
            .OrderBy(c => c.Nome).ThenBy(c => c.Id)
            .Skip(paginacao.Pular).Take(paginacao.TamanhoPagina)
            .Select(c => new { c.Id, c.Nome, c.Documento, c.Ativo, c.RazaoSocial, c.ResponsavelId, c.Etiquetas })
            .ToListAsync(cancellationToken);
        var nomes = await Nomes.DeUsuariosAsync(
            _db, linhas.Where(l => l.ResponsavelId is not null).Select(l => l.ResponsavelId!.Value), cancellationToken);
        var itens = linhas
            .Select(l => new ClienteResumoDto(
                l.Id, l.Nome, l.Documento, l.Ativo, l.RazaoSocial, Nomes.Pessoa(l.ResponsavelId, nomes), [.. l.Etiquetas]))
            .ToList();
        return new ResultadoPaginado<ClienteResumoDto>(itens, paginacao.Pagina, paginacao.TamanhoPagina, total);
    }

    public async Task<IReadOnlyList<string>> EtiquetasAsync(CancellationToken cancellationToken = default) =>
        await _db.Clientes.AsNoTracking()
            .SelectMany(c => c.Etiquetas)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync(cancellationToken);


    public async Task<ClienteDto?> DetalharAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var linha = await _db.Clientes.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new { Cliente = c, Versao = EF.Property<uint>(c, ConfiguracaoRaiz.Versao) })
            .SingleOrDefaultAsync(cancellationToken);
        if (linha is null)
            return null;

        var cliente = linha.Cliente;
        var acessos = await AcessosAoPortalAsync(cliente, cancellationToken);
        Dictionary<Guid, string> nomes = cliente.ResponsavelId is { } responsavel
            ? await Nomes.DeUsuariosAsync(_db, [responsavel], cancellationToken)
            : [];
        return new ClienteDto(
            cliente.Id,
            cliente.Nome,
            cliente.Documento,
            cliente.Observacoes,
            cliente.Ativo,
            linha.Versao.ToString(CultureInfo.InvariantCulture),
            [.. cliente.Contatos
                .OrderBy(c => c.Nome)
                .Select(c => new ContatoDto(
                    c.Id, c.Nome, c.Email, c.Telefone, c.Cargo, c.Ativo, c.UsuarioId is not null, acessos.GetValueOrDefault(c.Id)))],
            cliente.RazaoSocial,
            cliente.InscricaoEstadual,
            cliente.Email,
            cliente.Telefone,
            EnderecoDto.De(cliente.Endereco),
            Nomes.Pessoa(cliente.ResponsavelId, nomes),
            [.. cliente.Etiquetas]);
    }

    /// <summary>Por contato: vínculo de cliente (ATIVO) ou, sem ele, o convite pendente (CONVIDADO).</summary>
    private async Task<Dictionary<Guid, AcessoPortalContatoDto>> AcessosAoPortalAsync(
        Domain.Entities.Cliente cliente, CancellationToken cancellationToken)
    {
        var usuarios = cliente.Contatos.Where(c => c.UsuarioId is not null).Select(c => c.UsuarioId!.Value).ToList();
        var vinculos = usuarios.Count == 0
            ? []
            : await _db.Vinculos.AsNoTracking()
                .Where(v => v.ClienteId == cliente.Id && usuarios.Contains(v.UsuarioId))
                .ToDictionaryAsync(v => v.UsuarioId, v => v.EscopoPortal!.Value, cancellationToken);
        var convites = await _db.ConvitesPortal.AsNoTracking()
            .Where(c => c.ClienteId == cliente.Id && c.AceitoEm == null && c.RevogadoEm == null)
            .ToListAsync(cancellationToken);

        var acessos = new Dictionary<Guid, AcessoPortalContatoDto>();
        foreach (var contato in cliente.Contatos)
        {
            if (contato.UsuarioId is { } usuario && vinculos.TryGetValue(usuario, out var escopo))
                acessos[contato.Id] = new AcessoPortalContatoDto(SituacaoAcessoPortal.Ativo, escopo, null);
            else if (convites.Where(c => c.ContatoId == contato.Id).MaxBy(c => c.CriadoEm) is { } convite)
                acessos[contato.Id] = new AcessoPortalContatoDto(SituacaoAcessoPortal.Convidado, convite.Escopo, convite.ExpiraEm);
        }

        return acessos;
    }
}
