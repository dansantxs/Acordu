using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using Acordu.Infrastructure.Persistence;
using Acordu.Infrastructure.Persistence.Consultas;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>Consumo de contrato (read side §3) e casos de borda do §11.</summary>
[Collection(ColecaoBanco.Nome)]
public sealed class ConsultasContratoTests(BancoFixture banco)
{
    private static readonly MesReferencia Outubro = new(2026, 10);
    private static readonly MesReferencia Novembro = new(2026, 11);
    private static readonly DateOnly DepoisDeOutubro = new(2026, 11, 10);

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _cliente = Guid.NewGuid();

    private static ConsultasContrato Consultas(AcorduDbContext db, Quem quem) => new(db, new ConsultaSql(db, quem));

    private async Task<Contrato> ContratoAsync(TipoContrato tipo, Duracao? franquia, params (int Mes, int Dia, int Minutos, bool Faturavel)[] apontamentos)
    {
        var contrato = Contrato.Criar(
            Guid.NewGuid(), _tenant, _cliente, tipo, new Periodo(new DateOnly(2026, 10, 1)), franquia, Dados.Politica());
        var tarefa = Dados.Tarefa(_tenant, _cliente);
        await using var db = banco.DaEquipe(_tenant);
        db.AddRange(contrato, tarefa);
        foreach (var (mes, dia, minutos, faturavel) in apontamentos)
        {
            db.Add(Apontamento.Registrar(
                Guid.NewGuid(), _tenant, tarefa.Id, _cliente, Guid.NewGuid(), contrato.Id, new DateOnly(2026, mes, dia),
                Duracao.FromMinutos(minutos), "Trabalho", null, faturavel, null, []));
        }

        await db.SaveChangesAsync();
        return contrato;
    }

    private async Task AjustarAsync(Contrato contrato, int minutos, int mes, int dia)
    {
        await using var db = banco.DaEquipe(_tenant);
        db.Add(AjusteSaldo.Registrar(
            Guid.NewGuid(), contrato, new DuracaoAssinada(minutos), "Ajuste", new DateOnly(2026, mes, dia), Guid.NewGuid()));
        await db.SaveChangesAsync();
    }

    private async Task<Application.Contratos.ConsumoContratoDto?> ConsumoAsync(Guid contratoId, MesReferencia periodo, DateOnly hoje)
    {
        await using var db = banco.DaEquipe(_tenant);
        return await Consultas(db, Quem.Equipe(_tenant)).ConsumoAsync(contratoId, periodo, hoje);
    }

    [Fact]
    public async Task BancoDeHoras_SemApontamentos_SaldoEhAFranquia()
    {
        var contrato = await ContratoAsync(TipoContrato.BancoHoras, Duracao.FromHoras(20));

        var consumo = await ConsumoAsync(contrato.Id, Outubro, DepoisDeOutubro);

        consumo!.ConsumidoMinutos.Should().Be(0);
        consumo.SaldoMinutos.Should().Be(1200);
        consumo.Percentual.Should().Be(0);
    }

    [Fact]
    public async Task BancoDeHoras_RecargaNoMeioDoMes_SaldoAcumulado()
    {
        var contrato = await ContratoAsync(
            TipoContrato.BancoHoras,
            Duracao.FromHoras(20),
            (10, 5, 600, true),
            (10, 10, 120, false),
            (10, 20, 900, true),
            (11, 3, 300, true));
        await AjustarAsync(contrato, 600, 10, 15);

        var outubro = await ConsumoAsync(contrato.Id, Outubro, DepoisDeOutubro);
        var novembro = await ConsumoAsync(contrato.Id, Novembro, DepoisDeOutubro);
        var antesDaRecarga = await ConsumoAsync(contrato.Id, Outubro, new DateOnly(2026, 10, 12));

        outubro!.ConsumidoMinutos.Should().Be(1500);
        outubro.NaoFaturavelMinutos.Should().Be(120);
        outubro.ConsumidoAcumuladoMinutos.Should().Be(1500);
        outubro.AjustesMinutos.Should().Be(600);
        outubro.SaldoMinutos.Should().Be(300);
        outubro.Percentual.Should().Be(83);

        novembro!.ConsumidoMinutos.Should().Be(300);
        novembro.ConsumidoAcumuladoMinutos.Should().Be(1800);
        novembro.SaldoMinutos.Should().Be(0);
        novembro.Percentual.Should().Be(100);

        antesDaRecarga!.ConsumidoAcumuladoMinutos.Should().Be(600);
        antesDaRecarga.AjustesMinutos.Should().Be(0);
        antesDaRecarga.SaldoMinutos.Should().Be(600);
    }

    [Fact]
    public async Task MensalFixo_Excedente_SaldoNegativoEPercentualAcimaDeCem()
    {
        var contrato = await ContratoAsync(
            TipoContrato.MensalFixo, Duracao.FromHoras(10), (10, 5, 400, true), (10, 25, 500, true), (11, 2, 60, true));

        var consumo = await ConsumoAsync(contrato.Id, Outubro, DepoisDeOutubro);

        consumo!.ConsumidoMinutos.Should().Be(900);
        consumo.SaldoMinutos.Should().Be(-300);
        consumo.Percentual.Should().Be(150);
        consumo.ConsumidoAcumuladoMinutos.Should().BeNull();
    }

    [Fact]
    public async Task MensalFixo_FranquiaNaoAcumulaEntreMeses()
    {
        var contrato = await ContratoAsync(TipoContrato.MensalFixo, Duracao.FromHoras(10), (10, 5, 60, true), (11, 5, 120, true));

        var novembro = await ConsumoAsync(contrato.Id, Novembro, new DateOnly(2026, 11, 30));

        novembro!.SaldoMinutos.Should().Be(480);
    }

    [Fact]
    public async Task MensalFixoSemFranquia_SemSaldoNemPercentual()
    {
        var contrato = await ContratoAsync(TipoContrato.MensalFixo, null, (10, 5, 60, true));

        var consumo = await ConsumoAsync(contrato.Id, Outubro, DepoisDeOutubro);

        consumo!.ConsumidoMinutos.Should().Be(60);
        consumo.SaldoMinutos.Should().BeNull();
        consumo.Percentual.Should().BeNull();
    }

    [Fact]
    public async Task Avulso_SoConsumidoENaoFaturavel()
    {
        var contrato = await ContratoAsync(TipoContrato.Avulso, null, (10, 5, 60, true), (10, 6, 30, false));

        var consumo = await ConsumoAsync(contrato.Id, Outubro, DepoisDeOutubro);

        consumo!.ConsumidoMinutos.Should().Be(60);
        consumo.NaoFaturavelMinutos.Should().Be(30);
        consumo.SaldoMinutos.Should().BeNull();
        consumo.FranquiaMinutos.Should().BeNull();
    }

    [Fact]
    public async Task Contato_ProprioContrato_VeConsumo()
    {
        var contrato = await ContratoAsync(TipoContrato.BancoHoras, Duracao.FromHoras(20), (10, 5, 600, true));

        await using var db = banco.DoCliente(_tenant, _cliente);
        var consumo = await Consultas(db, Quem.DoCliente(_tenant, _cliente)).ConsumoAsync(contrato.Id, Outubro, DepoisDeOutubro);

        consumo!.SaldoMinutos.Should().Be(600);
    }

    [Fact]
    public async Task ContatoDeOutroCliente_Nulo()
    {
        var contrato = await ContratoAsync(TipoContrato.BancoHoras, Duracao.FromHoras(20), (10, 5, 600, true));
        var outroCliente = Guid.NewGuid();

        await using var db = banco.DoCliente(_tenant, outroCliente);
        var consumo = await Consultas(db, Quem.DoCliente(_tenant, outroCliente)).ConsumoAsync(contrato.Id, Outubro, DepoisDeOutubro);

        consumo.Should().BeNull();
    }

    [Fact]
    public async Task OutroTenant_Nulo()
    {
        var contrato = await ContratoAsync(TipoContrato.BancoHoras, Duracao.FromHoras(20));
        var outroTenant = Guid.NewGuid();

        await using var db = banco.DaEquipe(outroTenant);
        (await Consultas(db, Quem.Equipe(outroTenant)).ConsumoAsync(contrato.Id, Outubro, DepoisDeOutubro)).Should().BeNull();
    }
}
