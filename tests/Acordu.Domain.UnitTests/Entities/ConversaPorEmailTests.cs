using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using static Acordu.Domain.UnitTests.Calendarios;

namespace Acordu.Domain.UnitTests.Entities;

/// <summary>Spec do F15, F15c (F15-51; design D3 e D9): o token do endereço da conversa de cada solicitação.</summary>
public sealed class ConversaPorEmailTests
{
    private static readonly Guid Solicitacao = Guid.NewGuid();

    private static ConversaPorEmail Criar() =>
        ConversaPorEmail.Criar(Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, Solicitacao, Brasilia(6, 10));

    [Fact]
    public void Criar_SolicitacaoInternaOuDoPortal_TokenNoAlfabetoSemFioDeOrigem()
    {
        var conversa = Criar();

        conversa.Token.Should().HaveLength(ConversaPorEmail.TamanhoToken);
        conversa.Token.Should().MatchRegex("^[23456789abcdefghjkmnpqrstuvwxyz]{16}$");
        ConversaPorEmail.TokenValido(conversa.Token).Should().BeTrue();
        conversa.SolicitacaoId.Should().Be(Solicitacao);
        conversa.ClienteId.Should().Be(Contratos.Cliente);
        conversa.MessageIdOrigem.Should().BeNull();
        conversa.AssuntoOrigem.Should().BeNull();
        conversa.CriadaEm.Should().Be(Brasilia(6, 10));
        conversa.CriadaEm.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Criar_DuasConversas_TokensDiferentes()
    {
        Criar().Token.Should().NotBe(Criar().Token);
    }

    [Fact]
    public void Criar_SemSolicitacao_LancaExcecao()
    {
        var acao = () => ConversaPorEmail.Criar(Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, Guid.Empty, Brasilia(6, 10));

        acao.Should().Throw<ArgumentException>().WithParameterName("solicitacaoId");
    }

    [Fact]
    public void SortearToken_MilVezes_SoSimbolosDoAlfabeto()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => ConversaPorEmail.SortearToken()).ToList();

        tokens.Should().OnlyContain(t => t.Length == ConversaPorEmail.TamanhoToken && t.All(c => CodigoDeEntrada.Alfabeto.Contains(c)));
        tokens.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("k7m2qdxzk7m2qdx")]
    [InlineData("k7m2qdxzk7m2qdxzk")]
    [InlineData("K7M2QDXZK7M2QDXZ")]
    [InlineData("k7m2qdxzk7m2qd0l")]
    public void TokenValido_ForaDoFormato_Falso(string? token)
    {
        ConversaPorEmail.TokenValido(token).Should().BeFalse();
    }

    [Fact]
    public void DaAberturaPorEmail_ComMessageIdEAssunto_GuardaOFio()
    {
        var conversa = ConversaPorEmail.DaAberturaPorEmail(
            Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, Solicitacao, " <abc@mail.gmail.com> ", " Forno parou ", Brasilia(6, 10));

        conversa.MessageIdOrigem.Should().Be("<abc@mail.gmail.com>");
        conversa.AssuntoOrigem.Should().Be("Forno parou");
        ConversaPorEmail.TokenValido(conversa.Token).Should().BeTrue();
    }

    [Fact]
    public void DaAberturaPorEmail_SemAssunto_AssuntoVazio()
    {
        var conversa = ConversaPorEmail.DaAberturaPorEmail(
            Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, Solicitacao, "<abc@mail.gmail.com>", null, Brasilia(6, 10));

        conversa.AssuntoOrigem.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void DaAberturaPorEmail_SemMessageId_LancaExcecao(string? messageId)
    {
        var acao = () => ConversaPorEmail.DaAberturaPorEmail(
            Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, Solicitacao, messageId!, "Forno parou", Brasilia(6, 10));

        acao.Should().Throw<ArgumentException>().WithParameterName(nameof(messageId));
    }
}
