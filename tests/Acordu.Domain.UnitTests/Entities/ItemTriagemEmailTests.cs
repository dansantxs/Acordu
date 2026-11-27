using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.Entities;

/// <summary>
/// Spec do F15, F15h (F15-70, F15-24; design D10; ADR 0016, item 4): o item da triagem nasce na chegada do e-mail, por
/// tenant e sem cliente, com o remetente, o assunto, o texto, o motivo, os clientes possíveis e os anexos dentro da política.
/// </summary>
public sealed class ItemTriagemEmailTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 6, 10, 0, 0, TimeSpan.FromHours(-3));
    private static readonly PoliticaAnexos Politica = new(20L * 1024 * 1024, ["image/png", "application/pdf"], 5);

    private static NovoAnexo Png(string nome = "foto.png") => new(nome, "image/png", 1024, $"t/triagem/i/{Guid.NewGuid()}");

    private static ItemTriagemEmail Receber(
        MotivoTriagem motivo = MotivoTriagem.Desconhecido,
        IReadOnlyList<Guid>? clientes = null,
        IReadOnlyList<NovoAnexo>? anexos = null,
        string? remetente = "Pedro@Outro.com.br",
        AnexosRecusados? recusados = null) =>
        ItemTriagemEmail.Receber(
            Guid.NewGuid(), Guid.NewGuid(), remetente, "Pedro", "Orçamento", "Quanto custa?", motivo, clientes ?? [],
            anexos ?? [], recusados, Politica, Agora);

    [Fact] // F15-70
    public void Receber_Valido_GuardaOQueChegouEmUtcSemEvento()
    {
        var recusados = new AnexosRecusados([new AnexoRecusado("a.exe", MotivoRecusaAnexo.TipoNaoPermitido)]);

        var item = Receber(anexos: [Png()], recusados: recusados);

        item.RemetenteEmail.Should().Be("pedro@outro.com.br");
        item.RemetenteNome.Should().Be("Pedro");
        item.Assunto.Should().Be("Orçamento");
        item.Texto.Should().Be("Quanto custa?");
        item.Motivo.Should().Be(MotivoTriagem.Desconhecido);
        item.RecebidoEm.Should().Be(Agora);
        item.RecebidoEm.Offset.Should().Be(TimeSpan.Zero);
        item.Anexos.Should().ContainSingle().Which.NomeArquivo.Should().Be("foto.png");
        item.Recusados.Should().Be(recusados);
        item.ClientesPossiveis.Should().BeEmpty();
        item.Eventos.Should().BeEmpty("nada é avisado nem respondido na chegada à triagem");
    }

    [Fact]
    public void Receber_SemRemetenteNemConteudo_Nulos()
    {
        var item = ItemTriagemEmail.Receber(
            Guid.NewGuid(), Guid.NewGuid(), null, "  ", "", null, MotivoTriagem.Desconhecido, [], [], null, Politica, Agora);

        item.RemetenteEmail.Should().BeNull();
        item.RemetenteNome.Should().BeNull();
        item.Assunto.Should().BeNull();
        item.Texto.Should().BeNull();
    }

    [Fact] // F15-24
    public void Receber_MaisDeUmCliente_GuardaOsClientesPossiveis()
    {
        var clientes = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var item = Receber(MotivoTriagem.MaisDeUmCliente, clientes);

        item.ClientesPossiveis.Should().Equal(clientes);
    }

    [Fact]
    public void Receber_MaisDeUmClienteComMenosDeDois_ArgumentException()
    {
        var receber = () => Receber(MotivoTriagem.MaisDeUmCliente, [Guid.NewGuid()]);

        receber.Should().Throw<ArgumentException>().WithParameterName("clientesPossiveis");
    }

    [Fact]
    public void Receber_OutroMotivoComClientes_ArgumentException()
    {
        var receber = () => Receber(MotivoTriagem.ContatoInativo, [Guid.NewGuid(), Guid.NewGuid()]);

        receber.Should().Throw<ArgumentException>().WithParameterName("clientesPossiveis");
    }

    [Fact]
    public void Receber_ClienteRepetidoOuVazio_ArgumentException()
    {
        var cliente = Guid.NewGuid();

        var repetido = () => Receber(MotivoTriagem.MaisDeUmCliente, [cliente, cliente]);
        var vazio = () => Receber(MotivoTriagem.MaisDeUmCliente, [cliente, Guid.Empty]);

        repetido.Should().Throw<ArgumentException>().WithParameterName("clientesPossiveis");
        vazio.Should().Throw<ArgumentException>().WithParameterName("clientesPossiveis");
    }

    [Fact]
    public void Receber_MotivoDesconhecido_ArgumentOutOfRange()
    {
        var receber = () => Receber((MotivoTriagem)99);

        receber.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("motivo");
    }

    [Fact]
    public void Receber_RemetenteInvalido_ArgumentException()
    {
        var receber = () => Receber(remetente: "sem-arroba");

        receber.Should().Throw<ArgumentException>().WithParameterName("remetenteEmail");
    }

    [Fact] // RNF-SEG-07: no máximo 5 anexos, cada um pela política
    public void Receber_AlemDoMaximoOuForaDaPolitica_AnexoInvalido()
    {
        var demais = () => Receber(anexos: [.. Enumerable.Range(1, 6).Select(n => Png($"{n}.png"))]);
        var foraDoTipo = () => Receber(anexos: [new NovoAnexo("a.exe", "application/x-msdownload", 10, "t/triagem/i/a")]);

        demais.Should().Throw<AnexoInvalidoException>();
        foraDoTipo.Should().Throw<AnexoInvalidoException>();
    }

    [Fact]
    public void Receber_TextoAlemDoLimite_ArgumentException()
    {
        var receber = () => ItemTriagemEmail.Receber(
            Guid.NewGuid(), Guid.NewGuid(), null, null, null, new string('a', 10_001), MotivoTriagem.Desconhecido, [], [], null,
            Politica, Agora);

        receber.Should().Throw<ArgumentException>().WithParameterName("texto");
    }

    [Fact] // F15-70: nos limites exatos (nome 200, assunto 998, texto 10.000) o item guarda tudo
    public void Receber_NosLimites_GuardaSemCortar()
    {
        var item = ItemTriagemEmail.Receber(
            Guid.NewGuid(), Guid.NewGuid(), null, new string('n', 200), new string('a', 998), new string('t', 10_000),
            MotivoTriagem.Desconhecido, [], [], null, Politica, Agora);

        item.RemetenteNome.Should().HaveLength(200);
        item.Assunto.Should().HaveLength(998);
        item.Texto.Should().HaveLength(10_000);
    }

    [Fact]
    public void Receber_NomeOuAssuntoAlemDoLimite_ArgumentException()
    {
        var nome = () => ItemTriagemEmail.Receber(
            Guid.NewGuid(), Guid.NewGuid(), null, new string('n', 201), null, null, MotivoTriagem.Desconhecido, [], [], null, Politica, Agora);
        var assunto = () => ItemTriagemEmail.Receber(
            Guid.NewGuid(), Guid.NewGuid(), null, null, new string('a', 999), null, MotivoTriagem.Desconhecido, [], [], null, Politica, Agora);

        nome.Should().Throw<ArgumentException>().WithParameterName("remetenteNome");
        assunto.Should().Throw<ArgumentException>().WithParameterName("assunto");
    }

    [Theory] // os motivos que não trazem clientes possíveis aceitam a lista vazia, e só o MAIS_DE_UM_CLIENTE exige dois ou mais
    [InlineData(MotivoTriagem.Desconhecido)]
    [InlineData(MotivoTriagem.ContatoInativo)]
    [InlineData(MotivoTriagem.RemetenteNaoConfirmado)]
    [InlineData(MotivoTriagem.NaoContatoDaSolicitacao)]
    public void Receber_OutrosMotivosSemClientes_Guarda(MotivoTriagem motivo)
    {
        Receber(motivo).ClientesPossiveis.Should().BeEmpty();
    }

    [Fact] // exatamente 5 anexos cabem (RNF-SEG-07)
    public void Receber_ExatamenteOMaximoDeAnexos_Guarda()
    {
        var item = Receber(anexos: [.. Enumerable.Range(1, 5).Select(n => Png($"{n}.png"))]);

        item.Anexos.Should().HaveCount(5);
    }
}
