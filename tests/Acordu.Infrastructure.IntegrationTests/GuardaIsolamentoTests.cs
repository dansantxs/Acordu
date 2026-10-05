using Acordu.Domain.Entities;
using Acordu.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>O contexto recusa gravar fora do tenant (e, para contatos, fora do cliente) — defesa contra bug de orquestração.</summary>
[Collection(ColecaoBanco.Nome)]
public sealed class GuardaIsolamentoTests(BancoFixture banco)
{
    private readonly Guid _tenant = Guid.NewGuid();

    [Fact]
    public async Task IncluirDeOutroTenant_Recusa()
    {
        await using var db = banco.DaEquipe(_tenant);
        db.Add(Dados.Cliente(Guid.NewGuid()));

        var acao = () => db.SaveChangesAsync();

        await acao.Should().ThrowAsync<IsolamentoVioladoException>();
    }

    [Fact]
    public async Task GravarSemTenant_Recusa()
    {
        await using var db = banco.Contexto(Quem.Sistema());
        db.Add(Dados.Cliente(_tenant));

        var acao = () => db.SaveChangesAsync();

        await acao.Should().ThrowAsync<IsolamentoVioladoException>();
    }

    [Fact]
    public async Task Contato_GravarNoProprioCliente_Aceita()
    {
        var cliente = Dados.Cliente(_tenant);
        await using (var equipe = banco.DaEquipe(_tenant))
        {
            equipe.Add(cliente);
            await equipe.SaveChangesAsync();
        }

        await using var db = banco.DoCliente(_tenant, cliente.Id);
        db.Add(Dados.Solicitacao(_tenant, cliente.Id));

        var acao = () => db.SaveChangesAsync();

        await acao.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Contato_GravarEmOutroCliente_Recusa()
    {
        await using var db = banco.DoCliente(_tenant, Guid.NewGuid());
        db.Add(Dados.Solicitacao(_tenant, Guid.NewGuid()));

        var acao = () => db.SaveChangesAsync();

        await acao.Should().ThrowAsync<IsolamentoVioladoException>();
    }

    [Fact]
    public async Task Contato_GravarTipoSemCliente_Recusa()
    {
        await using var db = banco.DoCliente(_tenant, Guid.NewGuid());
        db.Add(Categoria.Criar(Guid.NewGuid(), _tenant, "Nova"));

        var acao = () => db.SaveChangesAsync();

        await acao.Should().ThrowAsync<IsolamentoVioladoException>();
    }

    [Fact]
    public async Task Recusa_NaoGravaNadaDoLote()
    {
        var valido = Dados.Cliente(_tenant);
        await using (var db = banco.DaEquipe(_tenant))
        {
            db.AddRange(valido, Dados.Cliente(Guid.NewGuid()));
            await ((Func<Task>)(() => db.SaveChangesAsync())).Should().ThrowAsync<IsolamentoVioladoException>();
        }

        await using var leitura = banco.DaEquipe(_tenant);
        (await leitura.Clientes.AnyAsync(c => c.Id == valido.Id)).Should().BeFalse();
    }
}
