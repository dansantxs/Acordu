namespace Acordu.Domain.Common;

/// <summary>Validação de referências por Id entre agregados.</summary>
internal static class Ids
{
    /// <summary>Referência opcional: nulo é aceito, <see cref="Guid.Empty"/> não.</summary>
    public static Guid? Opcional(Guid? id, string parametro) =>
        id == Guid.Empty ? throw new ArgumentException("O Id não pode ser vazio.", parametro) : id;

    public static Guid Obrigatorio(Guid id, string parametro) =>
        id == Guid.Empty ? throw new ArgumentException("O Id não pode ser vazio.", parametro) : id;
}
