using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.IntegrationTests;

[Collection(ColecaoBanco.Nome)]
public sealed class MigrationsTests(BancoFixture banco)
{
    [Fact]
    public async Task Migrations_AplicadasNoBancoVazio_SemPendencias()
    {
        await using var db = banco.Contexto(Quem.Sistema());

        (await db.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Modelo_SemMudancasForaDeMigration()
    {
        await using var db = banco.Contexto(Quem.Sistema());

        db.Database.HasPendingModelChanges().Should().BeFalse(
            "toda mudança de mapeamento precisa de migration (workflow Migrations)");
    }
}
