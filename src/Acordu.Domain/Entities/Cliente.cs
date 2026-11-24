using Acordu.Domain.Common;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Cliente do prestador, com seus contatos (modelo de domínio §4.1). Inativar não apaga histórico; a Application
/// bloqueia novas solicitações para cliente inativo.
/// </summary>
public sealed class Cliente : EntidadeBase
{
    /// <summary>Etiquetas por cliente (decidido em 16/10): servem para filtrar a lista, não para classificar a fundo.</summary>
    public const int MaximoEtiquetas = 10;

    private readonly List<Contato> _contatos = [];
    private List<string> _etiquetas = [];

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

    /// <summary>Nome jurídico, quando difere do nome pelo qual a equipe chama o cliente.</summary>
    public string? RazaoSocial { get; private set; }

    /// <summary>Número da inscrição estadual ou <c>ISENTO</c>, em maiúsculas.</summary>
    public string? InscricaoEstadual { get; private set; }

    /// <summary>E-mail geral da empresa (os contatos têm o seu).</summary>
    public string? Email { get; private set; }

    /// <summary>Telefone geral da empresa.</summary>
    public string? Telefone { get; private set; }

    public Endereco? Endereco { get; private set; }

    /// <summary>
    /// Quem da equipe cuida do cliente; sugerido como responsável das solicitações novas dele. Que é membro da equipe é
    /// conferido pela Application (outro agregado).
    /// </summary>
    public Guid? ResponsavelId { get; private set; }

    /// <summary>
    /// Cliente dos dados de exemplo (RF-EMP-05, F14r; design do F14, D13). Tudo o que é ligado a ele pelo
    /// <c>ClienteId</c> conta como exemplo, inclusive o que a equipe criou dentro dele, e sai junto quando os exemplos
    /// são apagados. Só a carga dos exemplos marca; nada desmarca.
    /// </summary>
    public bool Exemplo { get; private set; }

    public IReadOnlyList<string> Etiquetas => _etiquetas;

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

    /// <summary>Dados de cadastro além de nome, documento e observações (decidido em 16/10). Nulo apaga.</summary>
    public void DefinirCadastro(string? razaoSocial, string? inscricaoEstadual, string? email, string? telefone, Endereco? endereco)
    {
        var razao = Texto.Opcional(razaoSocial, LimitesTexto.Nome, nameof(razaoSocial));
        var inscricao = NormalizarInscricao(inscricaoEstadual);
        var emailValido = string.IsNullOrWhiteSpace(email) ? null : Common.Email.Normalizar(email, nameof(email));
        Telefone = Texto.Opcional(telefone, LimitesTexto.Telefone, nameof(telefone));
        RazaoSocial = razao;
        InscricaoEstadual = inscricao;
        Email = emailValido;
        Endereco = endereco;
    }

    public void DefinirResponsavel(Guid? responsavelId)
    {
        if (responsavelId is { } id)
            Ids.Obrigatorio(id, nameof(responsavelId));
        ResponsavelId = responsavelId;
    }

    /// <summary>
    /// Até <see cref="MaximoEtiquetas"/>, aparadas e com espaços internos simples; repetidas (sem diferenciar maiúsculas)
    /// ficam uma vez só, com a grafia da primeira.
    /// </summary>
    public void DefinirEtiquetas(IEnumerable<string>? etiquetas)
    {
        var lista = new List<string>();
        foreach (var bruta in etiquetas ?? [])
        {
            var etiqueta = string.Join(' ', (bruta ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (etiqueta.Length == 0)
                continue;
            if (etiqueta.Length > LimitesTexto.Etiqueta)
                throw new ArgumentException($"Cada etiqueta tem no máximo {LimitesTexto.Etiqueta} caracteres.", nameof(etiquetas));
            if (!lista.Exists(e => string.Equals(e, etiqueta, StringComparison.OrdinalIgnoreCase)))
                lista.Add(etiqueta);
        }

        if (lista.Count > MaximoEtiquetas)
            throw new ArgumentException($"No máximo {MaximoEtiquetas} etiquetas por cliente.", nameof(etiquetas));
        _etiquetas = lista;
    }

    /// <summary>Marca o cliente como dado de exemplo (só a carga dos exemplos chama).</summary>
    public void MarcarComoExemplo() => Exemplo = true;

    public void Inativar() => Ativo = false;

    /// <summary>Cliente que voltou: o histórico continua o mesmo; os acessos ao portal revogados na inativação não voltam.</summary>
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

    /// <summary>
    /// Contato que voltou (decidido em 16/10). Só em cliente ativo, e sem outro contato ativo com o mesmo e-mail. O acesso
    /// ao portal não volta junto: se for o caso, convida-se de novo.
    /// </summary>
    public void ReativarContato(Guid contatoId)
    {
        var contato = BuscarContato(contatoId);
        if (contato.Ativo)
            return;
        if (!Ativo)
            throw new TransicaoInvalidaException("Reative o cliente antes de reativar um contato dele.");
        if (_contatos.Exists(c => c.Ativo && c.Email == contato.Email))
            throw new EmailDuplicadoException($"Já existe contato ativo deste cliente com o e-mail {contato.Email}.");
        contato.Reativar();
    }

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

    private static string? NormalizarInscricao(string? inscricaoEstadual)
    {
        var valor = Texto.Opcional(inscricaoEstadual, LimitesTexto.InscricaoEstadual, nameof(inscricaoEstadual))?.ToUpperInvariant();
        if (valor is null || valor == "ISENTO")
            return valor;
        return valor.All(c => char.IsAsciiDigit(c) || c is '.' or '-' or '/') && valor.Any(char.IsAsciiDigit)
            ? valor
            : throw new ArgumentException("Inscrição estadual inválida: use os números ou ISENTO.", nameof(inscricaoEstadual));
    }

    private static string NormalizarEmail(string email) => Common.Email.Normalizar(email, nameof(email));

    private Contato BuscarContato(Guid contatoId) =>
        _contatos.Find(c => c.Id == contatoId)
        ?? throw new ArgumentException("Contato não encontrado neste cliente.", nameof(contatoId));
}
