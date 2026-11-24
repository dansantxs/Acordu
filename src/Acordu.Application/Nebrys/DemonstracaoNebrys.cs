using Acordu.Application.Common;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.Nebrys;

/// <summary>
/// Empresa de demonstração pela área da Nebrys (RF-NEB-02; F14-87 a F14-90, F14-110; design do F14, D13): criar, com os
/// dados fictícios do <c>demonstracao.json</c> (os mesmos do manual); restaurar, que volta a ela aos dados iniciais sem
/// tocar outra empresa; e indicar quem entra nela como Dono, só com conta que já existe no Acordu (decisão 17 do G2): a
/// demonstração não envia convite. Há uma demonstração só. Cada ação vai para a trilha sem tenant, com o operador (D14).
/// <para>
/// O trabalho fica na porta (<see cref="IEmpresaDeDemonstracao"/>), na Infrastructure, num escopo próprio com o tenant da
/// demonstração fixado (ADR 0014, item 5): a área continua sem ver dado de negócio, e este caso de uso não conhece tipo
/// nenhum de negócio (teste de arquitetura).
/// </para>
/// </summary>
public sealed class DemonstracaoNebrys
{
    /// <summary>Campo do e-mail no corpo da indicação.</summary>
    public const string CampoEmail = "email";

    /// <summary>A recusa do F14-110: e-mail sem conta (a demonstração não cria conta nem manda convite).</summary>
    public const string MensagemSemConta =
        "Este e-mail não tem conta no Acordu. A demonstração não manda convite: indique só quem já tem conta (a pessoa cria a "
        + "dela pelo cadastro ou por um convite de empresa) e tente de novo.";

    public const string MensagemJaIndicada = "Esta pessoa já está na empresa de demonstração.";

    private readonly IEmpresaDeDemonstracao _demonstracao;

    public DemonstracaoNebrys(IEmpresaDeDemonstracao demonstracao)
    {
        ArgumentNullException.ThrowIfNull(demonstracao);
        _demonstracao = demonstracao;
    }

    /// <summary>A demonstração (nula se ainda não foi criada) e as pessoas indicadas.</summary>
    public Task<DemonstracaoNebrysDto> ObterAsync(CancellationToken cancellationToken = default) =>
        _demonstracao.ObterAsync(cancellationToken);

    /// <summary>
    /// Cria a demonstração (F14-87): ATIVA e fora do ciclo de situação, com a equipe, os clientes e o resto dos dados
    /// fictícios, sem nenhum e-mail. Já existe uma → <c>422 transicao-invalida</c> (restaurar volta aos dados iniciais).
    /// </summary>
    public async Task<DemonstracaoNebrysDto> CriarAsync(CancellationToken cancellationToken = default)
    {
        await _demonstracao.CriarAsync(cancellationToken);
        return await _demonstracao.ObterAsync(cancellationToken);
    }

    /// <summary>
    /// Restaura (F14-88): apaga tudo o que a demonstração tem e semeia de novo, mantendo a empresa, as contas fictícias e as
    /// pessoas indicadas (de novo como Dono). Sem demonstração → <c>404</c>.
    /// </summary>
    public async Task<DemonstracaoNebrysDto> RestaurarAsync(CancellationToken cancellationToken = default)
    {
        await _demonstracao.RestaurarAsync(cancellationToken);
        return await _demonstracao.ObterAsync(cancellationToken);
    }

    /// <summary>
    /// Indica uma pessoa (F14-90): a conta do e-mail entra na demonstração como Dono e a vê na troca de empresa. E-mail vazio
    /// ou inválido → <c>400</c> no campo; sem conta no Acordu → <c>400</c> no campo, sem criar nada nem enviar e-mail
    /// (F14-110); já na demonstração → <c>422 transicao-invalida</c>; sem demonstração → <c>404</c>.
    /// </summary>
    public async Task<DemonstracaoNebrysDto> IndicarAsync(IndicacaoDemonstracaoRequest corpo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(corpo);
        var email = Domain.Common.Email.Normalizar(corpo.Email ?? string.Empty, CampoEmail);
        var resultado = await _demonstracao.IndicarAsync(email, cancellationToken);
        return resultado switch
        {
            ResultadoIndicacao.Indicada => await _demonstracao.ObterAsync(cancellationToken),
            ResultadoIndicacao.SemConta => throw ErroDeCampo.Criar(CampoEmail, MensagemSemConta),
            _ => throw new TransicaoInvalidaException(MensagemJaIndicada),
        };
    }

    /// <summary>
    /// Tira a pessoa indicada da demonstração: o vínculo sai e as sessões dela ali são revogadas. Quem não é indicado (as
    /// contas fictícias) ou sem demonstração → <c>404</c>.
    /// </summary>
    public Task RetirarAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        _demonstracao.RetirarAsync(usuarioId, cancellationToken);
}

/// <summary>
/// A empresa de demonstração, implementada na Infrastructure (ADR 0014, item 5; design do F14, D13): cada ação roda num
/// escopo próprio com o tenant da demonstração fixado (o escopo da requisição de operação continua sem tenant) e grava a
/// linha da trilha sem tenant, com o operador, na mesma transação. Nenhum e-mail sai de nenhuma delas.
/// </summary>
public interface IEmpresaDeDemonstracao
{
    Task<DemonstracaoNebrysDto> ObterAsync(CancellationToken cancellationToken = default);

    /// <summary>Já existe uma demonstração, ou uma conta com senha usa um dos e-mails fictícios → <c>422</c>.</summary>
    Task<Guid> CriarAsync(CancellationToken cancellationToken = default);

    /// <summary>Sem demonstração → <see cref="NaoEncontradoException"/>.</summary>
    Task RestaurarAsync(CancellationToken cancellationToken = default);

    /// <param name="email">Já normalizado.</param>
    Task<ResultadoIndicacao> IndicarAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Sem demonstração, ou a conta não é uma pessoa indicada nela → <see cref="NaoEncontradoException"/>.</summary>
    Task RetirarAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public enum ResultadoIndicacao
{
    Indicada,
    SemConta,
    JaIndicada,
}

/// <summary>Corpo do <c>POST /nebrys/demonstracao/indicacoes</c>.</summary>
public sealed record IndicacaoDemonstracaoRequest(string? Email);

/// <summary>
/// A demonstração na área (contrato §17): a empresa (campos nulos enquanto ela não existe) e as pessoas indicadas. Só a
/// relação com a Nebrys: nada dos dados fictícios de negócio.
/// </summary>
/// <param name="EmpresaId">Nulo: a demonstração ainda não foi criada.</param>
public sealed record DemonstracaoNebrysDto(
    Guid? EmpresaId,
    string? Nome,
    DateTimeOffset? CriadaEm,
    IReadOnlyList<PessoaIndicadaNebrysDto> Indicadas);

/// <summary>Uma pessoa indicada para a demonstração: entra nela como Dono.</summary>
public sealed record PessoaIndicadaNebrysDto(Guid UsuarioId, string Nome, string Email);
