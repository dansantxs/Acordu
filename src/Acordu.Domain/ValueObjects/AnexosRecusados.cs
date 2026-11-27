using Acordu.Domain.Common;
using Acordu.Domain.Enums;

namespace Acordu.Domain.ValueObjects;

/// <summary>Um anexo de e-mail recebido que não entrou, com o motivo (design do F15, D6 e D14; F15-15).</summary>
public sealed record AnexoRecusado
{
    public AnexoRecusado(string nomeArquivo, MotivoRecusaAnexo motivo)
    {
        if (!Enum.IsDefined(motivo))
            throw new ArgumentOutOfRangeException(nameof(motivo), motivo, "Motivo desconhecido.");
        NomeArquivo = Texto.Obrigatorio(nomeArquivo, LimitesTexto.NomeArquivo, nameof(nomeArquivo));
        Motivo = motivo;
    }

    public string NomeArquivo { get; }

    public MotivoRecusaAnexo Motivo { get; }
}

/// <summary>
/// Os anexos recusados de um e-mail recebido, na ordem do MIME, guardados no registro de entrada (<c>EmailRecebido</c>,
/// D6) para o aviso de recebida dizer ao contato o que não entrou (F15-15). Nunca vazia: sem recusados, nada é guardado.
/// </summary>
public sealed record AnexosRecusados
{
    public AnexosRecusados(IEnumerable<AnexoRecusado> itens)
    {
        ArgumentNullException.ThrowIfNull(itens);
        var lista = itens.ToList();
        if (lista.Count == 0)
            throw new ArgumentException("Informe ao menos um anexo recusado.", nameof(itens));
        if (lista.Contains(null!))
            throw new ArgumentException("Anexo recusado nulo.", nameof(itens));
        Itens = lista;
    }

    public IReadOnlyList<AnexoRecusado> Itens { get; }

    public bool Equals(AnexosRecusados? other) => other is not null && Itens.SequenceEqual(other.Itens);

    public override int GetHashCode() => Itens.Count;
}
