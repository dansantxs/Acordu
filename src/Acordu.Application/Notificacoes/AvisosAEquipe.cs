using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Events;

namespace Acordu.Application.Notificacoes;

/// <summary>
/// Avisos internos de solicitação (RF-NOT-02/03: sino e e-mail, pelo <see cref="AvisoInterno"/>), depois do commit: nova solicitação (responsável ou, sem ele, quem tem
/// <c>solicitacao.editar</c>; nunca quem abriu) e atribuição (o novo responsável, se não atribuiu a si mesmo).
/// </summary>
public sealed class AvisosAEquipe :
    IManipuladorPosCommit<SolicitacaoAberta>,
    IManipuladorPosCommit<SolicitacaoAtribuida>
{
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IPessoasDaEquipe _pessoas;
    private readonly AvisoInterno _aviso;
    private readonly EnderecosPublicos _enderecos;

    public AvisosAEquipe(
        IRepositorio<Solicitacao> solicitacoes,
        IRepositorio<Cliente> clientes,
        IPessoasDaEquipe pessoas,
        AvisoInterno aviso,
        EnderecosPublicos enderecos)
    {
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(pessoas);
        ArgumentNullException.ThrowIfNull(aviso);
        ArgumentNullException.ThrowIfNull(enderecos);
        _solicitacoes = solicitacoes;
        _clientes = clientes;
        _pessoas = pessoas;
        _aviso = aviso;
        _enderecos = enderecos;
    }

    public async Task TratarAsync(SolicitacaoAberta evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        var solicitacao = await _solicitacoes.ObterPorIdAsync(evento.SolicitacaoId, cancellationToken);
        if (solicitacao is null)
            return;

        var cliente = await _clientes.ObterPorIdAsync(solicitacao.ClienteId, cancellationToken);
        var destinatarios = await DestinatariosAsync(_pessoas, solicitacao, cancellationToken);
        var titulo = $"Nova solicitação #{solicitacao.Numero}: {solicitacao.Titulo}";
        var link = _enderecos.SolicitacaoInterna(solicitacao.Id);
        foreach (var pessoa in destinatarios.Where(p => p.UsuarioId != evento.AbertaPor))
        {
            await _aviso.AvisarAsync(
                pessoa,
                CategoriaAviso.NovaSolicitacao,
                $"solicitacao-aberta:{solicitacao.Id}",
                titulo,
                link,
                new MensagemEmail(
                    pessoa.Email,
                    $"Nova solicitação #{solicitacao.Numero}: {solicitacao.Titulo}",
                    $"""
                    Olá, {pessoa.Nome}!

                    Chegou a solicitação #{solicitacao.Numero} — {solicitacao.Titulo}.
                    Cliente: {cliente?.Nome}
                    Prioridade: {Textos.Prioridade(solicitacao.Prioridade)}
                    Aberta {(evento.Origem == Domain.Enums.OrigemSolicitacao.Portal ? "pelo cliente, no portal" : "pela equipe")}.

                    {_enderecos.SolicitacaoInterna(solicitacao.Id)}
                    """,
                    "aviso-nova-solicitacao"),
                cancellationToken);
        }
    }

    public async Task TratarAsync(SolicitacaoAtribuida evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        if (evento.ResponsavelId is not { } responsavelId || responsavelId == evento.AtribuidoPor)
            return;
        var solicitacao = await _solicitacoes.ObterPorIdAsync(evento.SolicitacaoId, cancellationToken);
        var pessoa = await _pessoas.ObterAsync(responsavelId, cancellationToken);
        if (solicitacao is null || pessoa is null || solicitacao.ResponsavelId != responsavelId)
            return;

        // Chave com o responsável anterior: A→B→A→B repetido não avisa de novo no sino (o e-mail sai).
        await _aviso.AvisarAsync(
            pessoa,
            CategoriaAviso.Atribuicao,
            $"atribuicao:{solicitacao.Id}:{evento.Anterior}:{responsavelId}",
            $"Solicitação #{solicitacao.Numero} atribuída a você: {solicitacao.Titulo}",
            _enderecos.SolicitacaoInterna(solicitacao.Id),
            new MensagemEmail(
                pessoa.Email,
                $"Solicitação #{solicitacao.Numero} atribuída a você",
                $"""
                Olá, {pessoa.Nome}!

                A solicitação #{solicitacao.Numero} — {solicitacao.Titulo} agora está com você.

                {_enderecos.SolicitacaoInterna(solicitacao.Id)}
                """,
                "aviso-atribuicao"),
            cancellationToken);
    }

    /// <summary>Responsável (se ainda for da equipe) ou, sem ele, quem pode editar solicitações.</summary>
    internal static async Task<IReadOnlyList<PessoaDaEquipe>> DestinatariosAsync(
        IPessoasDaEquipe pessoas, Solicitacao solicitacao, CancellationToken cancellationToken)
    {
        if (solicitacao.ResponsavelId is { } responsavelId && await pessoas.ObterAsync(responsavelId, cancellationToken) is { } responsavel)
            return [responsavel];
        return await pessoas.ComPermissaoAsync(Permissoes.SolicitacaoEditar, cancellationToken);
    }
}
