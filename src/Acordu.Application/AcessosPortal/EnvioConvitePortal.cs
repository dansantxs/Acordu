using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Application.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Events;

namespace Acordu.Application.AcessosPortal;

/// <summary>
/// Envia o e-mail do convite depois do commit. O token é gerado aqui e só o hash é gravado antes do envio: nada em claro
/// no banco nem na outbox. Reentrega (pelo menos uma vez) gera token novo e invalida o do e-mail anterior; convite que
/// deixou de estar pendente, ou contato/cliente inativado no meio do caminho, não recebe e-mail.
/// </summary>
public sealed class EnvioConvitePortal : IManipuladorPosCommit<ConvitePortalCriado>
{
    private readonly IRepositorio<ConvitePortal> _convites;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly ITenantAtual _tenant;
    private readonly IEnviadorEmail _email;
    private readonly EnderecosPublicos _enderecos;
    private readonly RelogioDoTenant _relogio;
    private readonly IUnitOfWork _unidade;

    public EnvioConvitePortal(
        IRepositorio<ConvitePortal> convites,
        IRepositorio<Cliente> clientes,
        ITenantAtual tenant,
        IEnviadorEmail email,
        EnderecosPublicos enderecos,
        RelogioDoTenant relogio,
        IUnitOfWork unidade)
    {
        ArgumentNullException.ThrowIfNull(convites);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(enderecos);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        _convites = convites;
        _clientes = clientes;
        _tenant = tenant;
        _email = email;
        _enderecos = enderecos;
        _relogio = relogio;
        _unidade = unidade;
    }

    public async Task TratarAsync(ConvitePortalCriado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        var convite = await _convites.ObterPorIdAsync(evento.ConviteId, cancellationToken);
        if (convite is null || !convite.Pendente)
            return;
        var cliente = await _clientes.ObterPorIdAsync(convite.ClienteId, cancellationToken);
        var contato = cliente?.Contatos.FirstOrDefault(c => c.Id == convite.ContatoId);
        if (cliente is null || contato is null || !cliente.Ativo || !contato.Ativo)
            return;

        var tenant = await _tenant.ObterAsync(cancellationToken);
        var prestador = tenant.NomePortal ?? tenant.Nome;
        var token = convite.GerarToken(_relogio.Agora);
        await _unidade.SalvarAsync(cancellationToken);

        var dias = (int)ConvitePortal.Validade.TotalDays;
        await _email.EnviarAsync(
            new MensagemEmail(
                contato.Email,
                $"{prestador} convidou você para o portal de atendimento",
                $"""
                Olá, {contato.Nome}!

                {prestador} convidou você para abrir e acompanhar suas solicitações pelo portal de atendimento.

                Para aceitar, acesse o link abaixo (válido por {dias} dias):
                {_enderecos.ConvitePortal(token)}

                Se você não esperava este convite, pode ignorar este e-mail.
                """,
                "convite-portal",
                prestador),
            cancellationToken);
    }
}
