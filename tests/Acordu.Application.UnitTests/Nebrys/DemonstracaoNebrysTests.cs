using Acordu.Application.Common;
using Acordu.Application.Nebrys;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.UnitTests.Nebrys;

/// <summary>
/// Spec do F14, F14s (F14-87, F14-88, F14-90, F14-110; design D13): o caso de uso da demonstração na área confere o e-mail
/// da indicação e traduz o resultado da porta: sem conta é recusa no campo (nada criado), já indicada é transição inválida.
/// </summary>
public sealed class DemonstracaoNebrysTests
{
    [Fact]
    public async Task Indicar_EmailComConta_NormalizaEDevolveADemonstracao() // F14-90
    {
        var porta = new DemonstracaoFalsa();

        var dto = await new DemonstracaoNebrys(porta).IndicarAsync(new IndicacaoDemonstracaoRequest("  Ana@Exemplo.com "));

        porta.Indicados.Should().Equal("ana@exemplo.com");
        dto.Should().BeSameAs(porta.Atual);
    }

    [Fact]
    public async Task Indicar_EmailSemConta_400NoCampoComAExplicacao() // F14-110
    {
        var porta = new DemonstracaoFalsa { Resultado = ResultadoIndicacao.SemConta };

        var acao = () => new DemonstracaoNebrys(porta).IndicarAsync(new IndicacaoDemonstracaoRequest("ana@exemplo.com"));

        (await acao.Should().ThrowAsync<ArgumentException>())
            .Which.ParamName.Should().Be(DemonstracaoNebrys.CampoEmail);
        (await acao.Should().ThrowAsync<ArgumentException>()).WithMessage("*não tem conta*");
    }

    [Fact]
    public async Task Indicar_JaIndicada_TransicaoInvalida()
    {
        var porta = new DemonstracaoFalsa { Resultado = ResultadoIndicacao.JaIndicada };

        var acao = () => new DemonstracaoNebrys(porta).IndicarAsync(new IndicacaoDemonstracaoRequest("ana@exemplo.com"));

        await acao.Should().ThrowAsync<TransicaoInvalidaException>().WithMessage(DemonstracaoNebrys.MensagemJaIndicada);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sem-arroba")]
    public async Task Indicar_EmailVazioOuInvalido_400NoCampoSemChamarAPorta(string? email) // F14-110
    {
        var porta = new DemonstracaoFalsa();

        var acao = () => new DemonstracaoNebrys(porta).IndicarAsync(new IndicacaoDemonstracaoRequest(email));

        (await acao.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be(DemonstracaoNebrys.CampoEmail);
        porta.Indicados.Should().BeEmpty();
    }

    [Fact]
    public async Task Criar_DevolveADemonstracaoLidaDepois() // F14-87
    {
        var porta = new DemonstracaoFalsa();

        var dto = await new DemonstracaoNebrys(porta).CriarAsync();

        porta.Criadas.Should().Be(1);
        dto.Should().BeSameAs(porta.Atual);
    }

    [Fact]
    public async Task Restaurar_DevolveADemonstracaoLidaDepois() // F14-88
    {
        var porta = new DemonstracaoFalsa();

        var dto = await new DemonstracaoNebrys(porta).RestaurarAsync();

        porta.Restauradas.Should().Be(1);
        dto.Should().BeSameAs(porta.Atual);
    }

    private sealed class DemonstracaoFalsa : IEmpresaDeDemonstracao
    {
        public DemonstracaoNebrysDto Atual { get; } = new(Guid.NewGuid(), "Estúdio Aurora", DateTimeOffset.UnixEpoch, []);

        public ResultadoIndicacao Resultado { get; init; } = ResultadoIndicacao.Indicada;

        public List<string> Indicados { get; } = [];

        public int Criadas { get; private set; }

        public int Restauradas { get; private set; }

        public Task<DemonstracaoNebrysDto> ObterAsync(CancellationToken cancellationToken = default) => Task.FromResult(Atual);

        public Task<Guid> CriarAsync(CancellationToken cancellationToken = default)
        {
            Criadas++;
            return Task.FromResult(Atual.EmpresaId!.Value);
        }

        public Task RestaurarAsync(CancellationToken cancellationToken = default)
        {
            Restauradas++;
            return Task.CompletedTask;
        }

        public Task<ResultadoIndicacao> IndicarAsync(string email, CancellationToken cancellationToken = default)
        {
            Indicados.Add(email);
            return Task.FromResult(Resultado);
        }

        public Task RetirarAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            throw new NaoEncontradoException("não indicada");
    }
}
