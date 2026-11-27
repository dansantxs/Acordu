using Acordu.Application.Common;
using Acordu.Application.Solicitacoes;
using Acordu.Application.UnitTests.Exemplos;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.UnitTests.Solicitacoes;

/// <summary>
/// Spec do F15, F15i (F15-30 a F15-32; RF-POR-05, RF-SOL-13; design §1.2): o núcleo da resposta do contato é um só, para o
/// portal (canal PORTAL) e para a resposta a um aviso por e-mail (canal EMAIL): mensagem EXTERNA do contato, com o evento
/// de que vêm a retomada e os avisos; a solicitação encerrada não recebe.
/// </summary>
public sealed class RespostaPeloContatoTests
{
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Cliente = Guid.NewGuid();
    private static readonly Guid Contato = Guid.NewGuid();
    private static readonly Guid Autor = Guid.NewGuid();

    private readonly Repositorio<Interacao> _interacoes = new();

    private RespostaPeloContato Resposta()
    {
        var calendarios = new CalendariosFixos(Empresa);
        return new RespostaPeloContato(_interacoes, new RelogioDoTenant(calendarios, new RelogioFixo(Falsos.Agora)));
    }

    private static Solicitacao Aberta() =>
        Solicitacao.Abrir(
            Guid.NewGuid(), Empresa, Cliente, 7, "Forno parou", null, Prioridade.Media, OrigemSolicitacao.Email, Contato, null,
            null, null, null, Falsos.Agora.AddDays(-1), CalendarioUtil.Padrao(Empresa));

    [Theory] // a mesma resposta para o portal e para o e-mail
    [InlineData(CanalMensagem.Portal)]
    [InlineData(CanalMensagem.Email)]
    public void Responder_ConversaAberta_MensagemExternaDoContatoNoCanalComOEvento(CanalMensagem canal)
    {
        var solicitacao = Aberta();

        var interacao = Resposta().Responder(solicitacao, Autor, "Liguei de novo.", canal, []);

        _interacoes.Itens.Should().ContainSingle().Which.Should().BeSameAs(interacao);
        interacao.TenantId.Should().Be(Empresa);
        interacao.ClienteId.Should().Be(Cliente);
        interacao.SolicitacaoId.Should().Be(solicitacao.Id);
        interacao.AutorId.Should().Be(Autor);
        interacao.AutorEhCliente.Should().BeTrue();
        interacao.Visibilidade.Should().Be(Visibilidade.Externa);
        interacao.Canal.Should().Be(canal);
        interacao.Texto.Should().Be("Liguei de novo.");
        interacao.RegistradaEm.Should().Be(Falsos.Agora);
        interacao.Eventos.OfType<InteracaoRegistrada>().Should().ContainSingle()
            .Which.AutorEhCliente.Should().BeTrue("a retomada e os avisos vêm deste evento, nos dois canais");
    }

    [Fact] // RF-SOL-13: a solicitação encerrada não recebe a resposta do contato
    public void Responder_SolicitacaoCancelada_SolicitacaoEncerradaSemMensagem()
    {
        var solicitacao = Aberta();
        solicitacao.Cancelar("Cliente desistiu", Falsos.Agora);

        var responder = () => Resposta().Responder(solicitacao, Autor, "Oi?", CanalMensagem.Email, []);

        responder.Should().Throw<SolicitacaoEncerradaException>();
        _interacoes.Itens.Should().BeEmpty();
    }

    [Fact] // a conferência vem antes de subir os anexos (o portal chama antes do storage)
    public void GarantirConversaAberta_SolicitacaoCancelada_SolicitacaoEncerrada()
    {
        var solicitacao = Aberta();
        solicitacao.Cancelar("Cliente desistiu", Falsos.Agora);

        var garantir = () => RespostaPeloContato.GarantirConversaAberta(solicitacao, comAnexos: false);

        garantir.Should().Throw<SolicitacaoEncerradaException>();
    }

    [Fact] // RESOLVIDA continua aberta à conversa (RF-SOL-10): reabrir é reprovar, com motivo
    public void Responder_SolicitacaoResolvida_EntraSemMudarOStatus()
    {
        var solicitacao = Aberta();
        solicitacao.Iniciar(Falsos.Agora.AddHours(-2));
        solicitacao.Resolver(new Solucao("Feito"), Falsos.Agora.AddHours(-1), CalendarioUtil.Padrao(Empresa));

        Resposta().Responder(solicitacao, Autor, "Ainda esquenta pouco.", CanalMensagem.Email, []);

        _interacoes.Itens.Should().ContainSingle();
        solicitacao.Status.Should().Be(StatusSolicitacao.Resolvida);
        solicitacao.ResolvidaEm.Should().Be(Falsos.Agora.AddHours(-1));
    }
}
