using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using static Acordu.Domain.UnitTests.Calendarios;

namespace Acordu.Domain.UnitTests;

/// <summary>Solicitações de apoio aos testes. Abertas terça 06/10/2026 10:00 (Brasília), prioridade URGENTE.</summary>
internal static class Solicitacoes
{
    public static readonly Guid ContratoId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static readonly DateTimeOffset Abertura = Brasilia(6, 10);

    public static Solicitacao Abrir(
        bool comContrato = true,
        Prioridade prioridade = Prioridade.Urgente,
        DateTimeOffset? agora = null) =>
        Solicitacao.Abrir(
            Guid.NewGuid(),
            Tenant,
            Contratos.Cliente,
            numero: 1042,
            titulo: "Ajustar banner da campanha",
            descricao: "Trocar a data para 15/10.",
            prioridade,
            OrigemSolicitacao.Interna,
            contatoId: null,
            categoriaId: null,
            responsavelId: null,
            comContrato ? ContratoId : null,
            comContrato ? Contratos.PoliticaPadrao() : null,
            agora ?? Abertura,
            Padrao());

    public static Solucao Solucao() => new("Arte refeita e publicada.");

    /// <summary>Leva uma solicitação nova até <paramref name="status"/> por um caminho válido, às 10:30 da abertura.</summary>
    public static Solicitacao Em(StatusSolicitacao status, bool comContrato = true)
    {
        var solicitacao = Abrir(comContrato);
        var instante = Brasilia(6, 10, 30);
        switch (status)
        {
            case StatusSolicitacao.Aberta:
                break;
            case StatusSolicitacao.EmAndamento:
                solicitacao.Iniciar(instante);
                break;
            case StatusSolicitacao.AguardandoCliente:
                solicitacao.AguardarCliente(instante);
                break;
            case StatusSolicitacao.AguardandoTerceiro:
                solicitacao.AguardarTerceiro(instante);
                break;
            case StatusSolicitacao.Resolvida:
                solicitacao.Resolver(Solucao(), instante, Padrao());
                break;
            case StatusSolicitacao.Fechada:
                solicitacao.Resolver(Solucao(), instante, Padrao());
                solicitacao.Fechar(instante);
                break;
            case StatusSolicitacao.Cancelada:
                solicitacao.Cancelar("Duplicada", instante);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        solicitacao.LimparEventos();
        return solicitacao;
    }
}
