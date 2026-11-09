using System.Text.Json;
using Acordu.Application.Abstractions;
using Acordu.Application.Atividades;
using Acordu.Application.Common;
using Acordu.Domain.Enums;
using Acordu.Infrastructure.Persistence.Consultas;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Atividades;

/// <summary>Feed de atividade no tenant do contexto: inclusão idempotente por SQL e leitura com os títulos atuais.</summary>
public sealed class RegistroAtividades : IAtividades
{
    private readonly AcorduDbContext _db;
    private readonly ITenantContext _contexto;

    public RegistroAtividades(AcorduDbContext db, ITenantContext contexto)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contexto);
        _db = db;
        _contexto = contexto;
    }

    public static string Texto(TipoAtividade tipo) => JsonNamingPolicy.SnakeCaseUpper.ConvertName(tipo.ToString());

    public async Task RegistrarAsync(NovaAtividade atividade, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(atividade);
        var tenant = _contexto.TenantId ?? throw new InvalidOperationException("Atividade exige tenant no contexto.");
        var id = Guid.CreateVersion7(atividade.OcorridaEm);
        var tipo = Texto(atividade.Tipo);
        var alvo = (int)atividade.Alvo;
        await _db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO atividades
                (id, tenant_id, chave, tipo, ocorrida_em, autor_id, autor_eh_cliente, alvo, alvo_id, cliente_id, status_de, status_para, responsavel_id)
            VALUES
                ({id}, {tenant}, {atividade.Chave}, {tipo}, {atividade.OcorridaEm}, {atividade.AutorId}, {atividade.AutorEhCliente}, {alvo},
                 {atividade.AlvoId}, {atividade.ClienteId}, {atividade.StatusDe}, {atividade.StatusPara}, {atividade.ResponsavelId})
            ON CONFLICT (tenant_id, chave) DO NOTHING
            """,
            cancellationToken);
    }

    public async Task<PaginaAtividadesDto> ListarAsync(FiltroAtividades filtro, int tamanho, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var consulta = _db.Set<Atividade>().AsNoTracking();
        if (!filtro.VerSolicitacoes)
            consulta = consulta.Where(a => a.Alvo != AlvoColaboracao.Solicitacao);
        if (!filtro.VerTarefas)
            consulta = consulta.Where(a => a.Alvo != AlvoColaboracao.Tarefa);
        if (filtro.ClienteId is { } clienteId)
            consulta = consulta.Where(a => a.ClienteId == clienteId);
        if (filtro.PessoaId is { } pessoaId)
            consulta = consulta.Where(a => a.AutorId == pessoaId && !a.AutorEhCliente);
        if (filtro.SolicitacaoId is { } solicitacaoId)
            consulta = consulta.Where(a => a.Alvo == AlvoColaboracao.Solicitacao && a.AlvoId == solicitacaoId);
        if (filtro.Antes is { } antes)
            consulta = consulta.Where(a => a.Sequencia < antes);

        var linhas = await consulta.OrderByDescending(a => a.Sequencia).Take(tamanho + 1).ToListAsync(cancellationToken);
        var temMais = linhas.Count > tamanho;
        if (temMais)
            linhas.RemoveAt(linhas.Count - 1);

        var solicitacoes = linhas.Where(l => l.Alvo == AlvoColaboracao.Solicitacao).Select(l => l.AlvoId).Distinct().ToList();
        var tarefas = linhas.Where(l => l.Alvo == AlvoColaboracao.Tarefa).Select(l => l.AlvoId).Distinct().ToList();
        var clientes = linhas.Where(l => l.ClienteId is not null).Select(l => l.ClienteId!.Value).Distinct().ToList();
        var dadosSolicitacoes = await _db.Solicitacoes.AsNoTracking().Where(s => solicitacoes.Contains(s.Id))
            .Select(s => new { s.Id, s.Numero, s.Titulo }).ToDictionaryAsync(s => s.Id, cancellationToken);
        var dadosTarefas = await _db.Tarefas.AsNoTracking().Where(t => tarefas.Contains(t.Id))
            .Select(t => new { t.Id, t.Titulo }).ToDictionaryAsync(t => t.Id, cancellationToken);
        var nomesClientes = await _db.Clientes.AsNoTracking().Where(c => clientes.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nome, cancellationToken);
        // TAREFA_PROJETO guarda os ids dos projetos em status_de/status_para; a leitura mostra os nomes de agora.
        var tipoProjeto = Texto(TipoAtividade.TarefaProjeto);
        var idsProjetos = linhas.Where(l => l.Tipo == tipoProjeto)
            .SelectMany(l => new[] { l.StatusDe, l.StatusPara })
            .Select(v => Guid.TryParse(v, out var id) ? id : (Guid?)null)
            .OfType<Guid>().Distinct().ToList();
        Dictionary<Guid, string> nomesProjetos = idsProjetos.Count == 0
            ? []
            : await _db.Projetos.AsNoTracking().Where(p => idsProjetos.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Nome, cancellationToken);

        var pessoas = await Nomes.DeUsuariosAsync(
            _db,
            linhas.SelectMany(l => new[] { l.AutorId, l.ResponsavelId }).OfType<Guid>(),
            cancellationToken);
        var faltando = linhas.Where(l => l.AutorEhCliente && l.AutorId is { } id && !pessoas.ContainsKey(id)).Select(l => l.AutorId!.Value).Distinct().ToList();
        if (faltando.Count > 0)
        {
            foreach (var contato in await _db.Clientes.AsNoTracking().SelectMany(c => c.Contatos)
                .Where(c => faltando.Contains(c.Id)).Select(c => new { c.Id, c.Nome }).ToListAsync(cancellationToken))
                pessoas[contato.Id] = contato.Nome;
        }

        var itens = new List<AtividadeDto>(linhas.Count);
        foreach (var l in linhas)
        {
            AlvoAtividadeDto? alvo = l.Alvo == AlvoColaboracao.Solicitacao
                ? dadosSolicitacoes.TryGetValue(l.AlvoId, out var s) ? new AlvoAtividadeDto(l.Alvo, l.AlvoId, s.Titulo, s.Numero) : null
                : dadosTarefas.TryGetValue(l.AlvoId, out var t) ? new AlvoAtividadeDto(l.Alvo, l.AlvoId, t.Titulo, null) : null;
            // Alvo que não aparece mais pelos filtros (ou foi removido) some do feed.
            if (alvo is null)
                continue;
            var detalhe = l.StatusDe is null && l.StatusPara is null && l.ResponsavelId is null
                ? null
                : l.Tipo == tipoProjeto
                    ? new DetalheAtividadeDto(
                        null, null, null, Projeto(l.StatusDe, nomesProjetos), Projeto(l.StatusPara, nomesProjetos))
                    : new DetalheAtividadeDto(l.StatusDe, l.StatusPara, Nomes.Pessoa(l.ResponsavelId, pessoas), null, null);
            itens.Add(new AtividadeDto(
                l.Id,
                Enum.GetValues<TipoAtividade>().First(x => Texto(x) == l.Tipo),
                l.OcorridaEm,
                Nomes.Pessoa(l.AutorId, pessoas),
                l.AutorEhCliente,
                alvo,
                l.ClienteId is { } c && nomesClientes.TryGetValue(c, out var nome) ? new Referencia(c, nome) : null,
                detalhe));
        }

        return new PaginaAtividadesDto(itens, temMais ? linhas[^1].Sequencia : null);
    }

    private static Referencia? Projeto(string? valor, Dictionary<Guid, string> nomes) =>
        Guid.TryParse(valor, out var id) ? new Referencia(id, nomes.GetValueOrDefault(id, string.Empty)) : null;
}
