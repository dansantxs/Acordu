using Acordu.Application.Common;
using Acordu.Domain.Enums;

namespace Acordu.Application.Atividades;

/// <summary>O que aconteceu (RF-COL-05). O front monta a frase a partir do tipo e do detalhe.</summary>
public enum TipoAtividade
{
    SolicitacaoAberta = 1,
    SolicitacaoAtribuida = 2,
    SolicitacaoStatus = 3,
    RespostaDoCliente = 4,
    RespostaAoCliente = 5,
    NotaInterna = 6,
    TarefaCriada = 7,
    TarefaAtribuida = 8,
    TarefaStatus = 9,
    ComentarioTarefa = 10,

    /// <summary>A tarefa mudou de projeto (F11-26): <see cref="DetalheAtividadeDto.ProjetoDe"/> e <c>ProjetoPara</c>.</summary>
    TarefaProjeto = 11,
}

/// <summary>
/// Item do feed (contrato §11f). <see cref="Autor"/> nulo = o sistema (job, recorrência); <see cref="AutorEhCliente"/>
/// marca o contato do cliente. O <see cref="Alvo"/> traz o título atual do item.
/// </summary>
public sealed record AtividadeDto(
    Guid Id,
    TipoAtividade Tipo,
    DateTimeOffset OcorridaEm,
    Referencia? Autor,
    bool AutorEhCliente,
    AlvoAtividadeDto Alvo,
    Referencia? Cliente,
    DetalheAtividadeDto? Detalhe);

/// <param name="Numero">Só solicitação.</param>
public sealed record AlvoAtividadeDto(AlvoColaboracao Tipo, Guid Id, string Titulo, int? Numero);

/// <summary>
/// Status de/para (SNAKE, como no resto da API), o novo responsável ou, em <c>TAREFA_PROJETO</c>, o projeto de antes e o
/// de agora (nulo = sem projeto), conforme o tipo.
/// </summary>
public sealed record DetalheAtividadeDto(
    string? StatusDe, string? StatusPara, Referencia? Responsavel, Referencia? ProjetoDe = null, Referencia? ProjetoPara = null);

/// <summary>Página do feed; <see cref="Proximo"/> é o cursor da próxima (<c>antes</c>), nulo no fim.</summary>
public sealed record PaginaAtividadesDto(IReadOnlyList<AtividadeDto> Itens, long? Proximo);

/// <summary>Uma linha a gravar, montada pelo <see cref="RegistroDeAtividades"/>.</summary>
public sealed record NovaAtividade(
    Guid Chave,
    TipoAtividade Tipo,
    DateTimeOffset OcorridaEm,
    Guid? AutorId,
    bool AutorEhCliente,
    AlvoColaboracao Alvo,
    Guid AlvoId,
    Guid? ClienteId,
    string? StatusDe = null,
    string? StatusPara = null,
    Guid? ResponsavelId = null);

public sealed record FiltroAtividades(
    Guid? ClienteId, Guid? PessoaId, long? Antes, bool VerSolicitacoes, bool VerTarefas, Guid? SolicitacaoId = null);

/// <summary>Feed de atividade no tenant do contexto (fora dos agregados, como o sino).</summary>
public interface IAtividades
{
    /// <summary>Idempotente pela <see cref="NovaAtividade.Chave"/> (a mensagem da outbox): a reentrega não duplica.</summary>
    Task RegistrarAsync(NovaAtividade atividade, CancellationToken cancellationToken = default);

    Task<PaginaAtividadesDto> ListarAsync(FiltroAtividades filtro, int tamanho, CancellationToken cancellationToken = default);
}
