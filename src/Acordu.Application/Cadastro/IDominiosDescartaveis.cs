namespace Acordu.Application.Cadastro;

/// <summary>
/// Lista de domínios de e-mail descartável (RF-AUT-10, F14-60; design do F14, D9): o cadastro e a lista de espera recusam
/// e-mail desses domínios. A lista é um arquivo versionado no repositório, lido uma vez pela Infrastructure, sem chamada a
/// terceiro em runtime.
/// </summary>
public interface IDominiosDescartaveis
{
    /// <summary>
    /// Se o domínio do e-mail, ou um domínio-pai dele (<c>a.b.exemplo.com</c> → <c>b.exemplo.com</c>, <c>exemplo.com</c>),
    /// está na lista. Compara sem diferenciar maiúsculas; domínio com acento (IDN) é comparado na forma ASCII. Sem
    /// <c>@</c>, falso (o formato é conferido antes, por <see cref="Domain.Common.Email"/>).
    /// </summary>
    bool EhDescartavel(string email);
}
