using Acordu.Domain.Common;
using Acordu.Domain.Entities;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class CategoriaTests
{
    [Fact]
    public void Criar_NasceAtivaComNomeAparado()
    {
        var categoria = Categoria.Criar(Guid.NewGuid(), Calendarios.Tenant, "  Design ");

        categoria.Nome.Should().Be("Design");
        categoria.Ativa.Should().BeTrue();
    }

    [Fact]
    public void Criar_NomeAcimaDoLimite_LancaExcecao()
    {
        var acao = () => Categoria.Criar(Guid.NewGuid(), Calendarios.Tenant, new string('x', LimitesTexto.NomeCategoria + 1));

        acao.Should().Throw<ArgumentException>().WithParameterName("nome");
    }

    [Fact]
    public void Renomear_SemNome_LancaExcecao()
    {
        var categoria = Categoria.Criar(Guid.NewGuid(), Calendarios.Tenant, "Design");

        var acao = () => categoria.Renomear("");

        acao.Should().Throw<ArgumentException>();
        categoria.Nome.Should().Be("Design");
    }

    [Fact]
    public void InativarEReativar()
    {
        var categoria = Categoria.Criar(Guid.NewGuid(), Calendarios.Tenant, "Design");

        categoria.Inativar();
        categoria.Ativa.Should().BeFalse();

        categoria.Reativar();
        categoria.Ativa.Should().BeTrue();
    }
}
