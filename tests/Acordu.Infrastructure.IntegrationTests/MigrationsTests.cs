using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence;
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

    [Fact]
    public async Task PreenchimentoDoF9_CriadorEhODonoMaisAntigo_EEmpresaFicaLiberada()
    {
        var tenant = Guid.NewGuid();
        var papeis = Papel.Padroes(tenant);
        var dono = papeis.Single(p => p.Nome == Papel.NomeDono);
        var primeiro = Vinculo.Criar(Guid.CreateVersion7(Dados.Agora), Guid.NewGuid(), dono);
        var segundo = Vinculo.Criar(Guid.CreateVersion7(Dados.Agora.AddMinutes(1)), Guid.NewGuid(), dono);
        await using (var db = banco.DaEquipe(tenant))
        {
            db.Add(Tenant.Criar(tenant, "Anterior ao F9", Dados.Agora, segundo.UsuarioId));
            db.AddRange(papeis);
            db.AddRange(segundo, primeiro);
            await db.SaveChangesAsync();
            // Como estavam as empresas antes da migration: sem criador e sem liberação.
            await db.Database.ExecuteSqlAsync(
                $"UPDATE tenants SET criado_por_usuario_id = NULL, envio_liberado_em = NULL WHERE id = {tenant}");
        }

        // O script é o da migration e roda no banco inteiro: libera todos os tenants do banco de testes. Nenhum teste de
        // integração depende de empresa não liberada (os do bloqueio são funcionais, com banco próprio).
        await using (var sistema = banco.Contexto(Quem.Sistema()))
            await sistema.Database.ExecuteSqlRawAsync(ScriptsDeDados.PreencherCriadorELiberacao);

        await using var leitura = banco.DaEquipe(tenant);
        var depois = await leitura.Tenants.AsNoTracking().SingleAsync();
        depois.CriadoPorUsuarioId.Should().Be(primeiro.UsuarioId);
        depois.EnvioLiberado.Should().BeTrue();
    }
}
