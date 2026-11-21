using System.Text.Json;
using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Nebrys;
using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.UnitTests.Nebrys;

/// <summary>
/// Spec do F14, F14-71 e F14-108 (F14l): a liberação acha o registro pelo documento normalizado, libera, grava na trilha
/// só as datas (nunca o documento) e salva; o que não vale não grava nada.
/// </summary>
public sealed class LiberacaoDocumentosTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 20, 15, 0, 0, TimeSpan.Zero);
    private const string Cpf = "52998224725";

    [Fact]
    public async Task Liberar_DocumentoComPontuacao_AchaPeloNormalizadoLiberaETrilhaSemODocumento() // F14-71, F14-108
    {
        var registro = DocumentoTeste.Registrar(new string('a', DocumentoTeste.TamanhoHash), Agora.AddDays(-60));
        var (liberacao, documentos, trilha, unidade) = Montar(registro);

        await liberacao.LiberarAsync(new LiberacaoDocumentoRequest("529.982.247-25"));

        documentos.Pedido.Should().Be(Cpf);
        registro.LiberacaoDisponivel.Should().BeTrue();
        registro.LiberadoEm.Should().Be(Agora);
        unidade.Salvamentos.Should().Be(1);
        trilha.Registros.Should().ContainSingle();
        var (acao, alvoTipo, alvoId, dados) = trilha.Registros[0];
        acao.Should().Be(AcoesAuditoria.NebrysDocumentoLiberacao);
        alvoTipo.Should().Be("documento");
        alvoId.Should().BeNull();
        var json = JsonSerializer.Serialize(dados, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().NotContain(Cpf).And.NotContain("529.982").And.NotContain(registro.Hash);
        var elemento = JsonDocument.Parse(json).RootElement;
        elemento.GetProperty("antes").GetProperty("liberadoEm").ValueKind.Should().Be(JsonValueKind.Null);
        elemento.GetProperty("depois").GetProperty("liberadoEm").GetDateTimeOffset().Should().Be(Agora);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("529.982.247-26")]
    public async Task Liberar_DocumentoVazioOuInvalido_400NoCampoSemLerNemGravar(string? documento)
    {
        var (liberacao, documentos, trilha, unidade) = Montar(null);

        var acao = () => liberacao.LiberarAsync(new LiberacaoDocumentoRequest(documento));

        (await acao.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be(LiberacaoDocumentos.CampoDocumento);
        documentos.Pedido.Should().BeNull();
        trilha.Registros.Should().BeEmpty();
        unidade.Salvamentos.Should().Be(0);
    }

    [Fact]
    public async Task Liberar_DocumentoSemTeste_TransicaoInvalidaSemGravar() // F14-71
    {
        var (liberacao, _, trilha, unidade) = Montar(null);

        var acao = () => liberacao.LiberarAsync(new LiberacaoDocumentoRequest(Cpf));

        await acao.Should().ThrowAsync<TransicaoInvalidaException>().WithMessage("*não teve teste*");
        trilha.Registros.Should().BeEmpty();
        unidade.Salvamentos.Should().Be(0);
    }

    [Fact]
    public async Task Liberar_ComLiberacaoNaoUsada_TransicaoInvalidaSemGravar() // F14-71
    {
        var registro = DocumentoTeste.Registrar(new string('b', DocumentoTeste.TamanhoHash), Agora.AddDays(-60));
        registro.Liberar(Agora.AddDays(-1));
        var (liberacao, _, trilha, unidade) = Montar(registro);

        var acao = () => liberacao.LiberarAsync(new LiberacaoDocumentoRequest(Cpf));

        await acao.Should().ThrowAsync<TransicaoInvalidaException>();
        registro.LiberadoEm.Should().Be(Agora.AddDays(-1));
        trilha.Registros.Should().BeEmpty();
        unidade.Salvamentos.Should().Be(0);
    }

    private static (LiberacaoDocumentos, DocumentosFalsos, TrilhaFalsa, UnidadeContada) Montar(DocumentoTeste? registro)
    {
        var documentos = new DocumentosFalsos(registro);
        var trilha = new TrilhaFalsa();
        var unidade = new UnidadeContada();
        return (new LiberacaoDocumentos(documentos, trilha, unidade, new RelogioFixo(Agora)), documentos, trilha, unidade);
    }

    private sealed class DocumentosFalsos(DocumentoTeste? registro) : IDocumentosTeste
    {
        public string? Pedido { get; private set; }

        public Task<DocumentoTeste?> ObterAsync(string documentoNormalizado, CancellationToken cancellationToken = default)
        {
            Pedido = documentoNormalizado;
            return Task.FromResult(registro);
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

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
