using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.Nebrys;

/// <summary>
/// Lista de espera na área da Nebrys (RF-AUT-08; F14-76, F14-77; decisão 5 do G2 do F14; design D12): a conversão é
/// manual. Convidar emite um convite de empresa para o e-mail da entrada, pelo mesmo caminho da emissão direta
/// (<see cref="ConvitesEmpresaNebrys"/>), e marca a entrada como convidada; descartar apaga a entrada. Toda ação vai para a
/// trilha sem tenant, com o operador e o e-mail da entrada (D14). A leitura é da <see cref="IConsultasNebrys"/> (F14-75).
/// </summary>
public sealed class ListaEsperaNebrys
{
    /// <summary>Tipo do alvo da entrada na trilha.</summary>
    public const string AlvoEntrada = "lista-espera";

    private readonly IListaEspera _lista;
    private readonly ConvitesEmpresaNebrys _convites;
    private readonly ITrilhaDaOperacao _trilha;
    private readonly IUnitOfWork _unidade;

    public ListaEsperaNebrys(IListaEspera lista, ConvitesEmpresaNebrys convites, ITrilhaDaOperacao trilha, IUnitOfWork unidade)
    {
        ArgumentNullException.ThrowIfNull(lista);
        ArgumentNullException.ThrowIfNull(convites);
        ArgumentNullException.ThrowIfNull(trilha);
        ArgumentNullException.ThrowIfNull(unidade);
        _lista = lista;
        _convites = convites;
        _trilha = trilha;
        _unidade = unidade;
    }

    /// <summary>
    /// Convida a partir da entrada (F14-76): um convite de empresa sai para o e-mail dela, com a entrada de origem, e a
    /// entrada fica convidada, na mesma gravação. Entrada já convidada, e-mail de domínio descartável (#224) ou e-mail que
    /// já tem um convite em aberto → <c>422 transicao-invalida</c>, sem gravar nem enviar; inexistente → <c>404</c>.
    /// </summary>
    public async Task<ConviteEnviado> ConvidarAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _convites.EmitirDaListaAsync(await CarregarAsync(id, cancellationToken), cancellationToken);

    /// <summary>
    /// Descarta a entrada (F14-77): ela é apagada, aguardando ou convidada. O convite que saiu dela continua, sem a origem.
    /// A trilha guarda só o e-mail e a situação de antes (não o resto do que a pessoa escreveu). Inexistente → <c>404</c>.
    /// </summary>
    public async Task DescartarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entrada = await CarregarAsync(id, cancellationToken);
        _trilha.Registrar(
            AcoesAuditoria.NebrysListaDescarte,
            AlvoEntrada,
            entrada.Id,
            new
            {
                entrada.Email,
                antes = new { situacao = entrada.Situacao, entrada.CriadaEm, entrada.ConvidadaEm },
                // A entrada é apagada: não há depois (F14-70, o antes e o depois em toda ação da área).
                depois = (object?)null,
            });
        _lista.Remover(entrada);
        await _unidade.SalvarAsync(cancellationToken);
    }

    private async Task<EntradaListaEspera> CarregarAsync(Guid id, CancellationToken cancellationToken) =>
        await _lista.ObterAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Entrada da lista de espera não encontrada.");
}

/// <summary>
/// Uma entrada da lista de espera na área (F14-75): o que a pessoa mandou, a situação (aguardando ou convidada), a chegada
/// e, se convidada, quando e o convite que saiu dela (nulo se ele não existe mais).
/// </summary>
public sealed record EntradaListaEsperaNebrysDto(
    Guid Id,
    string Nome,
    string Email,
    string NomeEmpresa,
    string? Mensagem,
    SituacaoEntradaListaEspera Situacao,
    DateTimeOffset CriadaEm,
    DateTimeOffset? ConvidadaEm,
    Guid? ConviteId);
