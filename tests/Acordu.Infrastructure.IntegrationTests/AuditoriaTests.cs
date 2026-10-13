using Acordu.Application.Abstractions;
using Acordu.Infrastructure.Persistence.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>Trilha de auditoria: gravação na unidade de trabalho, filtros por tenant e expurgo de 12 meses.</summary>
[Collection(ColecaoBanco.Nome)]
public sealed class AuditoriaTests(BancoFixture banco)
{
    private readonly Guid _tenant = Guid.NewGuid();

    private async Task<Guid> RegistrarAsync(Servicos servicos, bool salvar = true)
    {
        var alvo = Guid.NewGuid();
        await using var escopo = servicos.DaEquipe(_tenant);
        escopo.ServiceProvider.GetRequiredService<IAuditoria>().Registrar("contrato.criacao", "contrato", alvo, new { valor = 10 });
        if (salvar)
            await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAsync();
        return alvo;
    }

    [Fact]
    public async Task Registrar_GravaNoSalvar_ComTenantDadosEHora()
    {
        await using var servicos = new Servicos(banco);

        var alvo = await RegistrarAsync(servicos);
        var naoSalvo = await RegistrarAsync(servicos, salvar: false);

        await using var db = banco.DaEquipe(_tenant);
        var registro = await db.Set<RegistroAuditoria>().SingleAsync(r => r.AlvoId == alvo);
        registro.TenantId.Should().Be(_tenant);
        registro.Acao.Should().Be("contrato.criacao");
        registro.Dados.Should().Contain("\"valor\"").And.Contain("10");
        registro.OcorridoEm.Should().Be(servicos.Relogio.Agora);
        registro.Ip.Should().BeNull();
        (await db.Set<RegistroAuditoria>().AnyAsync(r => r.AlvoId == naoSalvo)).Should().BeFalse();
    }

    [Fact]
    public async Task Filtros_OutroTenantENuloNaoAparecem()
    {
        await using var servicos = new Servicos(banco);
        var alvo = await RegistrarAsync(servicos);
        var semTenant = Guid.NewGuid();
        await using (var sistema = banco.Contexto(Quem.Sistema()))
        {
            sistema.Add(RegistroAuditoria.Criar(null, null, "login.falha", null, semTenant, null, null, servicos.Relogio.Agora));
            await sistema.SaveChangesAsync();
        }

        await using var outro = banco.DaEquipe(Guid.NewGuid());
        await using var dono = banco.DaEquipe(_tenant);
        (await outro.Set<RegistroAuditoria>().AnyAsync(r => r.AlvoId == alvo)).Should().BeFalse();
        (await dono.Set<RegistroAuditoria>().AnyAsync(r => r.AlvoId == semTenant)).Should().BeFalse();
        (await dono.Set<RegistroAuditoria>().AnyAsync(r => r.AlvoId == alvo)).Should().BeTrue();
    }

    [Fact]
    public async Task Expurgo_RemoveOsComMaisDeDozeMeses_InclusiveSemTenant()
    {
        await using var servicos = new Servicos(banco);
        var hoje = servicos.Relogio.Agora;
        servicos.Relogio.Agora = hoje.AddMonths(-13);
        var antigo = await RegistrarAsync(servicos);
        var antigoSemTenant = Guid.NewGuid();
        await using (var sistema = banco.Contexto(Quem.Sistema()))
        {
            sistema.Add(RegistroAuditoria.Criar(null, null, "login.falha", null, antigoSemTenant, null, null, servicos.Relogio.Agora));
            await sistema.SaveChangesAsync();
        }

        servicos.Relogio.Agora = hoje.AddMonths(-11);
        var recente = await RegistrarAsync(servicos);
        servicos.Relogio.Agora = hoje;

        var removidos = await servicos.Raiz.GetRequiredService<ExpurgoAuditoria>().ExecutarAsync();

        removidos.Should().BeGreaterThanOrEqualTo(2);
        await using var db = banco.Contexto(Quem.Sistema());
        var restantes = await db.Set<RegistroAuditoria>().IgnoreQueryFilters()
            .Where(r => r.AlvoId == antigo || r.AlvoId == antigoSemTenant || r.AlvoId == recente)
            .Select(r => r.AlvoId)
            .ToListAsync();
        restantes.Should().Equal(recente);
    }
}
