using System.Text;
using Acordu.Application.Abstractions;
using Acordu.Application.Anexos;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.UnitTests.Anexos;

public sealed class RecebimentoAnexosTests
{
    private readonly Armazenamento _armazenamento = new();
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _solicitacao = Guid.NewGuid();

    private static ArquivoRecebido Arquivo(string nome, byte[] conteudo, long? tamanho = null) =>
        new(nome, tamanho ?? conteudo.Length, () => new MemoryStream(conteudo));

    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7 conteudo");

    [Fact]
    public async Task Enviar_GravaComChaveDoAnexoETipoDetectado()
    {
        var recebimento = new RecebimentoAnexos(_armazenamento);

        var anexos = await recebimento.EnviarAsync(_tenant, _solicitacao, [Arquivo("nota.txt", Pdf)]);

        anexos.Should().HaveCount(1);
        anexos[0].TipoConteudo.Should().Be(TiposDeArquivo.Pdf);
        anexos[0].ChaveArmazenamento.Should().Be($"{_tenant}/{_solicitacao}/{anexos[0].Id}");
        _armazenamento.Objetos.ContainsKey(anexos[0].ChaveArmazenamento).Should().BeTrue();
    }

    [Fact]
    public async Task TipoNaoPermitido_RecusaAntesDeEnviarQualquerUm()
    {
        var recebimento = new RecebimentoAnexos(_armazenamento);

        var acao = () => recebimento.EnviarAsync(_tenant, _solicitacao, [Arquivo("ok.pdf", Pdf), Arquivo("x.pdf", [0x4D, 0x5A, 0x90])]);

        await acao.Should().ThrowAsync<AnexoInvalidoException>();
        _armazenamento.Objetos.Should().BeEmpty();
    }

    [Fact]
    public async Task AcimaDoLimite_413SemLerOConteudo()
    {
        var recebimento = new RecebimentoAnexos(_armazenamento);

        var acao = () => recebimento.EnviarAsync(_tenant, _solicitacao, [Arquivo("grande.pdf", Pdf, 21L * 1024 * 1024)]);

        await acao.Should().ThrowAsync<ArquivoGrandeDemaisException>();
    }

    [Fact]
    public async Task FalhaNoMeio_DescartaOsJaEnviados()
    {
        _armazenamento.FalharNaChamada = 2;
        var recebimento = new RecebimentoAnexos(_armazenamento);

        var acao = () => recebimento.EnviarAsync(_tenant, _solicitacao, [Arquivo("a.pdf", Pdf), Arquivo("b.pdf", Pdf)]);

        await acao.Should().ThrowAsync<IOException>();
        _armazenamento.Objetos.Should().BeEmpty();
    }

    [Fact]
    public async Task MaisQueOMaximo_Recusa()
    {
        var recebimento = new RecebimentoAnexos(_armazenamento);
        var arquivos = Enumerable.Range(0, 6).Select(i => Arquivo($"{i}.pdf", Pdf)).ToList();

        var acao = () => recebimento.EnviarAsync(_tenant, _solicitacao, arquivos);

        await acao.Should().ThrowAsync<AnexoInvalidoException>();
    }

    private sealed class Armazenamento : IArmazenamentoArquivos
    {
        private int _chamadas;

        public Dictionary<string, byte[]> Objetos { get; } = [];

        public int? FalharNaChamada { get; set; }

        public async Task EnviarAsync(
            string chave, Stream conteudo, long tamanhoBytes, string tipoConteudo, CancellationToken cancellationToken = default)
        {
            if (++_chamadas == FalharNaChamada)
                throw new IOException("Falha simulada no storage.");
            using var copia = new MemoryStream();
            await conteudo.CopyToAsync(copia, cancellationToken);
            Objetos[chave] = copia.ToArray();
        }

        public Task RemoverAsync(string chave, CancellationToken cancellationToken = default)
        {
            Objetos.Remove(chave);
            return Task.CompletedTask;
        }

        public Uri UrlDeDownload(string chave, string nomeArquivo, string tipoConteudo, TimeSpan validade) =>
            new($"https://armazenamento.teste/{chave}");
    }
}
