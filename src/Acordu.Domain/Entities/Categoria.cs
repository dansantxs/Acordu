using Acordu.Domain.Common;

namespace Acordu.Domain.Entities;

/// <summary>
/// Categoria de solicitação configurável pelo tenant (RF-SOL-04). Nome único por tenant é regra entre agregados:
/// Application + índice único no banco. Inativa não aparece para novas solicitações, mas continua nas antigas.
/// </summary>
public sealed class Categoria : EntidadeBase
{
    private Categoria(Guid id, Guid tenantId, string nome)
        : base(id, tenantId)
    {
        Nome = nome;
        Ativa = true;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Categoria()
    {
        Nome = null!;
    }

    public string Nome { get; private set; }

    public bool Ativa { get; private set; }

    public static Categoria Criar(Guid id, Guid tenantId, string nome) =>
        new(id, tenantId, Texto.Obrigatorio(nome, LimitesTexto.NomeCategoria, nameof(nome)));

    public void Renomear(string nome) => Nome = Texto.Obrigatorio(nome, LimitesTexto.NomeCategoria, nameof(nome));

    public void Inativar() => Ativa = false;

    public void Reativar() => Ativa = true;
}
