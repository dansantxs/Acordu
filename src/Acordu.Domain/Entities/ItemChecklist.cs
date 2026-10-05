using Acordu.Domain.Common;

namespace Acordu.Domain.Entities;

/// <summary>Subitem da checklist de uma <see cref="Tarefa"/>. Só muda pelos métodos da tarefa.</summary>
public sealed class ItemChecklist
{
    internal ItemChecklist(Guid id, int ordem, string texto)
    {
        Id = id;
        Ordem = ordem;
        Texto = texto;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private ItemChecklist()
    {
        Texto = null!;
    }

    public Guid Id { get; private set; }

    /// <summary>Posição na checklist (persistida: o banco não garante a ordem de carga).</summary>
    public int Ordem { get; private set; }

    public string Texto { get; private set; }

    public bool Feito { get; private set; }

    internal void Editar(string texto) => Texto = texto;

    internal void Marcar(bool feito) => Feito = feito;

    internal static string ValidarTexto(string texto) =>
        Common.Texto.Obrigatorio(texto, LimitesTexto.ItemChecklist, nameof(texto));
}
