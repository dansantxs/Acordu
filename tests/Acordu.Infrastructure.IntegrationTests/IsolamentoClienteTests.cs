using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>RNF-SEG-02: no contexto de um contato, só os dados do cliente dele; equipe do prestador vê todos.</summary>
[Collection(ColecaoBanco.Nome)]
public sealed class IsolamentoClienteTests(BancoFixture banco) : IAsyncLifetime
{
    private readonly Guid _tenant = Guid.NewGuid();
    private Cliente _clienteA = null!;
    private Cliente _clienteB = null!;

    public async Task InitializeAsync()
    {
        _clienteA = Dados.Cliente(_tenant, "Cliente A");
        _clienteB = Dados.Cliente(_tenant, "Cliente B");
        await using var db = banco.DaEquipe(_tenant);
        db.AddRange(CalendarioUtil.Padrao(_tenant), Categoria.Criar(Guid.NewGuid(), _tenant, "Design"));
        db.Add(Dados.Tarefa(_tenant, null)); // tarefa interna, sem cliente
        db.Add(Dados.Cronometro(_tenant));
        db.Add(Dados.EmailRecebido(_tenant));
        foreach (var cliente in new[] { _clienteA, _clienteB })
        {
            var contrato = Dados.Contrato(_tenant, cliente.Id);
            var solicitacao = Dados.Solicitacao(_tenant, cliente.Id, contrato.Id);
            var tarefa = Dados.Tarefa(_tenant, cliente.Id, solicitacao.Id);
            db.AddRange(
                cliente,
                contrato,
                AjusteSaldo.Registrar(Guid.NewGuid(), contrato, new Domain.ValueObjects.DuracaoAssinada(60), "Recarga", Dados.Hoje, Guid.NewGuid()),
                solicitacao,
                Dados.Interacao(_tenant, cliente.Id, solicitacao.Id),
                Dados.Acesso(_tenant, cliente.Id, solicitacao.Id).Acesso,
                Dados.Conversa(_tenant, cliente.Id, solicitacao.Id),
                Dados.Projeto(_tenant, cliente.Id),
                tarefa,
                Dados.Apontamento(_tenant, cliente.Id, tarefa.Id, contrato.Id),
                Dados.Recorrencia(_tenant, cliente.Id));
        }

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Contato_SoVeOProprioCliente()
    {
        var a = _clienteA.Id;
        await using var db = banco.DoCliente(_tenant, a);

        (await db.Clientes.ToListAsync()).Should().ContainSingle(c => c.Id == a).And.HaveCount(1);
        (await db.Contratos.ToListAsync()).Should().OnlyContain(e => e.ClienteId == a).And.HaveCount(1);
        (await db.AjustesSaldo.ToListAsync()).Should().OnlyContain(e => e.ClienteId == a).And.HaveCount(1);
        (await db.Solicitacoes.ToListAsync()).Should().OnlyContain(e => e.ClienteId == a).And.HaveCount(1);
        (await db.Interacoes.ToListAsync()).Should().OnlyContain(e => e.ClienteId == a).And.HaveCount(1);
        (await db.AcessosPortal.ToListAsync()).Should().OnlyContain(e => e.ClienteId == a).And.HaveCount(1);
        (await db.ConversasPorEmail.ToListAsync()).Should().OnlyContain(e => e.ClienteId == a).And.HaveCount(1);
        (await db.Projetos.ToListAsync()).Should().OnlyContain(e => e.ClienteId == a).And.HaveCount(1);
        (await db.Tarefas.ToListAsync()).Should().OnlyContain(e => e.ClienteId == a).And.HaveCount(1);
        (await db.Apontamentos.ToListAsync()).Should().OnlyContain(e => e.ClienteId == a).And.HaveCount(1);
        (await db.Recorrencias.ToListAsync()).Should().OnlyContain(e => e.ClienteId == a).And.HaveCount(1);
    }

    [Fact]
    public async Task Contato_NaoVeCronometroMasVeCategoriaECalendario()
    {
        await using var db = banco.DoCliente(_tenant, _clienteA.Id);

        (await db.Cronometros.AnyAsync()).Should().BeFalse();
        (await db.EmailsRecebidos.AnyAsync()).Should().BeFalse("o registro de entrada é só da equipe (F15d)");
        (await db.Categorias.CountAsync()).Should().Be(1);
        (await db.Calendarios.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Equipe_VeTodosOsClientesETarefaInterna()
    {
        await using var db = banco.DaEquipe(_tenant);

        (await db.Clientes.CountAsync()).Should().Be(2);
        (await db.Solicitacoes.CountAsync()).Should().Be(2);
        (await db.Tarefas.CountAsync()).Should().Be(3);
        (await db.Cronometros.CountAsync()).Should().Be(1);
        (await db.EmailsRecebidos.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ContatoDeOutroTenantComMesmoClienteId_NaoVeNada()
    {
        await using var db = banco.DoCliente(Guid.NewGuid(), _clienteA.Id);

        (await db.Solicitacoes.AnyAsync()).Should().BeFalse();
        (await db.Clientes.AnyAsync()).Should().BeFalse();
    }
}
