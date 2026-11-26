using Acordu.Application.EntradaEmail;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Infrastructure.Persistence.EntradaEmail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>
/// Spec do F15, F15-108 (design D6 e D16; ADR 0016, item 11; ADR 0015, itens 3 e 4): o passo <c>expurgo-email-entrada</c>
/// apaga o registro de entrada com mais de <c>EmailEntrada__DiasRegistro</c> dias, em todas as empresas de uma vez,
/// inclusive as só leitura; o mais novo fica.
/// </summary>
[Collection(ColecaoBanco.Nome)]
public sealed class ExpurgoEmailEntradaTests(BancoFixture banco)
{
    private static ExpurgoEmailEntrada Expurgo(Servicos servicos, int dias = OpcoesEmailEntrada.DiasRegistroPadrao) =>
        new(servicos.Raiz.GetRequiredService<IServiceScopeFactory>(), servicos.Relogio, new OpcoesEmailEntrada(diasRegistro: dias));

    /// <summary>Empresa com um registro recebido em cada instante, gravados antes de a situação mudar por SQL.</summary>
    private async Task<(Guid Tenant, Guid[] Registros)> EmpresaComRegistrosAsync(string? situacao, params DateTimeOffset[] recebidos)
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio Traço", Dados.Agora, Guid.NewGuid(), TimeSpan.FromDays(30));
        tenant.LigarRecebimentoEmail();
        var registros = recebidos
            .Select(quando => EmailRecebido.Registrar(
                Guid.NewGuid(), tenant.Id, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray())),
                null, ResultadoEntradaEmail.Ignorado, quando))
            .ToList();
        await using (var criacao = banco.DaEquipe(tenant.Id))
        {
            criacao.Add(tenant);
            await criacao.SaveChangesAsync();
        }

        await using var db = banco.DaEquipe(tenant.Id);
        db.AddRange(registros);
        await db.SaveChangesAsync();
        if (situacao is not null)
        {
            var desde = Dados.Agora.ToUniversalTime();
            await db.Database.ExecuteSqlAsync(
                $"UPDATE tenants SET situacao = {situacao}, somente_leitura_desde = {desde} WHERE id = {tenant.Id}");
        }

        return (tenant.Id, registros.Select(r => r.Id).ToArray());
    }

    private async Task<Guid[]> RestantesAsync(Guid tenantId)
    {
        await using var db = banco.DaEquipe(tenantId);
        return await db.EmailsRecebidos.AsNoTracking().Select(e => e.Id).ToArrayAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task ExecutarAsync_RegistroComMaisDe7Dias_ApagaInclusiveNaSoLeitura(string? situacao) // F15-108
    {
        await using var servicos = new Servicos(banco);
        var agora = servicos.Relogio.Agora;
        var (tenant, registros) = await EmpresaComRegistrosAsync(
            situacao, agora.AddDays(-8), agora.AddDays(-7).AddMinutes(-1), agora.AddDays(-7).AddMinutes(1), agora.AddDays(-1));

        var removidos = await Expurgo(servicos).ExecutarAsync();

        removidos.Should().BeGreaterThanOrEqualTo(2, "a coleção compartilha o banco, mas os dois vencidos entram na conta");
        (await RestantesAsync(tenant)).Should().BeEquivalentTo(registros[2..], "o que tem até 7 dias fica");
    }

    [Fact]
    public async Task ExecutarAsync_DuasEmpresas_ApagaNasDuasDeUmaVez() // D16: fora do ExecutorPorTenant, todos os tenants
    {
        await using var servicos = new Servicos(banco);
        var antigo = servicos.Relogio.Agora.AddDays(-10);
        var (a, _) = await EmpresaComRegistrosAsync(null, antigo);
        var (b, _) = await EmpresaComRegistrosAsync("VENCIDA", antigo);

        await Expurgo(servicos).ExecutarAsync();

        (await RestantesAsync(a)).Should().BeEmpty();
        (await RestantesAsync(b)).Should().BeEmpty();
    }

    [Fact]
    public async Task ExecutarAsync_RetencaoConfigurada_UsaOsDiasDaConfiguracao() // EmailEntrada__DiasRegistro
    {
        await using var servicos = new Servicos(banco);
        var agora = servicos.Relogio.Agora;
        var (tenant, registros) = await EmpresaComRegistrosAsync(null, agora.AddDays(-3), agora.AddDays(-1));

        await Expurgo(servicos, dias: 2).ExecutarAsync();

        (await RestantesAsync(tenant)).Should().Equal(registros[1]);
    }
}
