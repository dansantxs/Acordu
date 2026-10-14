using System.Text.RegularExpressions;
using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;

namespace Acordu.Application.Notificacoes;

/// <summary>
/// Aviso de menção (RF-COL-02, categoria MENCAO), depois do commit: cada mencionado que ainda é da equipe recebe o
/// sino e, se não desligou, o e-mail com um trecho do texto (as menções viram <c>@Nome</c>).
/// </summary>
public sealed partial class AvisosDeMencao : IManipuladorPosCommit<PessoasMencionadas>
{
    private const int TamanhoTrecho = 280;

    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly IRepositorio<Interacao> _interacoes;
    private readonly IRepositorio<Tarefa> _tarefas;
    private readonly IRepositorio<ComentarioTarefa> _comentarios;
    private readonly IPessoasDaEquipe _pessoas;
    private readonly AvisoInterno _aviso;
    private readonly EnderecosPublicos _enderecos;

    public AvisosDeMencao(
        IRepositorio<Solicitacao> solicitacoes,
        IRepositorio<Interacao> interacoes,
        IRepositorio<Tarefa> tarefas,
        IRepositorio<ComentarioTarefa> comentarios,
        IPessoasDaEquipe pessoas,
        AvisoInterno aviso,
        EnderecosPublicos enderecos)
    {
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(interacoes);
        ArgumentNullException.ThrowIfNull(tarefas);
        ArgumentNullException.ThrowIfNull(comentarios);
        ArgumentNullException.ThrowIfNull(pessoas);
        ArgumentNullException.ThrowIfNull(aviso);
        ArgumentNullException.ThrowIfNull(enderecos);
        _solicitacoes = solicitacoes;
        _interacoes = interacoes;
        _tarefas = tarefas;
        _comentarios = comentarios;
        _pessoas = pessoas;
        _aviso = aviso;
        _enderecos = enderecos;
    }

    public async Task TratarAsync(PessoasMencionadas evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        var alvo = await AlvoAsync(evento, cancellationToken);
        if (alvo is null)
            return;

        var autor = await _pessoas.ObterAsync(evento.AutorId, cancellationToken);
        var nomeAutor = autor?.Nome ?? "Alguém da equipe";
        var trecho = await TrechoAsync(alvo.Value.Texto, cancellationToken);
        foreach (var mencionadoId in evento.Mencionados)
        {
            if (await _pessoas.ObterAsync(mencionadoId, cancellationToken) is not { } pessoa)
                continue;

            await _aviso.AvisarAsync(
                pessoa,
                CategoriaAviso.Mencao,
                $"mencao:{evento.OrigemId}",
                $"{nomeAutor} mencionou você {alvo.Value.Nome}",
                alvo.Value.Link,
                new MensagemEmail(
                    pessoa.Email,
                    $"{nomeAutor} mencionou você {alvo.Value.Nome}",
                    $"""
                    Olá, {pessoa.Nome}!

                    {nomeAutor} mencionou você {alvo.Value.Nome}:

                    "{trecho}"

                    {alvo.Value.Link}
                    """,
                    "aviso-mencao"),
                cancellationToken);
        }
    }

    private async Task<(string Nome, Uri Link, string? Texto)?> AlvoAsync(PessoasMencionadas evento, CancellationToken cancellationToken)
    {
        if (evento.Alvo == AlvoColaboracao.Solicitacao)
        {
            var solicitacao = await _solicitacoes.ObterPorIdAsync(evento.AlvoId, cancellationToken);
            var interacao = await _interacoes.ObterPorIdAsync(evento.OrigemId, cancellationToken);
            return solicitacao is null || interacao is null
                ? null
                : ($"na solicitação #{solicitacao.Numero} {solicitacao.Titulo}", _enderecos.SolicitacaoInterna(solicitacao.Id), interacao.Texto);
        }

        var tarefa = await _tarefas.ObterPorIdAsync(evento.AlvoId, cancellationToken);
        var comentario = await _comentarios.ObterPorIdAsync(evento.OrigemId, cancellationToken);
        return tarefa is null || comentario is null
            ? null
            : ($"na tarefa {tarefa.Titulo}", _enderecos.TarefaInterna(tarefa.Id), comentario.Texto);
    }

    /// <summary>Troca <c>@{id}</c> por <c>@Nome</c> (ou <c>@alguém</c> se a pessoa saiu) e corta no tamanho do trecho.</summary>
    private async Task<string> TrechoAsync(string? texto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(texto))
            return string.Empty;
        var nomes = new Dictionary<Guid, string>();
        foreach (Match m in Token().Matches(texto))
        {
            var id = Guid.Parse(m.Groups[1].ValueSpan);
            if (!nomes.ContainsKey(id))
                nomes[id] = (await _pessoas.ObterAsync(id, cancellationToken))?.Nome ?? "alguém";
        }

        var legivel = Token().Replace(texto, m => "@" + nomes[Guid.Parse(m.Groups[1].ValueSpan)]);
        return legivel.Length <= TamanhoTrecho ? legivel : string.Concat(legivel.AsSpan(0, TamanhoTrecho), "…");
    }

    [GeneratedRegex(@"@\{([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\}")]
    private static partial Regex Token();
}
