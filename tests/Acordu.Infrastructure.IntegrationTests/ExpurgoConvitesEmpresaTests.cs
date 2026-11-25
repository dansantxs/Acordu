using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.ConvitesEmpresa;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>
/// #213: o diário apaga o convite de empresa 90 dias depois de usado, revogado ou vencido; o pendente dentro da validade e
/// os encerrados há menos de 90 dias ficam, e a entrada da lista de espera de origem não muda.
/// </summary>
[Collection(ColecaoBanco.Nome)]
public sealed class ExpurgoConvitesEmpresaTests(BancoFixture banco)
{
    private static readonly TimeSpan Validade = TimeSpan.FromDays(7);

    [Theory]
    [InlineData("usado")]
    [InlineData("revogado")]
    [InlineData("vencido")]
    public async Task ExecutarAsync_EncerradoHaMaisDe90Dias_Apaga(string encerramento)
    {
        await using var servicos = new Servicos(banco);
        var id = await GravarAsync(Encerrado(encerramento, servicos.Relogio.Agora.AddDays(-91)));

        var removidos = await servicos.Raiz.GetRequiredService<ExpurgoConvitesEmpresa>().ExecutarAsync();

        removidos.Should().BeGreaterThanOrEqualTo(1, "a coleção compartilha o banco, mas este convite entra na conta");
        (await ExisteAsync(id)).Should().BeFalse();
    }

    [Theory]
    [InlineData("usado")]
    [InlineData("revogado")]
    [InlineData("vencido")]
    public async Task ExecutarAsync_EncerradoHaMenosDe90Dias_Fica(string encerramento)
    {
        await using var servicos = new Servicos(banco);
        var id = await GravarAsync(Encerrado(encerramento, servicos.Relogio.Agora.AddDays(-89)));

        await servicos.Raiz.GetRequiredService<ExpurgoConvitesEmpresa>().ExecutarAsync();

        (await ExisteAsync(id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExecutarAsync_PendenteValidoEmitidoHaMuitoTempo_Fica()
    {
        await using var servicos = new Servicos(banco);
        var hoje = servicos.Relogio.Agora;
        // Emitido há 200 dias (venceu há muito) e reenviado ontem: vale de novo, então não está encerrado.
        var (convite, _) = ConviteEmpresa.Emitir(Guid.NewGuid(), Email(), Validade, hoje.AddDays(-200));
        convite.Reenviar(Validade, hoje.AddDays(-1));
        var id = await GravarAsync(convite);

        await servicos.Raiz.GetRequiredService<ExpurgoConvitesEmpresa>().ExecutarAsync();

        (await ExisteAsync(id)).Should().BeTrue("convite pendente e ainda válido nunca sai");
    }

    [Theory]
    [InlineData(89, true)]
    [InlineData(91, false)]
    public async Task ExecutarAsync_VencidoReenviado_ContaDoVencimentoNovo(int diasDoVencimentoNovo, bool fica)
    {
        await using var servicos = new Servicos(banco);
        var hoje = servicos.Relogio.Agora;
        // Venceu há 200 dias e foi reenviado: a validade nova termina há 89 ou 91 dias; só a segunda passa da retenção.
        var (convite, _) = ConviteEmpresa.Emitir(Guid.NewGuid(), Email(), Validade, hoje.AddDays(-200));
        convite.Reenviar(Validade, hoje.AddDays(-diasDoVencimentoNovo) - Validade);
        var id = await GravarAsync(convite);

        await servicos.Raiz.GetRequiredService<ExpurgoConvitesEmpresa>().ExecutarAsync();

        (await ExisteAsync(id)).Should().Be(fica);
    }

    [Fact]
    public async Task ExecutarAsync_RevogadoOntemQueJaEstavaVencidoHaMuito_Fica()
    {
        await using var servicos = new Servicos(banco);
        var hoje = servicos.Relogio.Agora;
        // A revogação é o encerramento: a validade antiga (vencida há 200 dias) não conta quando há revogação recente.
        var (convite, _) = ConviteEmpresa.Emitir(Guid.NewGuid(), Email(), Validade, hoje.AddDays(-207));
        convite.Revogar(hoje.AddDays(-1));
        var id = await GravarAsync(convite);

        await servicos.Raiz.GetRequiredService<ExpurgoConvitesEmpresa>().ExecutarAsync();

        (await ExisteAsync(id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExecutarAsync_ConviteDeUmaEntradaDaListaDeEspera_ApagaOConviteEMantemAEntrada()
    {
        await using var servicos = new Servicos(banco);
        var quando = servicos.Relogio.Agora.AddDays(-91);
        var entrada = EntradaListaEspera.Registrar(Guid.NewGuid(), "Marina Souza", Email(), "Bom Grão", null, quando.AddDays(-5));
        entrada.Convidar(quando.AddDays(-1));
        await using (var db = banco.Contexto(Quem.Sistema()))
        {
            db.Add(entrada);
            await db.SaveChangesAsync();
        }

        var (convite, token) = ConviteEmpresa.Emitir(Guid.NewGuid(), entrada.Email, Validade, quando.AddDays(-1), entrada.Id);
        convite.Usar(token, convite.Email, quando);
        var id = await GravarAsync(convite);

        await servicos.Raiz.GetRequiredService<ExpurgoConvitesEmpresa>().ExecutarAsync();

        (await ExisteAsync(id)).Should().BeFalse();
        await using var verificacao = banco.Contexto(Quem.Sistema());
        var guardada = await verificacao.Set<EntradaListaEspera>().AsNoTracking().SingleOrDefaultAsync(e => e.Id == entrada.Id);
        guardada.Should().NotBeNull("a entrada convidada fica até o descarte");
        guardada!.ConvidadaEm.Should().Be(entrada.ConvidadaEm);
    }

    /// <summary>Convite encerrado no instante <paramref name="quando"/>: usado, revogado ou com a validade terminando nele.</summary>
    private static ConviteEmpresa Encerrado(string encerramento, DateTimeOffset quando)
    {
        switch (encerramento)
        {
            case "usado":
            {
                var (convite, token) = ConviteEmpresa.Emitir(Guid.NewGuid(), Email(), Validade, quando.AddDays(-1));
                convite.Usar(token, convite.Email, quando);
                return convite;
            }

            case "revogado":
            {
                var (convite, _) = ConviteEmpresa.Emitir(Guid.NewGuid(), Email(), Validade, quando.AddDays(-1));
                convite.Revogar(quando);
                return convite;
            }

            default:
                return ConviteEmpresa.Emitir(Guid.NewGuid(), Email(), Validade, quando - Validade).Convite;
        }
    }

    private static string Email() => $"convite-{Guid.NewGuid():N}@exemplo.com.br";

    private async Task<Guid> GravarAsync(ConviteEmpresa convite)
    {
        await using var db = banco.Contexto(Quem.Sistema());
        db.Add(convite);
        await db.SaveChangesAsync();
        return convite.Id;
    }

    private async Task<bool> ExisteAsync(Guid id)
    {
        await using var db = banco.Contexto(Quem.Sistema());
        return await db.Set<ConviteEmpresa>().AnyAsync(c => c.Id == id);
    }
}
