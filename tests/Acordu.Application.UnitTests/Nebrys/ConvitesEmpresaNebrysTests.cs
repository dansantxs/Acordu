using System.Text.Json;
using System.Text.Json.Serialization;
using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Cadastro;
using Acordu.Application.Common;
using Acordu.Application.Empresas;
using Acordu.Application.Nebrys;
using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.UnitTests.Nebrys;

/// <summary>
/// Spec do F14, F14-72 a F14-74 (F14n; design D12 e D14): a área emite, reenvia e revoga convites de empresa, manda o link
/// depois de gravar, registra na trilha o e-mail e as datas (nunca o token) e informa a falha do envio sem desfazer.
/// </summary>
public sealed class ConvitesEmpresaNebrysTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 15, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions ComoATrilha = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    [Fact] // F14-72
    public async Task Emitir_EmailNovo_GravaTrilhaEEnviaOLinkDepoisDeGravar()
    {
        var m = new Montagem();

        var enviado = await m.Casos.EmitirAsync(new ConviteEmpresaRequest(" Ana@Exemplo.com "));

        enviado.EmailEnviado.Should().BeTrue();
        var convite = m.Convites.Adicionados.Should().ContainSingle().Subject;
        convite.Id.Should().Be(enviado.Id);
        convite.Email.Should().Be("ana@exemplo.com");
        convite.ExpiraEm.Should().Be(Agora + TimeSpan.FromDays(7));
        m.Unidade.Salvamentos.Should().Be(1);
        m.Email.EnviadoAntesDeSalvar.Should().BeFalse("o e-mail sai depois do commit");
        var mensagem = m.Email.Mensagens.Should().ContainSingle().Subject;
        mensagem.Para.Should().Be("ana@exemplo.com");
        mensagem.Categoria.Should().Be(ConvitesEmpresaNebrys.CategoriaEmail);
        mensagem.Texto.Should().Contain("https://acordu.app.br/cadastro/convite/").And.Contain("7 dias");
        var token = TokenDoLink(mensagem.Texto);
        convite.Validar(token, Agora).Should().BeTrue("o link do e-mail é o do convite");
        var (acao, alvo, alvoId, dados) = m.Trilha.Registros.Should().ContainSingle().Subject;
        acao.Should().Be(AcoesAuditoria.NebrysConviteEmissao);
        alvo.Should().Be("convite");
        alvoId.Should().Be(convite.Id);
        var json = JsonSerializer.Serialize(dados, ComoATrilha);
        json.Should().Contain("ana@exemplo.com").And.Contain("PENDENTE").And.NotContain(token).And.NotContain(convite.TokenHash);
    }

    [Fact] // F14-72: a falha do envio não desfaz o convite
    public async Task Emitir_EmailForaDoAr_GravaEDevolveEmailNaoEnviado()
    {
        var m = new Montagem(falharEnvio: true);

        var enviado = await m.Casos.EmitirAsync(new ConviteEmpresaRequest("ana@exemplo.com"));

        enviado.EmailEnviado.Should().BeFalse();
        m.Unidade.Salvamentos.Should().Be(1);
        m.Convites.Adicionados.Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sem-arroba")]
    public async Task Emitir_EmailVazioOuInvalido_400NoCampoSemGravar(string? email)
    {
        var m = new Montagem();

        var acao = () => m.Casos.EmitirAsync(new ConviteEmpresaRequest(email));

        (await acao.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be(ConvitesEmpresaNebrys.CampoEmail);
        m.Unidade.Salvamentos.Should().Be(0);
        m.Email.Mensagens.Should().BeEmpty();
    }

    [Fact]
    public async Task Emitir_EmailComConviteEmAberto_TransicaoInvalidaSemGravarNemEnviar()
    {
        var m = new Montagem();
        m.Convites.EmAberto.Add("ana@exemplo.com");

        var acao = () => m.Casos.EmitirAsync(new ConviteEmpresaRequest("ANA@exemplo.com"));

        await acao.Should().ThrowAsync<TransicaoInvalidaException>().WithMessage("*reenvie*");
        m.Convites.Adicionados.Should().BeEmpty();
        m.Unidade.Salvamentos.Should().Be(0);
        m.Email.Mensagens.Should().BeEmpty();
        m.Trilha.Registros.Should().BeEmpty();
    }

    [Fact] // #224: o cadastro recusaria o e-mail (F14-60), então o convite não sai
    public async Task Emitir_EmailDescartavel_400NoEmailSemGravarEnviarNemTrilha()
    {
        var m = new Montagem(descartavel: "descartavel.test");

        var acao = () => m.Casos.EmitirAsync(new ConviteEmpresaRequest(" Ana@Descartavel.TEST "));

        var erro = (await acao.Should().ThrowAsync<ArgumentException>()).Which;
        erro.ParamName.Should().Be(ConvitesEmpresaNebrys.CampoEmail);
        erro.Message.Should().StartWith(MensagensDeCadastro.EmailDescartavel);
        m.Dominios.Conferidos.Should().ContainSingle().Which.Should().Be("ana@descartavel.test", "confere o e-mail já normalizado");
        m.Convites.Adicionados.Should().BeEmpty();
        m.Convites.ConsultasEmAberto.Should().Be(0, "a recusa vem antes do banco");
        m.Unidade.Salvamentos.Should().Be(0);
        m.Email.Mensagens.Should().BeEmpty();
        m.Trilha.Registros.Should().BeEmpty();
    }

    [Fact] // #224: o domínio comum segue recebendo o convite
    public async Task Emitir_EmailDeDominioComum_ConfereAListaEEmite()
    {
        var m = new Montagem(descartavel: "descartavel.test");

        var enviado = await m.Casos.EmitirAsync(new ConviteEmpresaRequest("ana@exemplo.com"));

        enviado.EmailEnviado.Should().BeTrue();
        m.Dominios.Conferidos.Should().ContainSingle().Which.Should().Be("ana@exemplo.com");
        m.Convites.Adicionados.Should().ContainSingle();
    }

    [Fact] // F14-73
    public async Task Reenviar_Pendente_LinkNovoOAnteriorParaDeValerETrilhaComAntesEDepois()
    {
        var m = new Montagem();
        var (convite, anterior) = ConviteEmpresa.Emitir(Guid.NewGuid(), "ana@exemplo.com", TimeSpan.FromDays(7), Agora.AddDays(-3));
        m.Convites.Existentes.Add(convite);

        var enviado = await m.Casos.ReenviarAsync(convite.Id);

        enviado.EmailEnviado.Should().BeTrue();
        convite.Validar(anterior, Agora).Should().BeFalse();
        convite.ExpiraEm.Should().Be(Agora + TimeSpan.FromDays(7));
        m.Unidade.Salvamentos.Should().Be(1);
        var token = TokenDoLink(m.Email.Mensagens.Single().Texto);
        convite.Validar(token, Agora).Should().BeTrue();
        var (acao, _, alvoId, dados) = m.Trilha.Registros.Single();
        acao.Should().Be(AcoesAuditoria.NebrysConviteReenvio);
        alvoId.Should().Be(convite.Id);
        var elemento = JsonDocument.Parse(JsonSerializer.Serialize(dados, ComoATrilha)).RootElement;
        elemento.GetProperty("antes").GetProperty("expiraEm").GetDateTimeOffset().Should().Be(Agora.AddDays(4));
        elemento.GetProperty("depois").GetProperty("expiraEm").GetDateTimeOffset().Should().Be(Agora.AddDays(7));
    }

    [Fact] // F14-74
    public async Task Revogar_Pendente_ORevogadoGravaComTrilhaSemEnviar()
    {
        var m = new Montagem();
        var (convite, token) = ConviteEmpresa.Emitir(Guid.NewGuid(), "ana@exemplo.com", TimeSpan.FromDays(7), Agora.AddDays(-1));
        m.Convites.Existentes.Add(convite);

        await m.Casos.RevogarAsync(convite.Id);

        convite.RevogadoEm.Should().Be(Agora);
        convite.Validar(token, Agora).Should().BeFalse();
        m.Unidade.Salvamentos.Should().Be(1);
        m.Email.Mensagens.Should().BeEmpty();
        var (acao, _, _, dados) = m.Trilha.Registros.Single();
        acao.Should().Be(AcoesAuditoria.NebrysConviteRevogacao);
        JsonSerializer.Serialize(dados, ComoATrilha).Should().Contain("\"situacao\":\"REVOGADO\"");
    }

    [Fact]
    public async Task ReenviarERevogar_Inexistente_NaoEncontrado()
    {
        var m = new Montagem();

        await ((Func<Task>)(() => m.Casos.ReenviarAsync(Guid.NewGuid()))).Should().ThrowAsync<NaoEncontradoException>();
        await ((Func<Task>)(() => m.Casos.RevogarAsync(Guid.NewGuid()))).Should().ThrowAsync<NaoEncontradoException>();
        m.Unidade.Salvamentos.Should().Be(0);
    }

    [Fact]
    public async Task Reenviar_Usado_TransicaoInvalidaSemGravarNemEnviar()
    {
        var m = new Montagem();
        var (convite, token) = ConviteEmpresa.Emitir(Guid.NewGuid(), "ana@exemplo.com", TimeSpan.FromDays(7), Agora.AddDays(-1));
        convite.Usar(token, "ana@exemplo.com", Agora.AddHours(-1));
        m.Convites.Existentes.Add(convite);

        await ((Func<Task>)(() => m.Casos.ReenviarAsync(convite.Id))).Should().ThrowAsync<TransicaoInvalidaException>();
        m.Unidade.Salvamentos.Should().Be(0);
        m.Email.Mensagens.Should().BeEmpty();
        m.Trilha.Registros.Should().BeEmpty();
    }

    /// <summary>O token do link do e-mail: os 43 caracteres depois de <c>/cadastro/convite/</c>.</summary>
    private static string TokenDoLink(string texto)
    {
        const string Prefixo = "/cadastro/convite/";
        var inicio = texto.IndexOf(Prefixo, StringComparison.Ordinal) + Prefixo.Length;
        return texto.Substring(inicio, 43);
    }

    private sealed class Montagem
    {
        public Montagem(bool falharEnvio = false, string? descartavel = null)
        {
            Email = new EmailFalso(falharEnvio, Unidade);
            Dominios = new DominiosFalsos(descartavel);
            Casos = new ConvitesEmpresaNebrys(
                Convites, Trilha, Unidade, new RelogioFixo(Agora), Email, new EnderecosPublicos(new Uri("https://acordu.app.br/")),
                new OpcoesCadastro(ModoCadastro.Convite, 7), Dominios, new OpcoesFornecedor());
        }

        public ConvitesFalsos Convites { get; } = new();

        public DominiosFalsos Dominios { get; }

        public TrilhaFalsa Trilha { get; } = new();

        public UnidadeContada Unidade { get; } = new();

        public EmailFalso Email { get; }

        public ConvitesEmpresaNebrys Casos { get; }
    }

    private sealed class ConvitesFalsos : IConvitesEmpresa
    {
        public List<ConviteEmpresa> Adicionados { get; } = [];

        public List<ConviteEmpresa> Existentes { get; } = [];

        public HashSet<string> EmAberto { get; } = [];

        public int ConsultasEmAberto { get; private set; }

        public void Adicionar(ConviteEmpresa convite) => Adicionados.Add(convite);

        public Task<ConviteEmpresa?> ObterAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Existentes.FirstOrDefault(c => c.Id == id));

        public Task<bool> HaEmAbertoAsync(string emailNormalizado, CancellationToken cancellationToken = default)
        {
            ConsultasEmAberto++;
            return Task.FromResult(EmAberto.Contains(emailNormalizado));
        }
    }

    private sealed class DominiosFalsos(string? descartavel) : IDominiosDescartaveis
    {
        public List<string> Conferidos { get; } = [];

        public bool EhDescartavel(string email)
        {
            Conferidos.Add(email);
            return descartavel is not null && email.EndsWith($"@{descartavel}", StringComparison.Ordinal);
        }
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
