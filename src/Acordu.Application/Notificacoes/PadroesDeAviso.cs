namespace Acordu.Application.Notificacoes;

/// <summary>Estado de fábrica do e-mail por categoria (convenções): ligado, exceto as que fariam barulho demais.</summary>
public static class PadroesDeAviso
{
    /// <summary>Categorias que nascem com o e-mail desligado; o sino recebe sempre.</summary>
    public static readonly IReadOnlySet<CategoriaAviso> EmailDesligadoPorPadrao = new HashSet<CategoriaAviso> { CategoriaAviso.Seguidas };
}
