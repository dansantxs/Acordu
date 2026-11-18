using Acordu.Infrastructure.Persistence.Cadastros;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>
/// RF-AUT-07 (spec do F9, F9-38): toda entidade com <c>TenantId</c> do modelo está classificada para o expurgo de
/// cadastro abandonado, numa lista só. Entidade nova sem classificação faz este teste falhar antes de o expurgo apagar
/// dado real.
/// </summary>
[Collection(ColecaoBanco.Nome)]
public sealed class ClassificacaoDoExpurgoTests(BancoFixture banco)
{
    [Fact]
    public async Task EntidadesComTenant_TodasClassificadas_NumaListaSo()
    {
        await using var db = banco.Contexto(Quem.Sistema());
        var tipos = ApagadorDeEmpresa.TiposComTenant(db).ToList();

        tipos.Where(t => !ClassificacaoDoExpurgo.Dados.Contains(t) && !ClassificacaoDoExpurgo.RegistroOuRastro.Contains(t))
            .Should().BeEmpty("classifique a entidade em ClassificacaoDoExpurgo (dado ou registro/rastro)");
        ClassificacaoDoExpurgo.Dados.Intersect(ClassificacaoDoExpurgo.RegistroOuRastro).Should().BeEmpty();
        ClassificacaoDoExpurgo.Dados.Concat(ClassificacaoDoExpurgo.RegistroOuRastro)
            .Should().OnlyContain(t => tipos.Contains(t), "a lista não tem tipo fora do modelo");
    }
}
