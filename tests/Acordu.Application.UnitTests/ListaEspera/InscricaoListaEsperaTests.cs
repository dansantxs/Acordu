using Acordu.Application.ListaEspera;
using Acordu.Domain.Entities;

namespace Acordu.Application.UnitTests.ListaEspera;

/// <summary>Spec do F14, F14-52, F14-53 e F14-55 (F14o; design D12): a inscrição pela página pública.</summary>
public sealed class InscricaoListaEsperaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 15, 0, 0, TimeSpan.Zero);

    [Fact] // F14-52
    public async Task Inscrever_CamposValidos_EntregaAEntradaComADataParaGravar()
    {
        var gravacao = new GravacaoFalsa();
        var inscricao = new InscricaoListaEspera(gravacao, new RelogioFixo(Agora));

        await inscricao.InscreverAsync(new ListaEsperaRequest("Ana", " Ana@Exemplo.com ", "Estúdio Traço", "Organizar o atendimento."));

        var entrada = gravacao.Entradas.Should().ContainSingle().Subject;
        entrada.Email.Should().Be("ana@exemplo.com");
        entrada.NomeEmpresa.Should().Be("Estúdio Traço");
        entrada.Mensagem.Should().Be("Organizar o atendimento.");
        entrada.CriadaEm.Should().Be(Agora);
        entrada.Id.Should().NotBeEmpty();
    }

    [Theory] // F14-55
    [InlineData(null, "ana@exemplo.com", "Estúdio", "nome")]
    [InlineData("Ana", "invalido", "Estúdio", "email")]
    [InlineData("Ana", "ana@exemplo.com", "", "nomeEmpresa")]
    public async Task Inscrever_CampoFaltandoOuEmailInvalido_400NoCampoSemGravar(string? nome, string? email, string? empresa, string campo)
    {
        var gravacao = new GravacaoFalsa();
        var inscricao = new InscricaoListaEspera(gravacao, new RelogioFixo(Agora));

        var acao = () => inscricao.InscreverAsync(new ListaEsperaRequest(nome, email, empresa));

        (await acao.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be(campo);
        gravacao.Entradas.Should().BeEmpty();
    }

    [Fact] // F14-53: o caso de uso não distingue a entrada nova da repetida (quem decide é o índice único, na gravação)
    public async Task Inscrever_EmailJaNaLista_TerminaIgualSemErro()
    {
        var gravacao = new GravacaoFalsa();
        var inscricao = new InscricaoListaEspera(gravacao, new RelogioFixo(Agora));
        await inscricao.InscreverAsync(new ListaEsperaRequest("Ana", "ana@exemplo.com", "Estúdio"));

        var acao = () => inscricao.InscreverAsync(new ListaEsperaRequest("Ana de novo", "ANA@exemplo.com", "Outro"));

        await acao.Should().NotThrowAsync();
        gravacao.Entradas.Should().ContainSingle().Which.Nome.Should().Be("Ana");
    }

    /// <summary>Como o índice único: o e-mail que já está não grava de novo nem muda a entrada que existe.</summary>
    private sealed class GravacaoFalsa : IInscricoesListaEspera
    {
        public List<EntradaListaEspera> Entradas { get; } = [];

        public Task GravarSeNovaAsync(EntradaListaEspera entrada, CancellationToken cancellationToken = default)
        {
            if (Entradas.TrueForAll(e => e.Email != entrada.Email))
                Entradas.Add(entrada);
            return Task.CompletedTask;
        }
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
