using Acordu.Application.Clientes;
using Acordu.Application.Common;
using Acordu.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>
/// Anexos do cliente pelos filtros globais: das interações (com o número da solicitação) e dos comentários das tarefas
/// dele. Só metadados; junta, ordena e pagina em memória (o volume por cliente é de dezenas a centenas).
/// </summary>
public sealed class ConsultasArquivosCliente : IConsultasArquivosCliente
{
    private readonly AcorduDbContext _db;

    public ConsultasArquivosCliente(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<ResultadoPaginado<ArquivoClienteDto>> ListarAsync(
        Guid clienteId, bool deSolicitacoes, bool deTarefas, Paginacao paginacao, CancellationToken cancellationToken = default)
    {
        var linhas = new List<Linha>();
        if (deSolicitacoes)
        {
            linhas.AddRange(await (from i in _db.Interacoes.AsNoTracking()
                                   join s in _db.Solicitacoes on i.SolicitacaoId equals s.Id
                                   from a in i.Anexos
                                   where i.ClienteId == clienteId
                                   select new Linha(
                                       a.Id, a.NomeArquivo, a.TipoConteudo, a.TamanhoBytes, i.RegistradaEm, i.AutorId, i.AutorEhCliente,
                                       i.Visibilidade == Visibilidade.Interna, AlvoColaboracao.Solicitacao, s.Id, s.Titulo, s.Numero))
                .ToListAsync(cancellationToken));
        }

        if (deTarefas)
        {
            linhas.AddRange(await (from c in _db.ComentariosTarefa.AsNoTracking()
                                   join t in _db.Tarefas on c.TarefaId equals t.Id
                                   from a in c.Anexos
                                   where t.ClienteId == clienteId
                                   select new Linha(
                                       a.Id, a.NomeArquivo, a.TipoConteudo, a.TamanhoBytes, c.RegistradoEm, c.AutorId, false,
                                       true, AlvoColaboracao.Tarefa, t.Id, t.Titulo, null))
                .ToListAsync(cancellationToken));
        }

        var pagina = linhas
            .OrderByDescending(l => l.EnviadoEm).ThenBy(l => l.NomeArquivo, StringComparer.Ordinal).ThenBy(l => l.Id)
            .Skip(paginacao.Pular).Take(paginacao.TamanhoPagina)
            .ToList();
        var pessoas = await Nomes.DeAutoresAsync(_db, clienteId, pagina.Select(l => l.AutorId), cancellationToken);
        return new ResultadoPaginado<ArquivoClienteDto>(
            [.. pagina.Select(l => new ArquivoClienteDto(
                l.Id, l.NomeArquivo, l.TipoConteudo, l.TamanhoBytes, l.EnviadoEm,
                pessoas.TryGetValue(l.AutorId, out var nome) ? new Referencia(l.AutorId, nome) : null,
                l.AutorEhCliente, l.Interno, new OrigemArquivoDto(l.Origem, l.OrigemId, l.Titulo, l.Numero)))],
            paginacao.Pagina,
            paginacao.TamanhoPagina,
            linhas.Count);
    }

    private sealed record Linha(
        Guid Id,
        string NomeArquivo,
        string TipoConteudo,
        long TamanhoBytes,
        DateTimeOffset EnviadoEm,
        Guid AutorId,
        bool AutorEhCliente,
        bool Interno,
        AlvoColaboracao Origem,
        Guid OrigemId,
        string Titulo,
        int? Numero);
}
