using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>Tenant, papéis e vínculos (segurança §2): persistência e os dois isolamentos.</summary>
[Collection(ColecaoBanco.Nome)]
public sealed class IdentidadeTests(BancoFixture banco)
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _cliente = Guid.NewGuid();

    /// <summary>O que o registro grava: tenant, papéis padrão, o Dono e, aqui, também um contato do cliente.</summary>
    private async Task<(IReadOnlyList<Papel> Papeis, Vinculo Dono, Vinculo Contato)> SemearAsync(Guid tenant, Guid cliente)
    {
        var papeis = Papel.Padroes(tenant);
        var dono = Vinculo.Criar(Guid.NewGuid(), Guid.NewGuid(), papeis[0]);
        var contato = Vinculo.Criar(Guid.NewGuid(), Guid.NewGuid(), papeis[3], cliente, EscopoPortal.Proprias);

        await using var db = banco.DaEquipe(tenant);
        db.Add(Tenant.Criar(tenant, "Estúdio Traço", Dados.Agora));
        db.AddRange(papeis);
        db.AddRange(dono, contato);
        await db.SaveChangesAsync();
        return (papeis, dono, contato);
    }

    [Fact]
    public async Task Persistencia_PermissoesEmArrayEEnumsEmScreamingSnake()
    {
        var (papeis, _, contato) = await SemearAsync(_tenant, _cliente);

        await using var db = banco.DaEquipe(_tenant);
        var tenant = await db.Tenants.SingleAsync();
        var membro = await db.Papeis.SingleAsync(p => p.Id == papeis[1].Id);
        var lido = await db.Vinculos.SingleAsync(v => v.Id == contato.Id);
        var linha = await db.Database
            .SqlQuery<LinhaTexto>($"SELECT p.tipo, v.escopo_portal FROM vinculos v JOIN papeis p ON p.id = v.papel_id WHERE v.id = {contato.Id}")
            .SingleAsync();

        tenant.Nome.Should().Be("Estúdio Traço");
        tenant.CriadoEm.Should().Be(Dados.Agora);
        membro.Permissoes.Should().Equal(papeis[1].Permissoes);
        membro.Padrao.Should().BeTrue();
        lido.Should().BeEquivalentTo(contato, o => o.Excluding(v => v.Eventos));
        linha.Tipo.Should().Be("CLIENTE");
        linha.EscopoPortal.Should().Be("PROPRIAS");
    }

    [Fact]
    public async Task OutroTenant_NaoVeTenantPapeisNemVinculos()
    {
        await SemearAsync(_tenant, _cliente);
        var outro = Guid.NewGuid();
        await SemearAsync(outro, Guid.NewGuid());

        await using var db = banco.DaEquipe(outro);

        (await db.Tenants.Select(t => t.Id).ToListAsync()).Should().Equal(outro);
        (await db.Papeis.ToListAsync()).Should().HaveCount(4).And.OnlyContain(p => p.TenantId == outro);
        (await db.Vinculos.ToListAsync()).Should().HaveCount(2).And.OnlyContain(v => v.TenantId == outro);
    }

    [Fact]
    public async Task Contato_SoVeVinculosDoProprioClienteEOsPapeis()
    {
        var (_, _, contato) = await SemearAsync(_tenant, _cliente);

        await using var db = banco.DoCliente(_tenant, _cliente);

        (await db.Vinculos.Select(v => v.Id).ToListAsync()).Should().Equal(contato.Id);
        (await db.Papeis.CountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task GravarTenant_DeOutroContexto_Recusa()
    {
        await using var db = banco.DaEquipe(_tenant);
        db.Add(Tenant.Criar(Guid.NewGuid(), "Intruso", Dados.Agora));

        var acao = () => db.SaveChangesAsync();

        await acao.Should().ThrowAsync<IsolamentoVioladoException>();
    }

    [Fact]
    public async Task GravarTenant_PeloContato_Recusa()
    {
        await SemearAsync(_tenant, _cliente);
        await using var db = banco.DoCliente(_tenant, _cliente);
        var tenant = await db.Tenants.SingleAsync();
        tenant.Renomear("Outro nome");

        var acao = () => db.SaveChangesAsync();

        await acao.Should().ThrowAsync<IsolamentoVioladoException>();
    }

    [Fact]
    public async Task SegundoVinculoDoMesmoUsuarioNoTenant_Recusa()
    {
        var (papeis, dono, _) = await SemearAsync(_tenant, _cliente);
        await using var db = banco.DaEquipe(_tenant);
        db.Add(Vinculo.Criar(Guid.NewGuid(), dono.UsuarioId, papeis[1]));

        var acao = () => db.SaveChangesAsync();

        await acao.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task PapelComNomeRepetidoNoTenant_Recusa()
    {
        await SemearAsync(_tenant, _cliente);
        await using var db = banco.DaEquipe(_tenant);
        db.Add(Papel.Criar(Guid.NewGuid(), _tenant, Papel.NomeMembro, TipoPapel.Interno, [Permissoes.ClienteVer]));

        var acao = () => db.SaveChangesAsync();

        await acao.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task MesmoUsuario_MembroNumTenantEClienteNoutro()
    {
        var usuario = Guid.NewGuid();
        var outro = Guid.NewGuid();
        var (papeisA, _, _) = await SemearAsync(_tenant, _cliente);
        var (papeisB, _, _) = await SemearAsync(outro, Guid.NewGuid());

        await using (var db = banco.DaEquipe(_tenant))
        {
            db.Add(Vinculo.Criar(Guid.NewGuid(), usuario, papeisA[1]));
            await db.SaveChangesAsync();
        }

        await using (var db = banco.DaEquipe(outro))
        {
            db.Add(Vinculo.Criar(Guid.NewGuid(), usuario, papeisB[3], Guid.NewGuid(), EscopoPortal.TodasDoCliente));
            await db.SaveChangesAsync();
        }

        await using var doTenant = banco.DaEquipe(_tenant);
        (await doTenant.Vinculos.SingleAsync(v => v.UsuarioId == usuario)).PapelId.Should().Be(papeisA[1].Id);
    }

    private sealed class LinhaTexto
    {
        public string Tipo { get; set; } = "";

        public string EscopoPortal { get; set; } = "";
    }
}
