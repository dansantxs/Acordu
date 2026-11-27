using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>RNF-SEG-01: um tenant nunca enxerga dados de outro, em nenhum tipo de agregado.</summary>
[Collection(ColecaoBanco.Nome)]
public sealed class IsolamentoTenantTests(BancoFixture banco) : IAsyncLifetime
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private Guid _solicitacaoA;

    public async Task InitializeAsync()
    {
        await Semear(_tenantA, guardar: id => _solicitacaoA = id);
        await Semear(_tenantB, guardar: _ => { });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task Semear(Guid tenant, Action<Guid> guardar)
    {
        var cliente = Dados.Cliente(tenant);
        var contrato = Dados.Contrato(tenant, cliente.Id);
        var solicitacao = Dados.Solicitacao(tenant, cliente.Id, contrato.Id);
        var tarefa = Dados.Tarefa(tenant, cliente.Id, solicitacao.Id);
        guardar(solicitacao.Id);

        await using var db = banco.DaEquipe(tenant);
        db.AddRange(
            Domain.Entities.CalendarioUtil.Padrao(tenant),
            Domain.Entities.Categoria.Criar(Guid.NewGuid(), tenant, "Design"),
            cliente,
            contrato,
            Domain.Entities.AjusteSaldo.Registrar(Guid.NewGuid(), contrato, new Domain.ValueObjects.DuracaoAssinada(60), "Recarga", Dados.Hoje, Guid.NewGuid()),
            solicitacao,
            Dados.Interacao(tenant, cliente.Id, solicitacao.Id),
            Dados.Acesso(tenant, cliente.Id, solicitacao.Id).Acesso,
            Dados.Conversa(tenant, cliente.Id, solicitacao.Id),
            Dados.Projeto(tenant, cliente.Id),
            tarefa,
            Dados.Apontamento(tenant, cliente.Id, tarefa.Id, contrato.Id),
            Dados.Cronometro(tenant),
            Dados.EmailRecebido(tenant),
            Dados.ItemTriagem(tenant),
            Dados.Recorrencia(tenant, cliente.Id));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CadaTipo_SoODoProprioTenant()
    {
        await using var db = banco.DaEquipe(_tenantA);

        (await db.Calendarios.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
        (await db.Categorias.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
        (await db.Clientes.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
        (await db.Contratos.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
        (await db.AjustesSaldo.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
        (await db.Solicitacoes.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
        (await db.Interacoes.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
        (await db.AcessosPortal.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
        (await db.ConversasPorEmail.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
        (await db.Projetos.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
        (await db.Tarefas.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
        (await db.Apontamentos.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
        (await db.Cronometros.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
        (await db.EmailsRecebidos.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
        (await db.ItensTriagemEmail.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
        (await db.Recorrencias.ToListAsync()).Should().OnlyContain(e => e.TenantId == _tenantA).And.HaveCount(1);
    }

    [Fact]
    public async Task BuscaPorId_DeOutroTenant_NaoEncontra()
    {
        await using var db = banco.DaEquipe(_tenantB);

        (await db.Solicitacoes.FindAsync(_solicitacaoA)).Should().BeNull();
        (await db.Solicitacoes.AnyAsync(s => s.Id == _solicitacaoA)).Should().BeFalse();
    }

    [Fact]
    public async Task SemTenantNoContexto_NaoEnxergaNada()
    {
        await using var db = banco.Contexto(Quem.Sistema());

        (await db.Solicitacoes.AnyAsync()).Should().BeFalse();
        (await db.Clientes.AnyAsync()).Should().BeFalse();
        (await db.Calendarios.AnyAsync()).Should().BeFalse();
    }
}
