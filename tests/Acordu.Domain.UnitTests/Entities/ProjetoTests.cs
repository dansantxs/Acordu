using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class ProjetoTests
{
    private static Projeto Criar(Duracao? estimativa = null) =>
        Projeto.Criar(
            Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, "Site institucional", Guid.NewGuid(),
            new DateOnly(2026, 12, 15), estimativa ?? Duracao.FromHoras(40));

    [Fact]
    public void Criar_NasceEmAndamento()
    {
        var projeto = Criar();

        projeto.Status.Should().Be(StatusProjeto.EmAndamento);
        projeto.Nome.Should().Be("Site institucional");
        projeto.Estimativa.Should().Be(Duracao.FromHoras(40));
        projeto.ClienteId.Should().Be(Contratos.Cliente);
    }

    [Fact]
    public void Criar_SemNome_LancaExcecao()
    {
        var acao = () => Projeto.Criar(Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, "", null, null, null);

        acao.Should().Throw<ArgumentException>().WithParameterName("nome");
    }

    [Fact]
    public void Criar_EstimativaZero_LancaExcecao()
    {
        var acao = () => Criar(Duracao.Zero);

        acao.Should().Throw<ArgumentException>().WithParameterName("estimativa");
    }

    [Fact]
    public void Editar_AlteraNomePrazoEstimativa()
    {
        var projeto = Criar();

        projeto.Editar("Site novo", null, null);

        projeto.Nome.Should().Be("Site novo");
        projeto.Prazo.Should().BeNull();
        projeto.Estimativa.Should().BeNull();
    }

    [Fact]
    public void AlterarContrato_RemoveOuTroca()
    {
        var projeto = Criar();

        projeto.AlterarContrato(null);

        projeto.ContratoId.Should().BeNull();
    }

    [Fact]
    public void Concluir_EmAndamento_FicaConcluido()
    {
        var projeto = Criar();

        projeto.Concluir();

        projeto.Status.Should().Be(StatusProjeto.Concluido);
    }

    [Fact]
    public void Cancelar_EmAndamento_FicaCancelado()
    {
        var projeto = Criar();

        projeto.Cancelar();

        projeto.Status.Should().Be(StatusProjeto.Cancelado);
    }

    [Theory]
    [InlineData(StatusProjeto.Concluido)]
    [InlineData(StatusProjeto.Cancelado)]
    public void EstadoFinal_TudoLancaExcecao(StatusProjeto status)
    {
        var projeto = Criar();
        if (status == StatusProjeto.Concluido)
            projeto.Concluir();
        else
            projeto.Cancelar();

        ((Action)projeto.Concluir).Should().Throw<TransicaoInvalidaException>();
        ((Action)projeto.Cancelar).Should().Throw<TransicaoInvalidaException>();
        ((Action)(() => projeto.Editar("X", null, null))).Should().Throw<TransicaoInvalidaException>();
        ((Action)(() => projeto.AlterarContrato(null))).Should().Throw<TransicaoInvalidaException>();
        projeto.Status.Should().Be(status);
    }
}
