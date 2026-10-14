using Acordu.Application.Abstractions;
using Acordu.Application.Notificacoes;
using Acordu.Infrastructure.Persistence.Notificacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>Sino: registro idempotente por chave, filtro por tenant e expurgo de 90 dias.</summary>
[Collection(ColecaoBanco.Nome)]
public sealed class NotificacoesTests(BancoFixture banco)
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _usuario = Guid.NewGuid();

    private async Task RegistrarAsync(Servicos servicos, string chave)
    {
        await using var escopo = servicos.DaEquipe(_tenant);
        await escopo.ServiceProvider.GetRequiredService<INotificacoes>()
            .RegistrarAsync(_usuario, CategoriaAviso.Sla, chave, "SLA em risco: solicitação #1", new Uri("https://app.teste/solicitacoes/1"));
    }

    [Fact]
    public async Task Registrar_MesmaChave_UmaSo_OutroTenantNaoVe()
    {
        await using var servicos = new Servicos(banco);

        await RegistrarAsync(servicos, "sla:1");
        await RegistrarAsync(servicos, "sla:1");

        await using var db = banco.DaEquipe(_tenant);
        var linhas = await db.Set<Notificacao>().Where(n => n.UsuarioId == _usuario).ToListAsync();
        linhas.Should().HaveCount(1);
        linhas[0].Categoria.Should().Be("SLA");
        await using var outro = banco.DaEquipe(Guid.NewGuid());
        (await outro.Set<Notificacao>().AnyAsync(n => n.UsuarioId == _usuario)).Should().BeFalse();
    }

    [Fact]
    public async Task Expurgo_RemoveAsComMaisDe90Dias()
    {
        await using var servicos = new Servicos(banco);
        var hoje = servicos.Relogio.Agora;
        servicos.Relogio.Agora = hoje.AddDays(-91);
        await RegistrarAsync(servicos, "antiga");
        servicos.Relogio.Agora = hoje.AddDays(-89);
        await RegistrarAsync(servicos, "recente");
        servicos.Relogio.Agora = hoje;

        await servicos.Raiz.GetRequiredService<ExpurgoNotificacoes>().ExecutarAsync();

        await using var db = banco.DaEquipe(_tenant);
        (await db.Set<Notificacao>().Where(n => n.UsuarioId == _usuario).Select(n => n.Chave).ToListAsync())
            .Should().Equal("recente");
    }
}
