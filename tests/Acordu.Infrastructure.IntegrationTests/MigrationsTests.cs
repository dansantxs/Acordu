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

    [Theory]
    [InlineData("ix_solicitacoes_tenant_id_status_prazo_resolucao")]
    [InlineData("ix_solicitacoes_tenant_id_status_limite_risco")]
    [InlineData("ix_apontamentos_tenant_id_contrato_id_data")]
    public async Task IndicesDoReadSide_Existem(string indice)
    {
        await using var db = banco.Contexto(Quem.Sistema());

        var existe = await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_indexes WHERE schemaname = 'public' AND indexname = {indice}")
            .SingleAsync();

        existe.Should().Be(1, "índices acrescentados à mão numa migration não podem se perder ao regenerá-la");
    }
}
