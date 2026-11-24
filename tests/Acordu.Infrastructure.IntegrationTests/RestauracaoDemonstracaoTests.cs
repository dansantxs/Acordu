using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Cadastros;
using Acordu.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>
/// Spec do F14, F14-88 (design D13): a restauração da demonstração apaga tudo o que tem o <c>TenantId</c> dela, sem apagar
/// a linha da empresa nem as sessões abertas nela, e nenhuma outra empresa é tocada.
/// </summary>
[Collection(ColecaoBanco.Nome)]
public sealed class RestauracaoDemonstracaoTests(BancoFixture banco)
{
    [Fact]
    public async Task ApagarParaRestaurar_Demonstracao_ApagaOsDadosDelaEMantemEmpresaESessoes() // F14-88
    {
        var demonstracao = Guid.NewGuid();
        var outra = Guid.NewGuid();
        var pessoa = Guid.NewGuid();
        var agora = Dados.Agora.ToUniversalTime();
        await using (var db = banco.DaEquipe(demonstracao))
        {
            db.Add(Tenant.CriarDemonstracao(demonstracao, "Estúdio Aurora", Dados.Agora, Guid.NewGuid()));
            var cliente = Dados.Cliente(demonstracao, "Padaria Grão Dourado");
            db.Add(cliente);
            db.Add(Dados.Tarefa(demonstracao, cliente.Id));
            db.AddRange(Papel.Padroes(demonstracao));
            db.Add(new RefreshToken(pessoa, demonstracao, $"hash-{Guid.NewGuid():N}", agora, agora.AddDays(30)));
            await db.SaveChangesAsync();
        }

        await using (var db = banco.DaEquipe(outra))
        {
            db.Add(Tenant.Criar(outra, "Estúdio Traço", Dados.Agora, Guid.NewGuid(), TimeSpan.FromDays(30)));
            var cliente = Dados.Cliente(outra, "Cliente de outra empresa");
            db.Add(cliente);
            db.Add(Dados.Tarefa(outra, cliente.Id));
            db.AddRange(Papel.Padroes(outra));
            await db.SaveChangesAsync();
        }

        await using (var db = banco.DaEquipe(demonstracao))
        {
            await using var transacao = await db.Database.BeginTransactionAsync();
            var tenant = await db.Tenants.SingleAsync();
            await ApagadorDeEmpresa.ApagarParaRestaurarAsync(db, tenant, CancellationToken.None);
            await transacao.CommitAsync();
        }

        await using (var db = banco.DaEquipe(demonstracao))
        {
            (await db.Tenants.SingleOrDefaultAsync()).Should().NotBeNull("a empresa fica: só os dados saem");
            (await db.Clientes.CountAsync()).Should().Be(0);
            (await db.Tarefas.CountAsync()).Should().Be(0);
            (await db.Papeis.CountAsync()).Should().Be(0, "os papéis são semeados de novo por quem restaura");
            (await db.Set<RefreshToken>().CountAsync(r => r.TenantId == demonstracao)).Should().Be(1, "as sessões abertas nela ficam");
        }

        await using (var db = banco.DaEquipe(outra))
        {
            (await db.Clientes.Select(c => c.Nome).ToListAsync()).Should().Equal("Cliente de outra empresa");
            (await db.Tarefas.CountAsync()).Should().Be(1);
            (await db.Papeis.CountAsync()).Should().Be(Papel.Padroes(outra).Count, "nenhuma outra empresa é tocada");
        }
    }
}
