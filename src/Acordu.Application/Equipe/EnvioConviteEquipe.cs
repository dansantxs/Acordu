using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Application.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Events;

namespace Acordu.Application.Equipe;

/// <summary>
/// Envia o e-mail do convite para a equipe depois do commit, gerando o token aqui (só o hash é gravado antes do envio),
/// como no convite do portal. Convite que deixou de estar pendente não recebe e-mail.
/// </summary>
public sealed class EnvioConviteEquipe : IManipuladorPosCommit<ConviteEquipeCriado>
{
    private readonly IRepositorio<ConviteEquipe> _convites;
    private readonly IRepositorio<Papel> _papeis;
    private readonly ITenantAtual _tenant;
    private readonly IEnviadorEmail _email;
    private readonly EnderecosPublicos _enderecos;
    private readonly RelogioDoTenant _relogio;
    private readonly IUnitOfWork _unidade;

    public EnvioConviteEquipe(
        IRepositorio<ConviteEquipe> convites,
        IRepositorio<Papel> papeis,
        ITenantAtual tenant,
        IEnviadorEmail email,
        EnderecosPublicos enderecos,
        RelogioDoTenant relogio,
        IUnitOfWork unidade)
    {
        ArgumentNullException.ThrowIfNull(convites);
        ArgumentNullException.ThrowIfNull(papeis);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(enderecos);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        _convites = convites;
        _papeis = papeis;
        _tenant = tenant;
        _email = email;
        _enderecos = enderecos;
        _relogio = relogio;
        _unidade = unidade;
    }

    public async Task TratarAsync(ConviteEquipeCriado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        var convite = await _convites.ObterPorIdAsync(evento.ConviteId, cancellationToken);
        if (convite is null || !convite.Pendente)
            return;

        var tenant = await _tenant.ObterAsync(cancellationToken);
        var papel = await _papeis.ObterPorIdAsync(convite.PapelId, cancellationToken);
        var token = convite.GerarToken(_relogio.Agora);
        await _unidade.SalvarAsync(cancellationToken);

        var dias = (int)ConviteEquipe.Validade.TotalDays;
        await _email.EnviarAsync(
            new MensagemEmail(
                convite.Email,
                $"Convite para a equipe de {tenant.Nome} no Acordu",
                $"""
                Olá!

                Você foi convidado para a equipe de {tenant.Nome} no Acordu{(papel is null ? "" : $", com o papel {papel.Nome}")}.

                Para aceitar, acesse o link abaixo (válido por {dias} dias):
                {_enderecos.ConviteEquipe(token)}

                Se você não esperava este convite, pode ignorar este e-mail.
                """,
                "convite-equipe"),
            cancellationToken);
    }
}
