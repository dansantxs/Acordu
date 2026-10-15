using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.Clientes;

/// <summary>
/// Anexo do cliente na aba Arquivos (contrato §4.3, RF-COL-05). <see cref="Interno"/>: de nota interna ou de comentário
/// de tarefa (o cliente não vê). Download pelo mesmo <c>/anexos/{id}/download</c>.
/// </summary>
public sealed record ArquivoClienteDto(
    Guid Id,
    string NomeArquivo,
    string TipoConteudo,
    long TamanhoBytes,
    DateTimeOffset EnviadoEm,
    Referencia? Autor,
    bool AutorEhCliente,
    bool Interno,
    OrigemArquivoDto Origem);

/// <param name="Numero">Só solicitação.</param>
public sealed record OrigemArquivoDto(AlvoColaboracao Tipo, Guid Id, string Titulo, int? Numero);

public interface IConsultasArquivosCliente
{
    /// <summary>Do mais novo; só as origens que a pessoa pode ver (interações e/ou comentários de tarefa).</summary>
    Task<ResultadoPaginado<ArquivoClienteDto>> ListarAsync(
        Guid clienteId, bool deSolicitacoes, bool deTarefas, Paginacao paginacao, CancellationToken cancellationToken = default);
}

/// <summary>
/// Aba de arquivos do cliente: todos os anexos das interações das solicitações dele (com <c>solicitacao.ver</c>) e dos
/// comentários das tarefas dele (com <c>tarefa.ver</c>). Só leitura.
/// </summary>
public sealed class LeituraArquivosCliente
{
    private readonly ITenantContext _contexto;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IConsultasArquivosCliente _consultas;

    public LeituraArquivosCliente(ITenantContext contexto, IRepositorio<Cliente> clientes, IConsultasArquivosCliente consultas)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(consultas);
        _contexto = contexto;
        _clientes = clientes;
        _consultas = consultas;
    }

    public async Task<ResultadoPaginado<ArquivoClienteDto>> ListarAsync(
        Guid clienteId, Paginacao paginacao, CancellationToken cancellationToken = default)
    {
        var solicitacoes = _contexto.Permissoes.Contains(Permissoes.SolicitacaoVer);
        var tarefas = _contexto.Permissoes.Contains(Permissoes.TarefaVer);
        if (!solicitacoes && !tarefas)
            throw new AcessoNegadoException();
        if (await _clientes.ObterPorIdAsync(clienteId, cancellationToken) is null)
            throw new NaoEncontradoException("Cliente não encontrado.");
        return await _consultas.ListarAsync(clienteId, solicitacoes, tarefas, paginacao, cancellationToken);
    }
}
