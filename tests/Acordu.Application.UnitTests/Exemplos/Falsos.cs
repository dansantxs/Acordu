using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Application.Exemplos;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.UnitTests.Exemplos;

/// <summary>Dublês da carga e das ações dos exemplos (F14r), com o relógio fixo numa terça (06/10/2026, 10h em São Paulo).</summary>
internal static class Falsos
{
    public static readonly DateTimeOffset Agora = new(2026, 10, 6, 13, 0, 0, TimeSpan.Zero);

    public static readonly DateOnly Hoje = new(2026, 10, 6);

    /// <summary>Empresa em TESTE criada ontem pelo <paramref name="dono"/>.</summary>
    public static Tenant EmTeste(Guid dono) => Tenant.Criar(Guid.NewGuid(), "Estúdio Traço", Agora.AddDays(-1), dono, TimeSpan.FromDays(30));
}

internal sealed class Repositorio<T> : IRepositorio<T>
    where T : EntidadeBase
{
    public List<T> Itens { get; } = [];

    public Task<T?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Itens.Find(i => i.Id == id));

    public void Adicionar(T entidade) => Itens.Add(entidade);

    public void Remover(T entidade) => Itens.Remove(entidade);
}

/// <summary>A carga com os repositórios em memória.</summary>
internal sealed class CargaEmMemoria
{
    private int _numero;

    public CargaEmMemoria(Guid tenantId)
    {
        var calendarios = new CalendariosFixos(tenantId);
        Carga = new CargaDosDadosFicticios(
            Clientes, Contratos, Solicitacoes, Interacoes, Projetos, Tarefas, Apontamentos,
            new Protocolo(() => ++_numero), calendarios, new RelogioDoTenant(calendarios, new RelogioFixo(Falsos.Agora)));
    }

    public CargaDosDadosFicticios Carga { get; }

    public Repositorio<Cliente> Clientes { get; } = new();

    public Repositorio<Contrato> Contratos { get; } = new();

    public Repositorio<Solicitacao> Solicitacoes { get; } = new();

    public Repositorio<Interacao> Interacoes { get; } = new();

    public Repositorio<Projeto> Projetos { get; } = new();

    public Repositorio<Tarefa> Tarefas { get; } = new();

    public Repositorio<Apontamento> Apontamentos { get; } = new();

    public IEnumerable<EntidadeBase> Todas =>
        Clientes.Itens.Cast<EntidadeBase>()
            .Concat(Contratos.Itens).Concat(Solicitacoes.Itens).Concat(Interacoes.Itens).Concat(Projetos.Itens)
            .Concat(Tarefas.Itens).Concat(Apontamentos.Itens);

    private sealed class Protocolo(Func<int> proximo) : IGeradorProtocolo
    {
        public Task<int> ProximoNumeroSolicitacaoAsync(CancellationToken cancellationToken = default) => Task.FromResult(proximo());
    }
}

internal sealed class CalendariosFixos(Guid tenantId) : ICalendarios
{
    public Task<CalendarioUtil> DoTenantAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CalendarioUtil.Padrao(tenantId));

    public Task<CalendarioUtil> ParaEdicaoAsync(CancellationToken cancellationToken = default) => DoTenantAsync(cancellationToken);
}

internal sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => agora;
}

internal sealed class ContextoDoDono(Guid tenantId, Guid usuarioId, params string[] permissoes) : ITenantContext
{
    public Guid? TenantId => tenantId;

    public bool EhCliente => false;

    public Guid? ClienteId => null;

    public Guid? UsuarioId => usuarioId;

    public string? Papel => "Dono";

    public IReadOnlySet<string> Permissoes { get; } = permissoes.ToHashSet(StringComparer.Ordinal);

    public EscopoPortal? EscopoPortal => null;

    public Guid? AcessoPortalId => null;

    public Guid? SolicitacaoPermitida => null;
}

internal sealed class TenantFixo(Tenant tenant) : ITenantAtual
{
    public Task<Tenant> ObterAsync(CancellationToken cancellationToken = default) => Task.FromResult(tenant);
}

internal sealed class ExemplosFalsos : IDadosDeExemplo
{
    public SituacaoDosExemplos Situacao { get; set; } = new(Carregados: false, DadosProprios: false);

    public ExemplosRemovidos Removidos { get; set; } = new(0, []);

    public int Consultas { get; private set; }

    public int Remocoes { get; private set; }

    public Task<SituacaoDosExemplos> SituacaoAsync(Guid? criadorId, CancellationToken cancellationToken = default)
    {
        Consultas++;
        return Task.FromResult(Situacao);
    }

    public Task<ExemplosRemovidos> RemoverAsync(CancellationToken cancellationToken = default)
    {
        Remocoes++;
        return Task.FromResult(Removidos);
    }
}

internal sealed record RegistroFalso(string Acao, string AlvoTipo, Guid? AlvoId, object? Dados);

internal sealed class AuditoriaFalsa : IAuditoria
{
    public List<RegistroFalso> Registros { get; } = [];

    public void Registrar(string acao, string alvoTipo, Guid? alvoId, object? dados = null) =>
        Registros.Add(new RegistroFalso(acao, alvoTipo, alvoId, dados));
}

internal sealed class UnidadeFalsa : IUnitOfWork
{
    public int Gravacoes { get; private set; }

    public Task SalvarAsync(CancellationToken cancellationToken = default)
    {
        Gravacoes++;
        return Task.CompletedTask;
    }
}

/// <summary>Storage que registra o que foi enviado e removido e pode falhar na remoção.</summary>
internal sealed class ArmazenamentoFalso(bool falhar = false) : IArmazenamentoArquivos
{
    public List<string> Removidas { get; } = [];

    /// <summary>As chaves enviadas, com o tipo, na ordem.</summary>
    public List<(string Chave, string Tipo)> Enviadas { get; } = [];

    public Task EnviarAsync(string chave, Stream conteudo, long tamanhoBytes, string tipoConteudo, CancellationToken cancellationToken = default)
    {
        Enviadas.Add((chave, tipoConteudo));
        return Task.CompletedTask;
    }

    public Task RemoverAsync(string chave, CancellationToken cancellationToken = default)
    {
        if (falhar)
            throw new IOException("storage fora do ar");
        Removidas.Add(chave);
        return Task.CompletedTask;
    }

    public Uri UrlDeDownload(string chave, string nomeArquivo, string tipoConteudo, TimeSpan validade) => new("https://exemplo.invalid/");
}
