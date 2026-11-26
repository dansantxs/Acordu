using Acordu.Application.Abstractions;
using Acordu.Application.EntradaEmail;
using Acordu.Application.UnitTests.Exemplos;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.UnitTests.EntradaEmail;

/// <summary>
/// Design do F15, D6 e §3 (ADR 0016, item 8): o e-mail aceito para a empresa deixa um registro de entrada, sem conteúdo,
/// e a repetição da mesma entrega não grava de novo. Até o F15f, o resultado é sempre IGNORADO; o automático, o laço e o
/// filtro (F15e) dão o desfecho do descarte.
/// </summary>
public sealed class ComandosEntradaEmailTests
{
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly DestinoDeEntrada Destino = new("k7m2qd9w@entrada.acordu.app.br", "k7m2qd9w", null);
    private readonly Repositorio<EmailRecebido> _repositorio = new();
    private readonly UnidadeFalsa _unidade = new();

    private ComandosEntradaEmail Comandos(ITenantContext? contexto = null) =>
        new(
            contexto ?? new ContextoDoDono(Empresa, Guid.NewGuid()),
            new RegistrosEmMemoria(_repositorio),
            _repositorio,
            _unidade,
            new RelogioFixo(Falsos.Agora),
            OpcoesEmailEntrada.DaConfiguracao("entrada.acordu.app.br", remetenteDosAvisos: "nao-responda@send.acordu.app.br"));

    private static MensagemDeEntrada Mensagem(string? messageId = "abc@mail.exemplo.com") =>
        new(messageId, ["ana@cliente.com.br"], new string('a', 64));

    [Fact]
    public async Task ReceberAsync_Novo_RegistraIgnoradoSemConteudo()
    {
        var desfecho = await Comandos().ReceberAsync(Mensagem(), Destino);

        desfecho.Should().Be(DesfechoEntrada.Registrada);
        _unidade.Gravacoes.Should().Be(1);
        var registro = _repositorio.Itens.Should().ContainSingle().Subject;
        registro.TenantId.Should().Be(Empresa);
        registro.Resultado.Should().Be(ResultadoEntradaEmail.Ignorado);
        registro.RecebidoEm.Should().Be(Falsos.Agora);
        registro.ChaveEntrada.Should().Be(ChaveDeEntrada.Calcular("abc@mail.exemplo.com", Destino.Endereco, new string('a', 64)));
        registro.RemetenteHash.Should().Be(ChaveDeEntrada.DoRemetente(["ana@cliente.com.br"]));
    }

    [Fact]
    public async Task ReceberAsync_MesmaEntregaDeNovo_RepetidaSemGravar()
    {
        await Comandos().ReceberAsync(Mensagem(), Destino);

        var desfecho = await Comandos().ReceberAsync(Mensagem(), Destino);

        desfecho.Should().Be(DesfechoEntrada.Repetida);
        _repositorio.Itens.Should().ContainSingle();
        _unidade.Gravacoes.Should().Be(1);
    }

    [Fact]
    public async Task ReceberAsync_OutroMessageId_OutroRegistro()
    {
        await Comandos().ReceberAsync(Mensagem(), Destino);

        await Comandos().ReceberAsync(Mensagem("outro@mail.exemplo.com"), Destino);

        _repositorio.Itens.Should().HaveCount(2);
    }

    [Theory] // F15-85, F15-87, F15-88: descarte registrado, como IGNORADO, sem criar nada
    [InlineData("automatico", DesfechoEntrada.Automatica)]
    [InlineData("laco", DesfechoEntrada.Laco)]
    [InlineData("filtro", DesfechoEntrada.Filtrada)]
    public async Task ReceberAsync_AutomaticoLacoOuFiltro_RegistraIgnoradoComODescarte(string caso, DesfechoEntrada esperado)
    {
        var mensagem = caso switch
        {
            "automatico" => Mensagem() with { Sinais = new SinaisDeAutomatico(AutoSubmitted: "auto-replied") },
            "laco" => Mensagem() with { Remetentes = ["nao-responda@send.acordu.app.br"] },
            _ => Mensagem() with { Filtro = "spam" },
        };

        var desfecho = await Comandos().ReceberAsync(mensagem, Destino);

        desfecho.Should().Be(esperado);
        var registro = _repositorio.Itens.Should().ContainSingle().Subject;
        registro.Resultado.Should().Be(ResultadoEntradaEmail.Ignorado);
        _unidade.Gravacoes.Should().Be(1);
    }

    [Fact]
    public async Task ReceberAsync_AutomaticoRepetido_RepetidaSemGravar() // a repetição vem antes do descarte (D6)
    {
        var automatico = Mensagem() with { Sinais = new SinaisDeAutomatico(TipoDoCorpo: "multipart/report") };
        await Comandos().ReceberAsync(automatico, Destino);

        var desfecho = await Comandos().ReceberAsync(automatico, Destino);

        desfecho.Should().Be(DesfechoEntrada.Repetida);
        _repositorio.Itens.Should().ContainSingle();
    }

    [Fact]
    public async Task ReceberAsync_SemTenantNoContexto_Recusa()
    {
        var receber = () => Comandos(new SemTenant()).ReceberAsync(Mensagem(), Destino);

        await receber.Should().ThrowAsync<InvalidOperationException>();
        _repositorio.Itens.Should().BeEmpty();
    }

    /// <summary>Contexto sem empresa (escopo que ninguém definiu).</summary>
    private sealed class SemTenant : ITenantContext
    {
        public Guid? TenantId => null;

        public bool EhCliente => false;

        public Guid? ClienteId => null;

        public Guid? UsuarioId => null;

        public string? Papel => null;

        public IReadOnlySet<string> Permissoes { get; } = new HashSet<string>();

        public EscopoPortal? EscopoPortal => null;

        public Guid? AcessoPortalId => null;

        public Guid? SolicitacaoPermitida => null;
    }

    /// <summary>A consulta "já existe" sobre os itens do repositório em memória.</summary>
    private sealed class RegistrosEmMemoria(Repositorio<EmailRecebido> repositorio) : IEmailsRecebidos
    {
        public Task<bool> ExisteAsync(string chaveEntrada, CancellationToken cancellationToken = default) =>
            Task.FromResult(repositorio.Itens.Exists(e => e.ChaveEntrada == chaveEntrada));
    }
}
