using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Application.AcessosPortal;
using Acordu.Application.Common;
using Acordu.Application.EntradaEmail;
using Acordu.Application.Notificacoes;
using Acordu.Application.Notificacoes.Canais;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;

namespace Acordu.Application.UnitTests.Notificacoes;

/// <summary>
/// Spec do F15, F15c (F15-51, F15-52; design D3, D9): quando o aviso cria a conversa da solicitação e quando não, e a
/// ordem "grava a conversa, depois envia" que faz o segundo de dois avisos simultâneos falhar antes de enviar.
/// </summary>
public sealed class AvisosAoContatoConversaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 6, 13, 0, 0, TimeSpan.Zero);
    private static readonly Guid Criador = Guid.NewGuid();

    private readonly Tenant _tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio Traço", Agora.AddDays(-1), Criador, TimeSpan.FromDays(30));
    private readonly Cliente _cliente;
    private readonly Solicitacao _solicitacao;
    private readonly Repositorio<Solicitacao> _solicitacoes = new();
    private readonly Repositorio<Cliente> _clientes = new();
    private readonly Repositorio<AcessoPortal> _acessos = new();
    private readonly Repositorio<ConversaPorEmail> _novasConversas = new();
    private readonly ConversasFalsas _conversas = new();
    private readonly UnidadeFalsa _unidade = new();
    private readonly CanalFalso _canal = new();

    public AvisosAoContatoConversaTests()
    {
        typeof(Tenant).GetProperty(nameof(Tenant.EnvioLiberadoEm))!.SetValue(_tenant, (DateTimeOffset?)Agora.AddDays(-1));
        _cliente = Cliente.Criar(Guid.NewGuid(), _tenant.Id, "Bom Grão", null, null);
        var contato = _cliente.AdicionarContato("Carlos Prado", "carlos@bomgrao.com.br", null, null);
        _solicitacao = Solicitacao.Abrir(
            Guid.NewGuid(), _tenant.Id, _cliente.Id, 7, "Trocar a logo", null, Prioridade.Media, OrigemSolicitacao.Interna,
            contato.Id, null, null, null, null, Agora, CalendarioUtil.Padrao(_tenant.Id));
        _solicitacoes.Itens.Add(_solicitacao);
        _clientes.Itens.Add(_cliente);
        _conversas.Gravadas = _novasConversas.Itens;
    }

    private AvisosAoContato Montar(string? dominio)
    {
        var calendarios = new CalendariosFixos(_tenant.Id);
        var tenantAtual = new TenantFixo(_tenant);
        return new AvisosAoContato(
            _solicitacoes, _clientes, new Repositorio<Interacao>(), _acessos, new AcessosFalsos(), new VinculosFalsos(),
            _novasConversas, _conversas, tenantAtual, new SeletorDeCanal([_canal]), OpcoesEmailEntrada.DaConfiguracao(dominio),
            new EnderecosPublicos(new Uri("https://app.acordu.app.br")),
            new RelogioDoTenant(calendarios, new RelogioFixo(Agora)), _unidade,
            new EnvioATerceiros(tenantAtual, new BloqueadosFalsos(), new OrigemFalsa()));
    }

    private SolicitacaoAberta Evento() => new(_solicitacao.Id, _tenant.Id, _cliente.Id, 7, OrigemSolicitacao.Interna, Agora);

    [Fact]
    public async Task Aviso_RecebimentoLigadoComDominio_CriaAConversaAntesDeEnviarEUsaOEnderecoDela()
    {
        _tenant.LigarRecebimentoEmail();
        _canal.AoEnviar = () => _unidade.Salvamentos.Should().Be(2, "o link e a conversa são gravados antes de enviar");

        await Montar("entrada.acordu.app.br").TratarAsync(Evento(), CancellationToken.None);

        var conversa = _novasConversas.Itens.Should().ContainSingle().Subject;
        conversa.SolicitacaoId.Should().Be(_solicitacao.Id);
        conversa.ClienteId.Should().Be(_cliente.Id);
        conversa.TenantId.Should().Be(_tenant.Id);
        _canal.Avisos.Should().ContainSingle().Which.EnderecoDaConversa
            .Should().Be($"{_tenant.CodigoEntrada}+{conversa.Token}@entrada.acordu.app.br");
    }

    [Fact]
    public async Task Aviso_ConversaJaExiste_NaoCriaOutraEReusaOToken() // F15-51: um token por solicitação
    {
        _tenant.LigarRecebimentoEmail();
        var existente = ConversaPorEmail.Criar(Guid.NewGuid(), _tenant.Id, _cliente.Id, _solicitacao.Id, Agora);
        _conversas.Existentes.Add(existente);

        await Montar("entrada.acordu.app.br").TratarAsync(Evento(), CancellationToken.None);

        _novasConversas.Itens.Should().BeEmpty();
        _canal.Avisos.Should().ContainSingle().Which.EnderecoDaConversa.Should().Contain("+" + existente.Token + "@");
    }

    [Fact]
    public async Task Aviso_RecebimentoLigadoSemDominioDeEntrada_ComoDesligado() // F15-52: sem endereço para responder
    {
        _tenant.LigarRecebimentoEmail();
        typeof(Tenant).GetProperty(nameof(Tenant.ResponderPara))!.SetValue(_tenant, "atendimento@estudiotraco.com.br");

        await Montar(null).TratarAsync(Evento(), CancellationToken.None);

        _novasConversas.Itens.Should().BeEmpty();
        var aviso = _canal.Avisos.Should().ContainSingle().Subject;
        aviso.EnderecoDaConversa.Should().BeNull();
        aviso.ResponderPara.Should().Be("atendimento@estudiotraco.com.br");
    }

    [Fact]
    public async Task Aviso_RecebimentoDesligado_NaoCriaConversa() // F15-52
    {
        await Montar("entrada.acordu.app.br").TratarAsync(Evento(), CancellationToken.None);

        _novasConversas.Itens.Should().BeEmpty();
        _canal.Avisos.Should().ContainSingle().Which.EnderecoDaConversa.Should().BeNull();
    }

    [Fact]
    public async Task Aviso_GravacaoDaConversaEsbarraNoIndiceUnico_FalhaSemEnviar() // corrida de dois avisos (D9)
    {
        _tenant.LigarRecebimentoEmail();
        _unidade.FalharNoSalvamento = 2;

        var acao = () => Montar("entrada.acordu.app.br").TratarAsync(Evento(), CancellationToken.None);

        await acao.Should().ThrowAsync<InvalidOperationException>();
        _canal.Avisos.Should().BeEmpty("a outbox repete o aviso, já com a conversa gravada pelo outro");
    }

    /// <summary>Solicitação aberta por e-mail (F15f) do mesmo contato, com a conversa que nasceu na abertura, no fio do e-mail.</summary>
    private SolicitacaoAberta AbertaPorEmail(OrigemSolicitacao origem = OrigemSolicitacao.Email, bool comFio = true)
    {
        var solicitacao = Solicitacao.Abrir(
            Guid.NewGuid(), _tenant.Id, _cliente.Id, 8, "Forno parou", null, Prioridade.Media, origem,
            _cliente.Contatos.First().Id, null, null, null, null, Agora, CalendarioUtil.Padrao(_tenant.Id));
        _solicitacoes.Itens.Add(solicitacao);
        _conversas.Existentes.Add(comFio
            ? ConversaPorEmail.DaAberturaPorEmail(
                Guid.NewGuid(), _tenant.Id, _cliente.Id, solicitacao.Id, "abc123@mail.bomgrao.com.br", "Forno parou", Agora)
            : ConversaPorEmail.Criar(Guid.NewGuid(), _tenant.Id, _cliente.Id, solicitacao.Id, Agora));
        return new SolicitacaoAberta(solicitacao.Id, _tenant.Id, _cliente.Id, 8, origem, Agora);
    }

    [Fact] // F15-16, F15-50: a conversa da abertura serve ao aviso: o endereço dela e o fio do e-mail
    public async Task Aviso_OrigemEmailRecebimentoLigado_LevaOEnderecoDaConversaEOFio()
    {
        _tenant.LigarRecebimentoEmail();
        var evento = AbertaPorEmail();

        await Montar("entrada.acordu.app.br").TratarAsync(evento, CancellationToken.None);

        _novasConversas.Itens.Should().BeEmpty("a conversa já nasceu na abertura");
        var aviso = _canal.Avisos.Should().ContainSingle().Subject;
        aviso.Origem.Should().Be(OrigemSolicitacao.Email);
        aviso.EnderecoDaConversa.Should().Contain("+" + _conversas.Existentes[0].Token + "@entrada.acordu.app.br");
        aviso.MessageIdOrigem.Should().Be("abc123@mail.bomgrao.com.br");
        aviso.AssuntoOrigem.Should().Be("Forno parou");
    }

    [Fact] // design D4: o fio segue a origem; o endereço da conversa segue o recebimento (F15-52)
    public async Task Aviso_OrigemEmailRecebimentoDesligado_LevaOFioSemOEnderecoDaConversa()
    {
        var evento = AbertaPorEmail();

        await Montar("entrada.acordu.app.br").TratarAsync(evento, CancellationToken.None);

        var aviso = _canal.Avisos.Should().ContainSingle().Subject;
        aviso.EnderecoDaConversa.Should().BeNull();
        aviso.MessageIdOrigem.Should().Be("abc123@mail.bomgrao.com.br");
        aviso.AssuntoOrigem.Should().Be("Forno parou");
        _novasConversas.Itens.Should().BeEmpty();
    }

    [Fact] // design D4: sem Message-ID, a conversa existe sem o fio, e o aviso sai com os assuntos de sempre
    public async Task Aviso_OrigemEmailSemFioGuardado_SemMessageIdNemAssuntoDeOrigem()
    {
        _tenant.LigarRecebimentoEmail();
        var evento = AbertaPorEmail(comFio: false);

        await Montar("entrada.acordu.app.br").TratarAsync(evento, CancellationToken.None);

        var aviso = _canal.Avisos.Should().ContainSingle().Subject;
        aviso.MessageIdOrigem.Should().BeNull();
        aviso.AssuntoOrigem.Should().BeNull();
        aviso.EnderecoDaConversa.Should().NotBeNull();
    }

    [Fact] // design D4: desligado e sem conversa, nada é criado nem lido, mesmo na origem EMAIL (F15-52)
    public async Task Aviso_OrigemEmailRecebimentoDesligadoSemConversa_NaoCriaConversaNemFio()
    {
        var evento = AbertaPorEmail();
        _conversas.Existentes.Clear();

        await Montar("entrada.acordu.app.br").TratarAsync(evento, CancellationToken.None);

        _novasConversas.Itens.Should().BeEmpty();
        var aviso = _canal.Avisos.Should().ContainSingle().Subject;
        aviso.EnderecoDaConversa.Should().BeNull();
        aviso.MessageIdOrigem.Should().BeNull();
    }

    [Theory] // F15-51: as outras origens seguem como antes, mesmo que uma conversa com fio exista
    [InlineData(OrigemSolicitacao.Portal)]
    [InlineData(OrigemSolicitacao.Interna)]
    public async Task Aviso_OutraOrigem_NuncaLevaOFio(OrigemSolicitacao origem)
    {
        _tenant.LigarRecebimentoEmail();
        var evento = AbertaPorEmail(origem);

        await Montar("entrada.acordu.app.br").TratarAsync(evento, CancellationToken.None);

        var aviso = _canal.Avisos.Should().ContainSingle().Subject;
        aviso.MessageIdOrigem.Should().BeNull();
        aviso.AssuntoOrigem.Should().BeNull();
    }

    private sealed class Repositorio<T> : IRepositorio<T>
        where T : EntidadeBase
    {
        public List<T> Itens { get; } = [];

        public Task<T?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Itens.Find(i => i.Id == id));

        public void Adicionar(T entidade) => Itens.Add(entidade);

        public void Remover(T entidade) => Itens.Remove(entidade);
    }

    private sealed class ConversasFalsas : IConversasPorEmail
    {
        public List<ConversaPorEmail> Existentes { get; } = [];

        public List<ConversaPorEmail> Gravadas { get; set; } = [];

        public Task<ConversaPorEmail?> DaSolicitacaoAsync(Guid solicitacaoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Existentes.Concat(Gravadas).FirstOrDefault(c => c.SolicitacaoId == solicitacaoId));
    }

    private sealed class UnidadeFalsa : IUnitOfWork
    {
        public int Salvamentos { get; private set; }

        public int FalharNoSalvamento { get; set; }

        public Task SalvarAsync(CancellationToken cancellationToken = default)
        {
            Salvamentos++;
            if (Salvamentos == FalharNoSalvamento)
                throw new InvalidOperationException("índice único");
            return Task.CompletedTask;
        }
    }

    private sealed class CanalFalso : ICanalDeAviso
    {
        public List<AvisoAoContato> Avisos { get; } = [];

        public Action? AoEnviar { get; set; }

        public CanalAviso Canal => CanalAviso.Email;

        public Task<bool> EnviarAsync(AvisoAoContato aviso, CancellationToken cancellationToken = default)
        {
            AoEnviar?.Invoke();
            Avisos.Add(aviso);
            return Task.FromResult(true);
        }
    }

    private sealed class TenantFixo(Tenant tenant) : ITenantAtual
    {
        public Task<Tenant> ObterAsync(CancellationToken cancellationToken = default) => Task.FromResult(tenant);
    }

    private sealed class AcessosFalsos : IAcessosDoPortal
    {
        public Task<IReadOnlyList<ConvitePortal>> ConvitesPendentesAsync(Guid contatoId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ConvitePortal>>([]);

        public Task<IReadOnlyList<AcessoPortal>> LinksNaoRevogadosAsync(Guid contatoId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AcessoPortal>>([]);
    }

    private sealed class VinculosFalsos : IVinculos
    {
        public Task<Vinculo?> DoUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) => Task.FromResult<Vinculo?>(null);

        public Task<int> ContarDonosAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> ContarPorPapelAsync(Guid papelId, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task RevogarSessoesAsync(Guid usuarioId, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> EmailJaVinculadoAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class BloqueadosFalsos : IEnviosBloqueados
    {
        public void Registrar(string categoria)
        {
        }
    }

    private sealed class OrigemFalsa : IOrigemDoEvento
    {
        public Guid? AutorId => null;

        public Guid? MensagemId => null;

        public DateTimeOffset? OcorridoEm => null;
    }

    private sealed class CalendariosFixos(Guid tenantId) : ICalendarios
    {
        public Task<CalendarioUtil> DoTenantAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CalendarioUtil.Padrao(tenantId));

        public Task<CalendarioUtil> ParaEdicaoAsync(CancellationToken cancellationToken = default) => DoTenantAsync(cancellationToken);
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
