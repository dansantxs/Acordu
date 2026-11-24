using Acordu.Application.Exemplos;
using Acordu.Domain.Entities;
using Acordu.Domain.ValueObjects;
using Acordu.Infrastructure.Persistence.Atividades;
using Acordu.Infrastructure.Persistence.Cadastros;
using Acordu.Infrastructure.Persistence.Colaboracao;
using Acordu.Infrastructure.Persistence.Exemplos;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>
/// Spec do F14, F14-93, F14-94 e F14-96 (design D13): o que conta como exemplo e como dado próprio, e a remoção, que
/// leva tudo o que tem o <c>ClienteId</c> de um cliente de exemplo, deixa o resto e nunca toca outro tenant. Entidade
/// nova com <c>ClienteId</c> entra sozinha na remoção (vem do modelo); uma de dado sem cliente precisa entrar na
/// conferência de "dado próprio", e o teste das listas falha sem isso.
/// </summary>
[Collection(ColecaoBanco.Nome)]
public sealed class DadosDeExemploTests(BancoFixture banco)
{
    [Fact]
    public void TiposConferidos_ComparadosAoExpurgo_SaoOsMesmos()
    {
        DadosDeExemplo.TiposConferidos.Should().BeEquivalentTo(
            ClassificacaoDoExpurgo.Dados, "a regra de \"sem dado\" dos exemplos é a do expurgo de cadastro (design D13)");
    }

    [Fact]
    public async Task TiposComCliente_DoModelo_TodasAsRaizesLigadasACliente()
    {
        await using var db = banco.Contexto(Quem.Sistema());

        DadosDeExemplo.TiposComCliente(db).Should().BeEquivalentTo(new[]
        {
            typeof(AcessoPortal), typeof(AjusteSaldo), typeof(Apontamento), typeof(Contrato), typeof(ConvitePortal),
            typeof(Interacao), typeof(Projeto), typeof(RecorrenciaTarefa), typeof(Solicitacao), typeof(Tarefa),
            typeof(Vinculo), typeof(Atividade),
        });
        DadosDeExemplo.TiposDaTarefa(db).Should().BeEquivalentTo(new[] { typeof(Cronometro), typeof(ComentarioTarefa) });
    }

    [Fact]
    public async Task SituacaoAsync_SoExemplos_SemDadoProprio()
    {
        var tenant = Guid.NewGuid();
        await GravarAsync(tenant, db =>
        {
            var exemplo = Exemplo(tenant);
            var solicitacao = Dados.Solicitacao(tenant, exemplo.Id);
            db.AddRange(exemplo, solicitacao, Dados.Tarefa(tenant, exemplo.Id, solicitacao.Id));
        });

        var situacao = await SituacaoAsync(tenant);

        situacao.Should().Be(new SituacaoDosExemplos(Carregados: true, DadosProprios: false));
    }

    [Fact]
    public async Task SituacaoAsync_TarefaSemCliente_EhDadoProprio() // F14-93
    {
        var tenant = Guid.NewGuid();
        await GravarAsync(tenant, db => db.AddRange(Exemplo(tenant), Dados.Tarefa(tenant, cliente: null)));

        (await SituacaoAsync(tenant)).DadosProprios.Should().BeTrue();
    }

    [Fact]
    public async Task SituacaoAsync_EmpresaVazia_NemExemploNemDado() // F14-91
    {
        (await SituacaoAsync(Guid.NewGuid())).Should().Be(new SituacaoDosExemplos(Carregados: false, DadosProprios: false));
    }

    [Fact]
    public async Task RemoverAsync_ExemplosDaEmpresa_SaemComOQueEhDelesEORestoFica() // F14-94, F14-96
    {
        var tenant = Guid.NewGuid();
        var outro = Guid.NewGuid();
        var exemplo = Exemplo(tenant);
        var real = Dados.Cliente(tenant, "Cliente de verdade");
        var solicitacao = Dados.Solicitacao(tenant, exemplo.Id);
        var tarefa = Dados.Tarefa(tenant, exemplo.Id, solicitacao.Id);
        var interacao = Dados.Interacao(tenant, exemplo.Id, solicitacao.Id);
        var daEquipe = Dados.Tarefa(tenant, exemplo.Id, solicitacao.Id);
        var solicitacaoReal = Dados.Solicitacao(tenant, real.Id);
        var cronometro = Cronometro.Iniciar(Guid.NewGuid(), tenant, Guid.NewGuid(), tarefa.Id, Dados.Agora);
        var tarefaReal = Dados.Tarefa(tenant, real.Id, solicitacaoReal.Id);
        var politica = new PoliticaAnexos(20L * 1024 * 1024, ["image/png"], 5);
        var comentario = ComentarioTarefa.Registrar(
            Guid.NewGuid(), tenant, tarefa.Id, Guid.NewGuid(), "Ver com o cliente", [], politica, Dados.Agora);
        var comentarioReal = ComentarioTarefa.Registrar(
            Guid.NewGuid(), tenant, tarefaReal.Id, Guid.NewGuid(), "Ver com o cliente", [], politica, Dados.Agora);
        await GravarAsync(tenant, db => db.AddRange(
            exemplo, real, solicitacao, tarefa, interacao, daEquipe, solicitacaoReal, cronometro, tarefaReal,
            comentario, comentarioReal,
            Dados.Apontamento(tenant, exemplo.Id, tarefa.Id), Dados.Projeto(tenant, exemplo.Id)));
        var seguidor = Guid.NewGuid();
        await using (var db = banco.DaEquipe(tenant))
        {
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO seguidores (tenant_id, alvo, alvo_id, usuario_id, desde) VALUES ({tenant}, 2, {tarefa.Id}, {seguidor}, {Dados.Agora}), ({tenant}, 2, {tarefaReal.Id}, {seguidor}, {Dados.Agora})");
        }
        var exemploDoOutro = Exemplo(outro);
        var solicitacaoDoOutro = Dados.Solicitacao(outro, exemploDoOutro.Id);
        await GravarAsync(outro, db => db.AddRange(exemploDoOutro, solicitacaoDoOutro));

        ExemplosRemovidos removidos;
        var quem = Quem.Equipe(tenant);
        await using (var db = banco.Contexto(quem))
        {
            removidos = await new DadosDeExemplo(db, quem).RemoverAsync();
            await db.SaveChangesAsync();
            await db.Database.CommitTransactionAsync();
        }

        removidos.Clientes.Should().Be(1);
        removidos.Arquivos.Should().ContainSingle().Which.Should().Be(interacao.Anexos[0].ChaveArmazenamento);
        await using (var db = banco.DaEquipe(tenant))
        {
            (await db.Clientes.Select(c => c.Id).ToListAsync()).Should().Equal(real.Id);
            (await db.Solicitacoes.Select(s => s.Id).ToListAsync()).Should().Equal(solicitacaoReal.Id);
                        (await db.Interacoes.AnyAsync()).Should().BeFalse();
            (await db.Apontamentos.AnyAsync()).Should().BeFalse();
            (await db.Projetos.AnyAsync()).Should().BeFalse();
            (await db.Cronometros.AnyAsync()).Should().BeFalse("o cronômetro de uma tarefa do exemplo sai com ela");
            (await db.ComentariosTarefa.Select(c => c.Id).ToListAsync()).Should().Equal(comentarioReal.Id);
            (await db.Tarefas.Select(t => t.Id).ToListAsync()).Should().Equal(tarefaReal.Id);
            (await db.Set<Seguidor>().Select(x => x.AlvoId).ToListAsync())
                .Should().Equal(tarefaReal.Id);
        }

        await using (var db = banco.DaEquipe(outro))
        {
            (await db.Clientes.Select(c => c.Id).ToListAsync()).Should().Equal(exemploDoOutro.Id);
            (await db.Solicitacoes.Select(s => s.Id).ToListAsync()).Should().Equal(solicitacaoDoOutro.Id);
        }
    }

    [Fact]
    public async Task RemoverAsync_SemExemplos_NaoMudaNada()
    {
        var tenant = Guid.NewGuid();
        var real = Dados.Cliente(tenant);
        await GravarAsync(tenant, db => db.Add(real));

        var quem = Quem.Equipe(tenant);
        await using var db = banco.Contexto(quem);
        var removidos = await new DadosDeExemplo(db, quem).RemoverAsync();

        removidos.Should().BeEquivalentTo(new ExemplosRemovidos(0, []));
        db.Database.CurrentTransaction.Should().BeNull("sem exemplos, nada é aberto nem gravado");
        (await db.Clientes.CountAsync()).Should().Be(1);
    }

    private static Cliente Exemplo(Guid tenant)
    {
        var cliente = Domain.Entities.Cliente.Criar(Guid.NewGuid(), tenant, "Padaria de exemplo", null, null);
        cliente.MarcarComoExemplo();
        return cliente;
    }

    private async Task GravarAsync(Guid tenant, Action<Persistence.AcorduDbContext> montar)
    {
        await using var db = banco.DaEquipe(tenant);
        montar(db);
        await db.SaveChangesAsync();
    }

    private async Task<SituacaoDosExemplos> SituacaoAsync(Guid tenant)
    {
        var quem = Quem.Equipe(tenant);
        await using var db = banco.Contexto(quem);
        return await new DadosDeExemplo(db, quem).SituacaoAsync(criadorId: null);
    }
}
