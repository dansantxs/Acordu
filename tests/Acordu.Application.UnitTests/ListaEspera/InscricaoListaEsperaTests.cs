using Acordu.Application.Cadastro;
using Acordu.Application.ListaEspera;
using Acordu.Domain.Entities;

namespace Acordu.Application.UnitTests.ListaEspera;

/// <summary>Spec do F14, F14-52, F14-53 e F14-55 (F14o; design D12) e F14-60 (F14p; D9): a inscrição pela página pública.</summary>
public sealed class InscricaoListaEsperaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 15, 0, 0, TimeSpan.Zero);
    private static readonly DominiosFalsos SemDescartaveis = new();

    [Fact] // F14-52
    public async Task Inscrever_CamposValidos_EntregaAEntradaComADataParaGravar()
    {
        var gravacao = new GravacaoFalsa();
        var inscricao = new InscricaoListaEspera(gravacao, new RelogioFixo(Agora), SemDescartaveis);

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
        var inscricao = new InscricaoListaEspera(gravacao, new RelogioFixo(Agora), SemDescartaveis);

        var acao = () => inscricao.InscreverAsync(new ListaEsperaRequest(nome, email, empresa));

        (await acao.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be(campo);
        gravacao.Entradas.Should().BeEmpty();
    }

    [Fact] // F14-53: o caso de uso não distingue a entrada nova da repetida (quem decide é o índice único, na gravação)
    public async Task Inscrever_EmailJaNaLista_TerminaIgualSemErro()
    {
        var gravacao = new GravacaoFalsa();
        var inscricao = new InscricaoListaEspera(gravacao, new RelogioFixo(Agora), SemDescartaveis);
        await inscricao.InscreverAsync(new ListaEsperaRequest("Ana", "ana@exemplo.com", "Estúdio"));

        var acao = () => inscricao.InscreverAsync(new ListaEsperaRequest("Ana de novo", "ANA@exemplo.com", "Outro"));

        await acao.Should().NotThrowAsync();
        gravacao.Entradas.Should().ContainSingle().Which.Nome.Should().Be("Ana");
    }

    [Fact] // F14-60
    public async Task Inscrever_EmailDescartavel_400NoEmailSemGravar()
    {
        var gravacao = new GravacaoFalsa();
        var dominios = new DominiosFalsos("descartavel.test");
        var inscricao = new InscricaoListaEspera(gravacao, new RelogioFixo(Agora), dominios);

        var acao = () => inscricao.InscreverAsync(new ListaEsperaRequest("Ana", " Ana@Descartavel.TEST ", "Estúdio"));

        var erro = (await acao.Should().ThrowAsync<ArgumentException>()).Which;
        erro.ParamName.Should().Be("email");
        erro.Message.Should().StartWith(MensagensDeCadastro.EmailDescartavel);
        dominios.Conferidos.Should().ContainSingle().Which.Should().Be("ana@descartavel.test", "confere o e-mail já normalizado");
        gravacao.Entradas.Should().BeEmpty();
    }

    [Fact] // F14-60: os campos vêm antes (o primeiro erro, na ordem do formulário), e o e-mail comum passa
    public async Task Inscrever_CampoFaltandoComEmailDescartavel_400NoCampoAntes()
    {
        var gravacao = new GravacaoFalsa();
        var inscricao = new InscricaoListaEspera(gravacao, new RelogioFixo(Agora), new DominiosFalsos("descartavel.test"));

        var semNome = () => inscricao.InscreverAsync(new ListaEsperaRequest(null, "ana@descartavel.test", "Estúdio"));
        await inscricao.InscreverAsync(new ListaEsperaRequest("Bia", "bia@exemplo.com", "Ateliê"));

        (await semNome.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be("nome");
        gravacao.Entradas.Should().ContainSingle().Which.Email.Should().Be("bia@exemplo.com");
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

    /// <summary>A lista de domínios descartáveis com um domínio só, ou nenhum (a comparação de verdade é da Infrastructure).</summary>
    private sealed class DominiosFalsos(string? descartavel = null) : IDominiosDescartaveis
    {
        public List<string> Conferidos { get; } = [];

        public bool EhDescartavel(string email)
        {
            Conferidos.Add(email);
            return descartavel is not null && email.EndsWith($"@{descartavel}", StringComparison.Ordinal);
        }
    }
}
