using Acordu.Domain.Common;
using Acordu.Domain.Exceptions;

namespace Acordu.Domain.Entities;

/// <summary>
/// Cliente do prestador, com seus contatos (modelo de domínio §4.1). Inativar não apaga histórico; a Application
/// bloqueia novas solicitações para cliente inativo.
/// </summary>
public sealed class Cliente : EntidadeBase
{
    private readonly List<Contato> _contatos = [];

    private Cliente(Guid id, Guid tenantId, string nome)
        : base(id, tenantId)
    {
        Nome = nome;
        Ativo = true;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Cliente()
    {
        Nome = null!;
    }

    public string Nome { get; private set; }

    /// <summary>CPF ou CNPJ, só dígitos.</summary>
    public string? Documento { get; private set; }

    public string? Observacoes { get; private set; }

    public bool Ativo { get; private set; }

    public IReadOnlyList<Contato> Contatos => _contatos;

    public static Cliente Criar(Guid id, Guid tenantId, string nome, string? documento, string? observacoes) =>
        new(id, tenantId, Texto.Obrigatorio(nome, LimitesTexto.Nome, nameof(nome)))
        {
            Documento = Common.Documento.Normalizar(documento, nameof(documento)),
            Observacoes = Texto.Opcional(observacoes, LimitesTexto.Observacoes, nameof(observacoes)),
        };

    public void Editar(string nome, string? documento, string? observacoes)
    {
        var nomeValido = Texto.Obrigatorio(nome, LimitesTexto.Nome, nameof(nome));
        var documentoValido = Common.Documento.Normalizar(documento, nameof(documento));
        Observacoes = Texto.Opcional(observacoes, LimitesTexto.Observacoes, nameof(observacoes));
        Nome = nomeValido;
        Documento = documentoValido;
    }

    public void Inativar() => Ativo = false;

    public void Reativar() => Ativo = true;

    public Contato AdicionarContato(string nome, string email, string? telefone, string? cargo)
    {
        var dados = ValidarContato(nome, email, telefone, cargo, ignorar: null);
        var contato = new Contato(Guid.CreateVersion7(), dados.Nome, dados.Email, dados.Telefone, dados.Cargo);
        _contatos.Add(contato);
        return contato;
    }

    public void AtualizarContato(Guid contatoId, string nome, string email, string? telefone, string? cargo)
    {
        var contato = BuscarContato(contatoId);
        var dados = ValidarContato(nome, email, telefone, cargo, ignorar: contato);
        contato.Atualizar(dados.Nome, dados.Email, dados.Telefone, dados.Cargo);
    }

    /// <summary>Inativa o contato. Revogar o acesso ao portal (Vinculo, links) é da Application.</summary>
    public void InativarContato(Guid contatoId) => BuscarContato(contatoId).Inativar();

    /// <summary>Chamado quando o contato aceita o convite do portal (RF-CLI-03).</summary>
    public void VincularUsuarioAoContato(Guid contatoId, Guid usuarioId)
    {
        var contato = BuscarContato(contatoId);
        Ids.Obrigatorio(usuarioId, nameof(usuarioId));
        if (!contato.Ativo)
            throw new TransicaoInvalidaException("Contato inativo não pode ser vinculado a um usuário.");
        if (contato.UsuarioId is { } atual && atual != usuarioId)
            throw new TransicaoInvalidaException("O contato já está vinculado a outro usuário.");

        contato.VincularUsuario(usuarioId);
    }

    /// <summary>Acesso ao portal revogado: o contato volta a não ter usuário (o histórico de interações fica).</summary>
    public void DesvincularUsuarioDoContato(Guid contatoId) => BuscarContato(contatoId).DesvincularUsuario();

    private (string Nome, string Email, string? Telefone, string? Cargo) ValidarContato(
        string nome, string email, string? telefone, string? cargo, Contato? ignorar)
    {
        var dados = (
            Texto.Obrigatorio(nome, LimitesTexto.Nome, nameof(nome)),
            NormalizarEmail(email),
            Texto.Opcional(telefone, LimitesTexto.Telefone, nameof(telefone)),
            Texto.Opcional(cargo, LimitesTexto.Cargo, nameof(cargo)));

        if (_contatos.Exists(c => c.Ativo && c != ignorar && c.Email == dados.Item2))
            throw new EmailDuplicadoException($"Já existe contato ativo deste cliente com o e-mail {dados.Item2}.");

        return dados;
    }

    private static string NormalizarEmail(string email) => Common.Email.Normalizar(email, nameof(email));

    private Contato BuscarContato(Guid contatoId) =>
        _contatos.Find(c => c.Id == contatoId)
        ?? throw new ArgumentException("Contato não encontrado neste cliente.", nameof(contatoId));
}
