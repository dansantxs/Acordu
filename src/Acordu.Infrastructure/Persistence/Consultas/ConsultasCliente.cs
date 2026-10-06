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
        string? busca, bool? ativo, Paginacao paginacao, CancellationToken cancellationToken = default)
    {
        var consulta = _db.Clientes.AsNoTracking().Where(c => ativo == null || c.Ativo == ativo);
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = $"%{busca.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
            var digitos = string.Concat(busca.Where(char.IsAsciiDigit));
            consulta = consulta.Where(c => EF.Functions.ILike(c.Nome, termo)
                || (digitos.Length > 0 && c.Documento != null && c.Documento.Contains(digitos)));
        }

        var total = await consulta.CountAsync(cancellationToken);
        var itens = await consulta
            .OrderBy(c => c.Nome).ThenBy(c => c.Id)
            .Skip(paginacao.Pular).Take(paginacao.TamanhoPagina)
            .Select(c => new ClienteResumoDto(c.Id, c.Nome, c.Documento, c.Ativo))
            .ToListAsync(cancellationToken);
        return new ResultadoPaginado<ClienteResumoDto>(itens, paginacao.Pagina, paginacao.TamanhoPagina, total);
    }

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
                    c.Id, c.Nome, c.Email, c.Telefone, c.Cargo, c.Ativo, c.UsuarioId is not null, acessos.GetValueOrDefault(c.Id)))]);
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
