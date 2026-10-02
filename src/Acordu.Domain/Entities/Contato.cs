namespace Acordu.Domain.Entities;

/// <summary>Pessoa do cliente (RF-CLI-02). Entidade dentro de <see cref="Cliente"/>; referenciada de fora por Id.</summary>
public sealed class Contato
{
    internal Contato(Guid id, string nome, string email, string? telefone, string? cargo)
    {
        Id = id;
        Nome = nome;
        Email = email;
        Telefone = telefone;
        Cargo = cargo;
        Ativo = true;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Contato()
    {
        Nome = null!;
        Email = null!;
    }

    public Guid Id { get; private set; }

    public string Nome { get; private set; }

    /// <summary>Normalizado em minúsculas; único entre os contatos ativos do cliente.</summary>
    public string Email { get; private set; }

    public string? Telefone { get; private set; }

    public string? Cargo { get; private set; }

    /// <summary>Usuário do portal, preenchido quando o contato aceita o convite (RF-CLI-03).</summary>
    public Guid? UsuarioId { get; private set; }

    public bool Ativo { get; private set; }

    internal void Atualizar(string nome, string email, string? telefone, string? cargo)
    {
        Nome = nome;
        Email = email;
        Telefone = telefone;
        Cargo = cargo;
    }

    internal void Inativar() => Ativo = false;

    internal void VincularUsuario(Guid usuarioId) => UsuarioId = usuarioId;
}
