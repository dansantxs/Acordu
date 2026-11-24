using Acordu.Application.Cadastro;
using Acordu.Domain.Entities;

namespace Acordu.Application.ListaEspera;

/// <summary>
/// Lista de espera pública (RF-AUT-08, F14-52 a F14-55; decisão 5 do G2 do F14; design D12): quem quer usar o Acordu
/// enquanto o cadastro é só por convite deixa nome, e-mail, nome da empresa e uma mensagem opcional. O Turnstile e o limite
/// por IP ficam na rota, antes daqui (F14-54). A entrada é gravada fora dos tenants, com a data; o e-mail que já está na
/// lista não duplica, e a resposta é a mesma nos dois caminhos (F14-53: não revela quem está na lista). Nenhum e-mail sai.
/// E-mail de domínio descartável é recusado como no cadastro (RF-AUT-10, F14-60).
/// </summary>
public sealed class InscricaoListaEspera
{
    private readonly IInscricoesListaEspera _inscricoes;
    private readonly TimeProvider _relogio;
    private readonly IDominiosDescartaveis _descartaveis;

    public InscricaoListaEspera(IInscricoesListaEspera inscricoes, TimeProvider relogio, IDominiosDescartaveis descartaveis)
    {
        ArgumentNullException.ThrowIfNull(inscricoes);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(descartaveis);
        _inscricoes = inscricoes;
        _relogio = relogio;
        _descartaveis = descartaveis;
    }

    /// <summary>
    /// Campo obrigatório vazio, e-mail inválido ou texto longo demais → <c>400</c> no campo, sem gravar (F14-55); e-mail de
    /// domínio descartável → <c>400</c> no <c>email</c>, sem gravar (F14-60), depois dos campos. Não devolve nada: a Api
    /// responde igual com ou sem a entrada nova.
    /// </summary>
    public async Task InscreverAsync(ListaEsperaRequest corpo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(corpo);
        var entrada = EntradaListaEspera.Registrar(
            Guid.CreateVersion7(), corpo.Nome, corpo.Email, corpo.NomeEmpresa, corpo.Mensagem, _relogio.GetUtcNow());
        MensagensDeCadastro.RecusarDescartavel(_descartaveis, entrada.Email, MensagensDeCadastro.CampoEmail);
        await _inscricoes.GravarSeNovaAsync(entrada, cancellationToken);
    }
}

/// <summary>
/// Corpo do <c>POST /lista-espera</c> (público): nome, e-mail e nome da empresa obrigatórios, mensagem opcional ("o que
/// você quer resolver") e o <see cref="TokenTurnstile"/> do desafio, exigido só com o Turnstile ligado no servidor (ação
/// <c>lista-espera</c>).
/// </summary>
public sealed record ListaEsperaRequest(
    string? Nome,
    string? Email,
    string? NomeEmpresa,
    string? Mensagem = null,
    string? TokenTurnstile = null);
