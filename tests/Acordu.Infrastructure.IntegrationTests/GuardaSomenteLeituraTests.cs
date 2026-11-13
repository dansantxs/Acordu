using Acordu.Application.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Infrastructure.Persistence.Auditoria;
using Acordu.Infrastructure.Persistence.Notificacoes;
using Acordu.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>
/// ADR 0015 (design do F14, D2; RF-EMP-02): o contexto recusa incluir, alterar ou remover dado da empresa num tenant
/// VENCIDO ou CANCELADO, antes de qualquer SQL, e deixa passar o que é da pessoa ou rastro. A situação vale pelo tenant da
/// entidade gravada.
/// </summary>
[Collection(ColecaoBanco.Nome)]
public sealed class GuardaSomenteLeituraTests(BancoFixture banco)
{
    /// <summary>Empresa gravada como TESTE, com um cliente, e posta na situação pedida por SQL (fora da guarda).</summary>
    private async Task<(Guid Tenant, Cliente Cliente)> EmpresaAsync(SituacaoEmpresa situacao)
    {
        var tenant = Guid.NewGuid();
        var cliente = Dados.Cliente(tenant);
        cliente.AdicionarContato("Marina Souza", $"marina-{Guid.NewGuid():N}@bomgrao.com.br", null, null);
        await using var db = banco.DaEquipe(tenant);
        db.Add(Tenant.Criar(tenant, "Estúdio Traço", Dados.Agora, Guid.NewGuid(), TimeSpan.FromDays(30)));
        db.Add(cliente);
        await db.SaveChangesAsync();
        var texto = situacao.ToString().ToUpperInvariant();
        await db.Database.ExecuteSqlAsync($"UPDATE tenants SET situacao = {texto} WHERE id = {tenant}");
        return (tenant, cliente);
    }

    public static TheoryData<SituacaoEmpresa> SoLeitura => new() { SituacaoEmpresa.Vencida, SituacaoEmpresa.Cancelada };

    [Theory]
    [MemberData(nameof(SoLeitura))]
    public async Task SaveChanges_IncluirNaEmpresaSoLeitura_RecusaSemGravar(SituacaoEmpresa situacao)
    {
        var (tenant, _) = await EmpresaAsync(situacao);
        var novo = Dados.Cliente(tenant, "Mercado Central");

        await using (var db = banco.DaEquipe(tenant))
        {
            db.Add(novo);
            await ((Func<Task>)(() => db.SaveChangesAsync())).Should().ThrowAsync<EmpresaSomenteLeituraException>();
        }

        await using var leitura = banco.DaEquipe(tenant);
        (await leitura.Clientes.AnyAsync(c => c.Id == novo.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task SaveChanges_AlterarNaEmpresaSoLeitura_RecusaSemGravar()
    {
        var (tenant, cliente) = await EmpresaAsync(SituacaoEmpresa.Vencida);

        await using (var db = banco.DaEquipe(tenant))
        {
            var carregado = await db.Clientes.SingleAsync(c => c.Id == cliente.Id);
            carregado.Editar("Outro nome", null, null);
            await ((Func<Task>)(() => db.SaveChangesAsync())).Should().ThrowAsync<EmpresaSomenteLeituraException>();
        }

        await using var leitura = banco.DaEquipe(tenant);
        (await leitura.Clientes.SingleAsync(c => c.Id == cliente.Id)).Nome.Should().Be(cliente.Nome);
    }

    [Fact]
    public async Task SaveChanges_RemoverNaEmpresaSoLeitura_Recusa()
    {
        var (tenant, cliente) = await EmpresaAsync(SituacaoEmpresa.Cancelada);

        await using var db = banco.DaEquipe(tenant);
        db.Remove(await db.Clientes.SingleAsync(c => c.Id == cliente.Id));

        await ((Func<Task>)(() => db.SaveChangesAsync())).Should().ThrowAsync<EmpresaSomenteLeituraException>();
    }

    [Fact]
    public async Task SaveChanges_FilhaDaRaizNaEmpresaSoLeitura_Recusa()
    {
        var (tenant, cliente) = await EmpresaAsync(SituacaoEmpresa.Vencida);

        await using var db = banco.DaEquipe(tenant);
        var carregado = await db.Clientes.SingleAsync(c => c.Id == cliente.Id);
        // Só a filha (contato, owned) muda; a raiz fica intacta no change tracker.
        carregado.AdicionarContato("Carlos Prado", $"carlos-{Guid.NewGuid():N}@bomgrao.com.br", null, null);

        await ((Func<Task>)(() => db.SaveChangesAsync())).Should().ThrowAsync<EmpresaSomenteLeituraException>();
    }

    [Fact]
    public async Task SaveChanges_PropriaEmpresaSoLeitura_Recusa()
    {
        var (tenant, _) = await EmpresaAsync(SituacaoEmpresa.Vencida);

        await using var db = banco.DaEquipe(tenant);
        (await db.Tenants.SingleAsync()).Renomear("Outro nome");

        await ((Func<Task>)(() => db.SaveChangesAsync())).Should().ThrowAsync<EmpresaSomenteLeituraException>();
    }

    [Fact]
    public async Task SaveChangesSincrono_EmpresaSoLeitura_Recusa()
    {
        var (tenant, _) = await EmpresaAsync(SituacaoEmpresa.Vencida);

        await using var db = banco.DaEquipe(tenant);
        db.Add(Dados.Cliente(tenant, "Mercado Central"));

        ((Action)(() => db.SaveChanges())).Should().Throw<EmpresaSomenteLeituraException>();
    }

    [Theory]
    [MemberData(nameof(SoLeitura))]
    public async Task SaveChanges_DaPessoaOuRastroNaEmpresaSoLeitura_Grava(SituacaoEmpresa situacao)
    {
        var (tenant, _) = await EmpresaAsync(situacao);
        var usuario = Guid.NewGuid();
        var agora = Dados.Agora.ToUniversalTime();

        await using var db = banco.DaEquipe(tenant);
        var preferencia = new PreferenciaNotificacao(tenant, usuario);
        db.Add(preferencia);
        db.Add(RegistroAuditoria.Criar(tenant, usuario, "auth.login.sucesso", null, null, null, null, agora));
        db.Add(new RefreshToken(usuario, tenant, $"hash-{Guid.NewGuid():N}", agora, agora.AddDays(30)));

        await ((Func<Task>)(() => db.SaveChangesAsync())).Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(SituacaoEmpresa.Teste)]
    [InlineData(SituacaoEmpresa.Ativa)]
    public async Task SaveChanges_EmpresaGravavel_Grava(SituacaoEmpresa situacao)
    {
        var (tenant, _) = await EmpresaAsync(situacao);

        await using var db = banco.DaEquipe(tenant);
        db.Add(Dados.Cliente(tenant, "Mercado Central"));

        await ((Func<Task>)(() => db.SaveChangesAsync())).Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveChanges_OutraEmpresaSoLeitura_NaoAfetaEsta()
    {
        await EmpresaAsync(SituacaoEmpresa.Vencida);
        var (gravavel, _) = await EmpresaAsync(SituacaoEmpresa.Teste);

        await using var db = banco.DaEquipe(gravavel);
        db.Add(Dados.Cliente(gravavel, "Mercado Central"));

        await ((Func<Task>)(() => db.SaveChangesAsync())).Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveChanges_PessoaJuntoComDaEmpresaNaSoLeitura_RecusaTudo()
    {
        var (tenant, _) = await EmpresaAsync(SituacaoEmpresa.Vencida);
        var usuario = Guid.NewGuid();
        var novo = Dados.Cliente(tenant, "Mercado Central");

        await using (var db = banco.DaEquipe(tenant))
        {
            db.Add(new PreferenciaNotificacao(tenant, usuario));
            db.Add(novo);
            await ((Func<Task>)(() => db.SaveChangesAsync())).Should().ThrowAsync<EmpresaSomenteLeituraException>();
        }

        await using var leitura = banco.DaEquipe(tenant);
        (await leitura.Clientes.AnyAsync(c => c.Id == novo.Id)).Should().BeFalse();
        (await leitura.Set<PreferenciaNotificacao>().AnyAsync(p => p.UsuarioId == usuario)).Should().BeFalse("a gravação é uma só");
    }

    [Fact]
    public async Task SaveChanges_EmpresaQueAindaNaoExiste_Grava()
    {
        // Registro: a empresa e o resto entram na mesma gravação.
        var tenant = Guid.NewGuid();

        await using var db = banco.DaEquipe(tenant);
        db.Add(Tenant.Criar(tenant, "Estúdio Novo", Dados.Agora, Guid.NewGuid(), TimeSpan.FromDays(30)));
        db.Add(CalendarioUtil.Padrao(tenant));

        await ((Func<Task>)(() => db.SaveChangesAsync())).Should().NotThrowAsync();
    }
}
