using System.Text.Json;
using System.Text.Json.Serialization;
using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Cadastro;
using Acordu.Application.Common;
using Acordu.Application.Nebrys;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.UnitTests.Nebrys;

/// <summary>
/// Spec do F14, F14-76 e F14-77 (F14o; design D12 e D14): a área convida a partir de uma entrada da lista de espera, pelo
/// mesmo caminho da emissão do convite, ou descarta a entrada, com a trilha.
/// </summary>
public sealed class ListaEsperaNebrysTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 15, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions ComoATrilha = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    [Fact] // F14-76
    public async Task Convidar_Aguardando_ConviteParaOEmailDaEntradaComAOrigemEEntradaConvidada()
    {
        var m = new Montagem();
        var entrada = m.Entrada("ana@exemplo.com");

        var enviado = await m.Lista.ConvidarAsync(entrada.Id);

        enviado.EmailEnviado.Should().BeTrue();
        var convite = m.Convites.Adicionados.Should().ContainSingle().Subject;
        convite.Id.Should().Be(enviado.Id);
        convite.Email.Should().Be("ana@exemplo.com");
        convite.EntradaListaEsperaId.Should().Be(entrada.Id);
        entrada.Situacao.Should().Be(SituacaoEntradaListaEspera.Convidada);
        entrada.ConvidadaEm.Should().Be(Agora);
        m.Unidade.Salvamentos.Should().Be(1, "convite e entrada na mesma gravação");
        m.Email.Mensagens.Should().ContainSingle().Which.Para.Should().Be("ana@exemplo.com");
        m.Email.EnviadoAntesDeSalvar.Should().BeFalse();
        m.Trilha.Registros.Select(r => r.Acao).Should().BeEquivalentTo(
            [AcoesAuditoria.NebrysConviteEmissao, AcoesAuditoria.NebrysListaConvite]);
        var (_, alvo, alvoId, dados) = m.Trilha.Registros.Single(r => r.Acao == AcoesAuditoria.NebrysListaConvite);
        alvo.Should().Be(ListaEsperaNebrys.AlvoEntrada);
        alvoId.Should().Be(entrada.Id);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(dados, ComoATrilha)).RootElement;
        json.GetProperty("email").GetString().Should().Be("ana@exemplo.com");
        json.GetProperty("antes").GetProperty("situacao").GetString().Should().Be("AGUARDANDO");
        json.GetProperty("depois").GetProperty("situacao").GetString().Should().Be("CONVIDADA");
        json.GetProperty("depois").GetProperty("conviteId").GetGuid().Should().Be(convite.Id);
        JsonSerializer.Serialize(m.Trilha.Registros.Select(r => r.Dados), ComoATrilha)
            .Should().NotContain(convite.TokenHash, "nunca o token nem o hash na trilha");
    }

    [Fact] // F14-76: a entrada já convidada não gera outro convite
    public async Task Convidar_JaConvidada_TransicaoInvalidaSemGravarNemEnviar()
    {
        var m = new Montagem();
        var entrada = m.Entrada("ana@exemplo.com");
        entrada.Convidar(Agora.AddDays(-1));

        var acao = () => m.Lista.ConvidarAsync(entrada.Id);

        await acao.Should().ThrowAsync<TransicaoInvalidaException>().WithMessage("*já foi convidada*");
        m.Convites.Adicionados.Should().BeEmpty();
        m.Unidade.Salvamentos.Should().Be(0);
        m.Email.Mensagens.Should().BeEmpty();
        m.Trilha.Registros.Should().BeEmpty();
    }

    [Fact] // F14-76 com a regra do F14n: um convite em aberto por e-mail
    public async Task Convidar_EmailComConviteEmAberto_TransicaoInvalidaSemGravar()
    {
        var m = new Montagem();
        var entrada = m.Entrada("ana@exemplo.com");
        m.Convites.EmAberto.Add("ana@exemplo.com");

        var acao = () => m.Lista.ConvidarAsync(entrada.Id);

        await acao.Should().ThrowAsync<TransicaoInvalidaException>().WithMessage("*convite em aberto*");
        m.Convites.Adicionados.Should().BeEmpty();
        m.Unidade.Salvamentos.Should().Be(0);
        m.Email.Mensagens.Should().BeEmpty();
    }

    [Fact] // F14-76: a falha do envio não desfaz o convite nem a marca
    public async Task Convidar_EmailForaDoAr_GravaEDevolveEmailNaoEnviado()
    {
        var m = new Montagem(falharEnvio: true);
        var entrada = m.Entrada("ana@exemplo.com");

        var enviado = await m.Lista.ConvidarAsync(entrada.Id);

        enviado.EmailEnviado.Should().BeFalse();
        m.Unidade.Salvamentos.Should().Be(1);
        entrada.Situacao.Should().Be(SituacaoEntradaListaEspera.Convidada);
    }

    [Fact] // F14-77
    public async Task Descartar_Entrada_RemoveComTrilhaSoComOEmailEASituacao()
    {
        var m = new Montagem();
        var entrada = m.Entrada("ana@exemplo.com");

        await m.Lista.DescartarAsync(entrada.Id);

        m.Entradas.Removidas.Should().ContainSingle().Which.Should().BeSameAs(entrada);
        m.Unidade.Salvamentos.Should().Be(1);
        var (acao, alvo, alvoId, dados) = m.Trilha.Registros.Should().ContainSingle().Subject;
        acao.Should().Be(AcoesAuditoria.NebrysListaDescarte);
        alvo.Should().Be(ListaEsperaNebrys.AlvoEntrada);
        alvoId.Should().Be(entrada.Id);
        var json = JsonSerializer.Serialize(dados, ComoATrilha);
        json.Should().Contain("ana@exemplo.com").And.Contain("AGUARDANDO")
            .And.NotContain(entrada.Nome).And.NotContain(entrada.NomeEmpresa).And.NotContain(entrada.Mensagem!);
    }

    [Fact]
    public async Task ConvidarEDescartar_Inexistente_NaoEncontradoSemGravar()
    {
        var m = new Montagem();

        await ((Func<Task>)(() => m.Lista.ConvidarAsync(Guid.NewGuid()))).Should().ThrowAsync<NaoEncontradoException>();
        await ((Func<Task>)(() => m.Lista.DescartarAsync(Guid.NewGuid()))).Should().ThrowAsync<NaoEncontradoException>();
        m.Unidade.Salvamentos.Should().Be(0);
        m.Trilha.Registros.Should().BeEmpty();
    }

    private sealed class Montagem
    {
        public Montagem(bool falharEnvio = false)
        {
            Email = new EmailFalso(falharEnvio, Unidade);
            var convites = new ConvitesEmpresaNebrys(
                Convites, Trilha, Unidade, new RelogioFixo(Agora), Email, new EnderecosPublicos(new Uri("https://acordu.app.br/")),
                new OpcoesCadastro(ModoCadastro.Convite, 7));
            Lista = new ListaEsperaNebrys(Entradas, convites, Trilha, Unidade);
        }

        public EntradasFalsas Entradas { get; } = new();

        public ConvitesFalsos Convites { get; } = new();

        public TrilhaFalsa Trilha { get; } = new();

        public UnidadeContada Unidade { get; } = new();

        public EmailFalso Email { get; }

        public ListaEsperaNebrys Lista { get; }

        public EntradaListaEspera Entrada(string email)
        {
            var entrada = EntradaListaEspera.Registrar(
                Guid.NewGuid(), "Ana Souza", email, "Estúdio Traço", "Quero organizar o atendimento.", Agora.AddDays(-5));
            Entradas.Existentes.Add(entrada);
            return entrada;
        }
    }

    private sealed class EntradasFalsas : IListaEspera
    {
        public List<EntradaListaEspera> Existentes { get; } = [];

        public List<EntradaListaEspera> Removidas { get; } = [];

        public Task<EntradaListaEspera?> ObterAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Existentes.FirstOrDefault(e => e.Id == id));

        public void Remover(EntradaListaEspera entrada) => Removidas.Add(entrada);
    }

    private sealed class ConvitesFalsos : IConvitesEmpresa
    {
        public List<ConviteEmpresa> Adicionados { get; } = [];

        public HashSet<string> EmAberto { get; } = [];

        public void Adicionar(ConviteEmpresa convite) => Adicionados.Add(convite);

        public Task<ConviteEmpresa?> ObterAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Adicionados.FirstOrDefault(c => c.Id == id));

        public Task<bool> HaEmAbertoAsync(string emailNormalizado, CancellationToken cancellationToken = default) =>
            Task.FromResult(EmAberto.Contains(emailNormalizado));
    }

    private sealed class TrilhaFalsa : ITrilhaDaOperacao
    {
        public List<(string Acao, string AlvoTipo, Guid? AlvoId, object? Dados)> Registros { get; } = [];

        public void Registrar(string acao, string alvoTipo, Guid? alvoId, object? dados = null) =>
            Registros.Add((acao, alvoTipo, alvoId, dados));
    }

    private sealed class UnidadeContada : IUnitOfWork
    {
        public int Salvamentos { get; private set; }

        public Task SalvarAsync(CancellationToken cancellationToken = default)
        {
            Salvamentos++;
            return Task.CompletedTask;
        }
    }

    private sealed class EmailFalso(bool falhar, UnidadeContada unidade) : IEnviadorEmail
    {
        public List<MensagemEmail> Mensagens { get; } = [];

        public bool EnviadoAntesDeSalvar { get; private set; }

        public Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancellationToken = default)
        {
            if (unidade.Salvamentos == 0)
                EnviadoAntesDeSalvar = true;
            if (falhar)
                throw new InvalidOperationException("e-mail fora do ar");
            Mensagens.Add(mensagem);
            return Task.CompletedTask;
        }
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
