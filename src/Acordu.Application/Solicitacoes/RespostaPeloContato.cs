using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Solicitacoes;

/// <summary>
/// O núcleo da resposta do contato na conversa (design do F15, §1.2, "o contato fala"), o mesmo para o portal (canal
/// PORTAL) e para a resposta a um aviso por e-mail (canal EMAIL, F15-30): a mensagem é sempre EXTERNA, do contato, com os
/// anexos já no storage; a solicitação encerrada (FECHADA/CANCELADA) não recebe (RF-SOL-13). Os efeitos vêm do
/// <c>InteracaoRegistrada</c>, iguais nos dois caminhos: a retomada de AGUARDANDO_CLIENTE na mesma transação (RF-POR-05,
/// F15-31), sem mexer na resolução nem no prazo do fechamento automático (RF-SOL-09, F15-32, F15-45), e, depois do commit,
/// os avisos ao responsável e aos seguidores, nunca ao próprio contato (F15-30, F15-56). Só acrescenta à unidade de
/// trabalho: quem chama grava, com o que mais for da mesma transação.
/// </summary>
public sealed class RespostaPeloContato
{
    private readonly IRepositorio<Interacao> _interacoes;
    private readonly RelogioDoTenant _relogio;

    public RespostaPeloContato(IRepositorio<Interacao> interacoes, RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(interacoes);
        ArgumentNullException.ThrowIfNull(relogio);
        _interacoes = interacoes;
        _relogio = relogio;
    }

    /// <summary>
    /// Recusa a resposta à solicitação encerrada (<c>SolicitacaoEncerradaException</c>, RF-SOL-13). Quem chama confere antes
    /// de mandar os arquivos ao storage.
    /// </summary>
    public static void GarantirConversaAberta(Solicitacao solicitacao, bool comAnexos)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        solicitacao.GarantirConversaAberta(doCliente: true, Visibilidade.Externa, comAnexos);
    }

    /// <summary>A mensagem do contato, acrescentada sem gravar.</summary>
    /// <param name="autorId">A conta do contato ou, sem ela (sessão de link, e-mail), o próprio contato.</param>
    /// <param name="canal">Por onde o contato falou (PORTAL ou EMAIL; RF-SOL-14).</param>
    /// <param name="anexos">Já no storage, com a chave da solicitação.</param>
    public Interacao Responder(
        Solicitacao solicitacao, Guid autorId, string? texto, CanalMensagem canal, IReadOnlyList<NovoAnexo> anexos)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        ArgumentNullException.ThrowIfNull(anexos);
        GarantirConversaAberta(solicitacao, anexos.Count > 0);
        var interacao = Interacao.Registrar(
            Guid.CreateVersion7(),
            solicitacao.TenantId,
            solicitacao.ClienteId,
            solicitacao.Id,
            autorId,
            autorEhCliente: true,
            texto,
            Visibilidade.Externa,
            canal,
            anexos,
            ComandosSolicitacao.PoliticaPadrao,
            _relogio.Agora);
        _interacoes.Adicionar(interacao);
        return interacao;
    }
}
