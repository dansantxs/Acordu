namespace Acordu.Application.Notificacoes;

/// <summary>Notificação do sino (contrato §11).</summary>
public sealed record NotificacaoDto(Guid Id, CategoriaAviso Categoria, string Titulo, Uri? Link, DateTimeOffset CriadaEm, DateTimeOffset? LidaEm);

/// <summary>Página de notificações com o total de não lidas (o número do sino).</summary>
public sealed record NotificacoesDto(IReadOnlyList<NotificacaoDto> Itens, int Pagina, int TamanhoPagina, int Total, int NaoLidas);

/// <summary>Uma categoria e se o e-mail dela está ligado.</summary>
public sealed record PreferenciaDto(CategoriaAviso Categoria, bool Email);

/// <summary>Corpo e resposta de <c>/notificacoes/preferencias</c>: as quatro categorias (ausente no PUT = ligada).</summary>
public sealed record PreferenciasDto(IReadOnlyList<PreferenciaDto> Categorias);
