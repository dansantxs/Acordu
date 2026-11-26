using Acordu.Application.Abstractions;
using Acordu.Application.EntradaEmail;
using Acordu.Application.UnitTests.Exemplos;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.UnitTests.EntradaEmail;

/// <summary>
/// Design do F15, D6 e §3 (ADR 0016, item 8): o e-mail aceito para a empresa deixa um registro de entrada, sem conteúdo,
/// e a repetição da mesma entrega não grava de novo. Até o F15f, o resultado é sempre IGNORADO.
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
            new RelogioFixo(Falsos.Agora));

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
