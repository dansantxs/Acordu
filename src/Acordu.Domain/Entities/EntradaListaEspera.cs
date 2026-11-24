using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;

namespace Acordu.Domain.Entities;

/// <summary>
/// Entrada da lista de espera do Acordu (RF-AUT-08, F14o; decisão 5 do G2 do F14; design D12): quem pediu para entrar
/// enquanto o cadastro é só por convite. Global, fora dos tenants (a empresa ainda não existe), sem referência a empresa
/// (ADR 0014, item 7). Um e-mail fica na lista uma vez só (índice único, F14-53). A conversão é manual: a Nebrys convida
/// a partir da entrada (F14-76), e o convite guarda a entrada de origem; ou descarta, e a entrada é apagada (F14-77).
/// </summary>
public sealed class EntradaListaEspera
{
    // Só o EF materializa.
    private EntradaListaEspera()
    {
        Nome = null!;
        Email = null!;
        NomeEmpresa = null!;
    }

    private EntradaListaEspera(Guid id, string nome, string email, string nomeEmpresa, string? mensagem, DateTimeOffset criadaEm)
    {
        Id = id;
        Nome = nome;
        Email = email;
        NomeEmpresa = nomeEmpresa;
        Mensagem = mensagem;
        CriadaEm = criadaEm;
    }

    public Guid Id { get; private set; }

    /// <summary>Nome de quem pediu.</summary>
    public string Nome { get; private set; }

    /// <summary>Normalizado por <see cref="Common.Email"/> (aparado, minúsculo): é por ele que a lista não duplica.</summary>
    public string Email { get; private set; }

    public string NomeEmpresa { get; private set; }

    /// <summary>"O que você quer resolver" (opcional); vazio vira nulo.</summary>
    public string? Mensagem { get; private set; }

    /// <summary>Chegada na lista: a ordem da lista na área (F14-75).</summary>
    public DateTimeOffset CriadaEm { get; private set; }

    /// <summary>Quando a Nebrys convidou a partir da entrada (F14-76); nulo enquanto aguarda.</summary>
    public DateTimeOffset? ConvidadaEm { get; private set; }

    /// <summary>Aguardando até a Nebrys convidar; convidada depois.</summary>
    public SituacaoEntradaListaEspera Situacao =>
        ConvidadaEm is null ? SituacaoEntradaListaEspera.Aguardando : SituacaoEntradaListaEspera.Convidada;

    /// <summary>
    /// A entrada que chega pela página pública (F14-52). Campo obrigatório vazio ou e-mail inválido →
    /// <see cref="ArgumentException"/> no parâmetro do campo (<c>nome</c>, <c>email</c>, <c>nomeEmpresa</c>,
    /// <c>mensagem</c>), na ordem do formulário (F14-55).
    /// </summary>
    public static EntradaListaEspera Registrar(
        Guid id, string? nome, string? email, string? nomeEmpresa, string? mensagem, DateTimeOffset agora)
    {
        var nomeValido = Obrigatorio(nome, LimitesTexto.Nome, nameof(nome), "Informe o seu nome.");
        var emailValido = string.IsNullOrWhiteSpace(email)
            ? throw new ArgumentException("Informe o e-mail.", nameof(email))
            : Common.Email.Normalizar(email, nameof(email));
        var empresaValida = Obrigatorio(nomeEmpresa, LimitesTexto.Nome, nameof(nomeEmpresa), "Informe o nome da empresa.");
        var mensagemValida = Texto.Opcional(mensagem, LimitesTexto.MensagemListaEspera, nameof(mensagem));
        return new EntradaListaEspera(
            Ids.Obrigatorio(id, nameof(id)), nomeValido, emailValido, empresaValida, mensagemValida, agora.ToUniversalTime());
    }

    /// <summary>
    /// A Nebrys convida a partir da entrada (F14-76): fica convidada. Já convidada → <see cref="TransicaoInvalidaException"/>
    /// (o convite que saiu dela se reenvia ou revoga pela lista de convites).
    /// </summary>
    public void Convidar(DateTimeOffset agora)
    {
        if (ConvidadaEm is not null)
            throw new TransicaoInvalidaException(
                "Esta entrada já foi convidada: reenvie ou revogue o convite que saiu dela na lista de convites.");
        ConvidadaEm = agora.ToUniversalTime();
    }

    private static string Obrigatorio(string? valor, int maximo, string parametro, string mensagem) =>
        string.IsNullOrWhiteSpace(valor)
            ? throw new ArgumentException(mensagem, parametro)
            : Texto.Obrigatorio(valor, maximo, parametro);
}
