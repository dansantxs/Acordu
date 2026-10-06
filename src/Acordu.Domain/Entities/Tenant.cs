using Acordu.Domain.Common;

namespace Acordu.Domain.Entities;

/// <summary>
/// O prestador de serviço (segurança §2). É a própria fronteira do isolamento: o <see cref="Id"/> é o <c>TenantId</c>
/// de todo o resto. Não herda <see cref="EntidadeBase"/>, que exigiria um TenantId separado e redundante.
/// </summary>
public sealed class Tenant
{
    private Tenant(Guid id, string nome, DateTimeOffset criadoEm)
    {
        Id = id;
        Nome = nome;
        CriadoEm = criadoEm;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Tenant()
    {
        Nome = null!;
    }

    public Guid Id { get; private set; }

    /// <summary>Nome da empresa do prestador, exibido para a equipe e no portal.</summary>
    public string Nome { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public static Tenant Criar(Guid id, string nome, DateTimeOffset agora) =>
        new(Ids.Obrigatorio(id, nameof(id)), Texto.Obrigatorio(nome, LimitesTexto.Nome, nameof(nome)), agora.ToUniversalTime());

    public void Renomear(string nome) => Nome = Texto.Obrigatorio(nome, LimitesTexto.Nome, nameof(nome));
}
