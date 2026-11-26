using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
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
            db.Add(Tenant.Criar(tenant, "Anterior ao F9", Dados.Agora, segundo.UsuarioId, TimeSpan.FromDays(30)));
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

    [Fact]
    public async Task PreenchimentoDoF14_EmpresaAnterior_FicaCanceladaComDataAntigaESoLeitura() // F14-08
    {
        var anterior = Guid.NewGuid();
        var nova = Guid.NewGuid();
        await using (var db = banco.DaEquipe(anterior))
        {
            db.Add(Tenant.Criar(anterior, "Anterior ao F14", Dados.Agora, Guid.NewGuid(), TimeSpan.FromDays(30)));
            await db.SaveChangesAsync();
            // Como a migration encontra as empresas anteriores: a coluna nova vazia, sem as datas.
            await db.Database.ExecuteSqlAsync(
                $"UPDATE tenants SET situacao = '', fim_do_teste = NULL WHERE id = {anterior}");
        }

        await using (var db = banco.DaEquipe(nova))
        {
            db.Add(Tenant.Criar(nova, "Já do F14", Dados.Agora, Guid.NewGuid(), TimeSpan.FromDays(30)));
            await db.SaveChangesAsync();
        }

        await using (var sistema = banco.Contexto(Quem.Sistema()))
            await sistema.Database.ExecuteSqlRawAsync(ScriptsDeDados.CancelarEmpresasAnterioresAoF14);

        await using (var leitura = banco.DaEquipe(anterior))
        {
            var depois = await leitura.Tenants.AsNoTracking().SingleAsync();
            depois.Situacao.Should().Be(SituacaoEmpresa.Cancelada);
            depois.SomenteLeitura.Should().BeTrue();
            depois.CanceladaEm.Should().Be(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            depois.SomenteLeituraDesde.Should().Be(ScriptsDeDados.CanceladasAntesDoF14Em);
            depois.FimDoTeste.Should().BeNull();
            // Antiga o bastante para o prazo do expurgo (30 dias, decisão 2 do G2) já ter passado antes do F14.
            depois.ExpurgoPrevistoEm(TimeSpan.FromDays(30)).Should().BeBefore(Dados.Agora);
        }

        await using (var leitura = banco.DaEquipe(nova))
        {
            var intacta = await leitura.Tenants.AsNoTracking().SingleAsync();
            intacta.Situacao.Should().Be(SituacaoEmpresa.Teste, "o preenchimento só pega as linhas que a coluna nova recebeu vazias");
            intacta.FimDoTeste.Should().Be(Dados.Agora.AddDays(30));
            intacta.CanceladaEm.Should().BeNull();
        }
    }

    [Fact] // F15-62
    public async Task PreenchimentoDoF15a_MensagensAnteriores_DoContatoNoPortalEDaEquipeNoAcordu()
    {
        var tenant = Guid.NewGuid();
        var cliente = Dados.Cliente(tenant);
        var solicitacao = Dados.Solicitacao(tenant, cliente.Id);
        var daEquipe = Dados.Interacao(tenant, cliente.Id, solicitacao.Id);
        var doContato = Interacao.Registrar(
            Guid.NewGuid(), tenant, cliente.Id, solicitacao.Id, Guid.NewGuid(), true, "Segue o logo", Visibilidade.Externa,
            CanalMensagem.Portal, [], new PoliticaAnexos(1024, ["image/png"], 5), Dados.Agora);
        var porEmail = Interacao.Registrar(
            Guid.NewGuid(), tenant, cliente.Id, solicitacao.Id, Guid.NewGuid(), true, "Já é do F15", Visibilidade.Externa,
            CanalMensagem.Email, [], new PoliticaAnexos(1024, ["image/png"], 5), Dados.Agora);
        await using (var db = banco.DaEquipe(tenant))
        {
            db.AddRange(cliente, solicitacao, daEquipe, doContato, porEmail);
            await db.SaveChangesAsync();
            // Como a migration encontra as mensagens anteriores ao F15: a coluna nova vazia.
            await db.Database.ExecuteSqlAsync(
                $"UPDATE interacoes SET canal = '' WHERE id IN ({daEquipe.Id}, {doContato.Id})");
        }

        await using (var sistema = banco.Contexto(Quem.Sistema()))
            await sistema.Database.ExecuteSqlRawAsync(ScriptsDeDados.PreencherCanalDasInteracoes);

        await using var leitura = banco.DaEquipe(tenant);
        var canais = await leitura.Interacoes.AsNoTracking()
            .Where(i => i.SolicitacaoId == solicitacao.Id)
            .ToDictionaryAsync(i => i.Id, i => i.Canal);
        canais[daEquipe.Id].Should().Be(CanalMensagem.Acordu);
        canais[doContato.Id].Should().Be(CanalMensagem.Portal);
        canais[porEmail.Id].Should().Be(CanalMensagem.Email, "o preenchimento só pega as linhas que a coluna nova recebeu vazias");
    }

    [Fact]
    public async Task Situacao_GravadaComoTextoEmMaiusculas()
    {
        var tenant = Guid.NewGuid();
        await using (var db = banco.DaEquipe(tenant))
        {
            db.Add(Tenant.Criar(tenant, "Estúdio", Dados.Agora, Guid.NewGuid(), TimeSpan.FromDays(30)));
            await db.SaveChangesAsync();
        }

        await using var sistema = banco.Contexto(Quem.Sistema());
        var situacao = await sistema.Database
            .SqlQuery<string>($"SELECT situacao AS \"Value\" FROM tenants WHERE id = {tenant}")
            .SingleAsync();
        situacao.Should().Be("TESTE");
    }
}
