using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using Acordu.Infrastructure.Persistence;
using Acordu.Infrastructure.Persistence.EntradaEmail;
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

    [Fact] // F15-01
    public async Task PreenchimentoDoF15b_EmpresasAnteriores_CodigoDeEntradaUnicoDoAlfabeto_DemonstracaoSem()
    {
        var anteriores = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        var demonstracao = Guid.NewGuid();
        var nova = Guid.NewGuid();
        string? codigoDaNova;
        foreach (var anterior in anteriores)
        {
            await using var db = banco.DaEquipe(anterior);
            db.Add(Tenant.Criar(anterior, "Estúdio Traço", Dados.Agora, Guid.NewGuid(), TimeSpan.FromDays(30)));
            await db.SaveChangesAsync();
            // Como a migration encontra as empresas anteriores ao F15: a coluna nova vazia.
            await db.Database.ExecuteSqlAsync($"UPDATE tenants SET codigo_entrada = NULL WHERE id = {anterior}");
        }

        await using (var db = banco.DaEquipe(demonstracao))
        {
            db.Add(Tenant.CriarDemonstracao(demonstracao, "Estúdio Aurora", Dados.Agora, Guid.NewGuid()));
            await db.SaveChangesAsync();
        }

        await using (var db = banco.DaEquipe(nova))
        {
            var tenant = Tenant.Criar(nova, "Já do F15", Dados.Agora, Guid.NewGuid(), TimeSpan.FromDays(30));
            codigoDaNova = tenant.CodigoEntrada;
            db.Add(tenant);
            await db.SaveChangesAsync();
        }

        await using (var sistema = banco.Contexto(Quem.Sistema()))
            await sistema.Database.ExecuteSqlRawAsync(ScriptsDeDados.PreencherCodigoEntradaDasEmpresas);

        var codigos = new List<string?>();
        foreach (var anterior in anteriores)
        {
            await using var leitura = banco.DaEquipe(anterior);
            var depois = await leitura.Tenants.AsNoTracking().SingleAsync();
            depois.RecebimentoEmail.Should().BeFalse("o recebimento nasce desligado (F15-01)");
            codigos.Add(depois.CodigoEntrada);
        }

        codigos.Should().OnlyContain(c => CodigoDeEntrada.Valido(c), "8 caracteres do alfabeto sem ambíguos (design D2)")
            .And.OnlyHaveUniqueItems();
        codigos.Should().NotContain(c => c!.Contains("traco", StringComparison.Ordinal));
        await using (var leitura = banco.DaEquipe(demonstracao))
            (await leitura.Tenants.AsNoTracking().SingleAsync()).CodigoEntrada.Should().BeNull("a demonstração não tem endereço (F15-96)");
        await using (var leitura = banco.DaEquipe(nova))
            (await leitura.Tenants.AsNoTracking().SingleAsync()).CodigoEntrada.Should().Be(codigoDaNova, "só as linhas vazias");

        await using var todas = banco.Contexto(Quem.Sistema());
        var repetidos = await todas.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM (SELECT codigo_entrada FROM tenants WHERE codigo_entrada IS NOT NULL GROUP BY codigo_entrada HAVING count(*) > 1) r")
            .SingleAsync();
        repetidos.Should().Be(0, "o código é único entre todas as empresas");
    }

    [Fact] // F15-01, design D2
    public async Task CodigoDeEntrada_RepetidoEmOutraEmpresa_IndiceUnicoRecusa()
    {
        var primeira = Tenant.Criar(Guid.NewGuid(), "Estúdio Traço", Dados.Agora, Guid.NewGuid(), TimeSpan.FromDays(30));
        await using (var db = banco.DaEquipe(primeira.Id))
        {
            db.Add(primeira);
            await db.SaveChangesAsync();
        }

        var segunda = Tenant.Criar(Guid.NewGuid(), "Padaria Bom Grão", Dados.Agora, Guid.NewGuid(), TimeSpan.FromDays(30));
        typeof(Tenant).GetProperty(nameof(Tenant.CodigoEntrada))!.SetValue(segunda, primeira.CodigoEntrada);
        await using var outra = banco.DaEquipe(segunda.Id);
        outra.Add(segunda);

        var gravar = () => outra.SaveChangesAsync();

        var excecao = (await gravar.Should().ThrowAsync<DbUpdateException>()).Which;
        CodigosDeEntrada.Disputa(excecao).Should().BeTrue("o cadastro reconhece a colisão e sorteia outro código");
    }

    [Fact] // F15-01, design D2
    public async Task CodigoDeEntrada_ColisaoNaTransacao_SorteiaOutroEGravaSemPerderOAnterior()
    {
        var a = Tenant.Criar(Guid.NewGuid(), "Estúdio Traço", Dados.Agora, Guid.NewGuid(), TimeSpan.FromDays(30));
        await using (var db = banco.DaEquipe(a.Id))
        {
            db.Add(a);
            await db.SaveChangesAsync();
        }

        var anterior = Tenant.Criar(Guid.NewGuid(), "Gravada antes", Dados.Agora, Guid.NewGuid(), TimeSpan.FromDays(30));
        var b = Tenant.Criar(Guid.NewGuid(), "Padaria Bom Grão", Dados.Agora, Guid.NewGuid(), TimeSpan.FromDays(30));
        typeof(Tenant).GetProperty(nameof(Tenant.CodigoEntrada))!.SetValue(b, a.CodigoEntrada);
        await using (var sistema = banco.Contexto(Quem.Sistema()))
        {
            // Como o cadastro: tudo numa transação, e a colisão chega depois de outra gravação já feita nela.
            await using var transacao = await sistema.Database.BeginTransactionAsync();
            sistema.Add(anterior);
            await sistema.SaveChangesAsync();
            sistema.Add(b);

            var colisao = await ((Func<Task>)(() => sistema.SaveChangesAsync())).Should().ThrowAsync<DbUpdateException>();
            CodigosDeEntrada.Disputa(colisao.Which).Should().BeTrue();
            b.SortearOutroCodigoEntrada();
            await sistema.SaveChangesAsync();
            await transacao.CommitAsync();
        }

        await using var leitura = banco.Contexto(Quem.Sistema());
        var gravadas = await leitura.Tenants.AsNoTracking().Where(t => t.Id == a.Id || t.Id == b.Id || t.Id == anterior.Id).ToListAsync();
        gravadas.Should().HaveCount(3, "a gravação anterior da transação sobreviveu à colisão");
        var codigoA = gravadas.Single(t => t.Id == a.Id).CodigoEntrada;
        var codigoB = gravadas.Single(t => t.Id == b.Id).CodigoEntrada;
        codigoB.Should().NotBe(codigoA);
        CodigoDeEntrada.Valido(codigoB).Should().BeTrue();
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
