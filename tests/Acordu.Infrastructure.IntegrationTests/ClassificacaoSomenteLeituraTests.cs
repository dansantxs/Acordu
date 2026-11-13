using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence;
using Acordu.Infrastructure.Persistence.Cadastros;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>
/// ADR 0015, item 2 (design do F14, D2): toda entidade com <c>TenantId</c> do modelo está classificada para o só leitura,
/// como da empresa ou da pessoa ou rastro, numa lista só. Entidade nova sem classificação faz este teste falhar (e, até
/// lá, a guarda a trata como da empresa).
/// </summary>
[Collection(ColecaoBanco.Nome)]
public sealed class ClassificacaoSomenteLeituraTests(BancoFixture banco)
{
    [Fact]
    public async Task EntidadesComTenant_TodasClassificadas_NumaListaSo()
    {
        await using var db = banco.Contexto(Quem.Sistema());
        var tipos = ExpurgoCadastros.TiposComTenant(db).ToList();
        var daEmpresa = ClassificacaoSomenteLeitura.DaEmpresa;
        var daPessoa = ClassificacaoSomenteLeitura.DaPessoaOuRastro;

        tipos.Where(t => !daEmpresa.Contains(t) && !daPessoa.Contains(t))
            .Should().BeEmpty("classifique a entidade em ClassificacaoSomenteLeitura (da empresa ou da pessoa/rastro)");
        daEmpresa.Intersect(daPessoa).Should().BeEmpty();
        daEmpresa.Concat(daPessoa).Where(t => t != typeof(Tenant))
            .Should().OnlyContain(t => tipos.Contains(t), "a lista não tem tipo fora do modelo");
    }

    [Fact]
    public void Tenant_EhDaEmpresa()
    {
        ClassificacaoSomenteLeitura.DaEmpresa.Should().Contain(typeof(Tenant));
    }
}
