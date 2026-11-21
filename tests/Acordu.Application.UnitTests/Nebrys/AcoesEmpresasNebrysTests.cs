using System.Text.Json;
using System.Text.Json.Serialization;
using Acordu.Application.Auditoria;
using Acordu.Application.Nebrys;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.UnitTests.Nebrys;

/// <summary>
/// Spec do F14, F14-65 a F14-69 e F14-107 (F14l): as ações da Nebrys sobre a situação passam a versão e o instante, e
/// mandam para a trilha a ação com o antes e o depois da situação (design D5 e D14).
/// </summary>
public sealed class AcoesEmpresasNebrysTests
{
    private static readonly DateTimeOffset Cadastro = new(2026, 10, 6, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Agora = new(2026, 11, 20, 15, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    [Fact]
    public async Task Ativar_Vencida_AtivaComOInstanteETrilhaComAntesEDepois() // F14-65, F14-70 (dados)
    {
        var tenant = Vencida();
        var (acoes, situacoes) = Montar(tenant);

        await acoes.AtivarAsync(tenant.Id, "1043");

        tenant.Situacao.Should().Be(SituacaoEmpresa.Ativa);
        tenant.GravavelDesde.Should().Be(Agora);
        situacoes.Empresa.Should().Be(tenant.Id);
        situacoes.Versao.Should().Be("1043");
        situacoes.Trilha!.Acao.Should().Be(AcoesAuditoria.NebrysEmpresaAtivacao);
        var dados = Json(situacoes.Trilha.Dados);
        dados.GetProperty("antes").GetProperty("situacao").GetString().Should().Be("VENCIDA");
        dados.GetProperty("antes").GetProperty("somenteLeituraDesde").GetDateTimeOffset().Should().Be(Cadastro.AddDays(30));
        dados.GetProperty("depois").GetProperty("situacao").GetString().Should().Be("ATIVA");
        dados.GetProperty("depois").GetProperty("fimDoTeste").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Prorrogar_UsaOFusoDaEmpresaEGravaAAcao() // F14-66
    {
        var tenant = Vencida();
        var (acoes, situacoes) = Montar(tenant);

        await acoes.ProrrogarAsync(tenant.Id, new ProrrogacaoTesteRequest(new DateOnly(2026, 12, 15)), null);

        tenant.Situacao.Should().Be(SituacaoEmpresa.Teste);
        tenant.FimDoTeste.Should().Be(new DateTimeOffset(2026, 12, 16, 3, 0, 0, TimeSpan.Zero));
        situacoes.Versao.Should().BeNull("sem If-Match, sem conferência (convenções §5)");
        situacoes.Trilha!.Acao.Should().Be(AcoesAuditoria.NebrysEmpresaProrrogacao);
    }

    [Fact]
    public async Task Prorrogar_SemODia_400NoCampoSemChamarAEmpresa()
    {
        var tenant = Vencida();
        var (acoes, situacoes) = Montar(tenant);

        var acao = () => acoes.ProrrogarAsync(tenant.Id, new ProrrogacaoTesteRequest(null), null);

        (await acao.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be(AcoesEmpresasNebrys.CampoAte);
        situacoes.Chamadas.Should().Be(0);
    }

    [Fact]
    public async Task Cancelar_EReativar_GravamAsAcoesDaTrilha() // F14-67, F14-68
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Cadastro, Guid.NewGuid(), TimeSpan.FromDays(30));
        var (acoes, situacoes) = Montar(tenant);

        await acoes.CancelarAsync(tenant.Id, null);
        tenant.Situacao.Should().Be(SituacaoEmpresa.Cancelada);
        tenant.CanceladaEm.Should().Be(Agora);
        situacoes.Trilha!.Acao.Should().Be(AcoesAuditoria.NebrysEmpresaCancelamento);

        await acoes.ReativarAsync(tenant.Id, null);
        tenant.Situacao.Should().Be(SituacaoEmpresa.Ativa);
        situacoes.Trilha!.Acao.Should().Be(AcoesAuditoria.NebrysEmpresaReativacao);
        Json(situacoes.Trilha.Dados).GetProperty("antes").GetProperty("canceladaEm").GetDateTimeOffset().Should().Be(Agora);
    }

    [Fact]
    public async Task Ativar_JaAtiva_TransicaoInvalidaSemTrilha() // F14-69
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Cadastro, Guid.NewGuid(), TimeSpan.FromDays(30));
        tenant.Ativar(Cadastro.AddDays(1));
        var (acoes, situacoes) = Montar(tenant);

        var acao = () => acoes.AtivarAsync(tenant.Id, null);

        await acao.Should().ThrowAsync<TransicaoInvalidaException>();
        situacoes.Trilha.Should().BeNull("a porta não grava quando a transição lança");
    }

    private static Tenant Vencida()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Cadastro, Guid.NewGuid(), TimeSpan.FromDays(30));
        tenant.Vencer(Cadastro.AddDays(30));
        return tenant;
    }

    private static (AcoesEmpresasNebrys Acoes, SituacoesFalsas Situacoes) Montar(Tenant tenant)
    {
        var situacoes = new SituacoesFalsas(tenant);
        return (new AcoesEmpresasNebrys(situacoes, new RelogioFixo(Agora)), situacoes);
    }

    /// <summary>Como a trilha serializa (camelCase e enums em MAIÚSCULAS).</summary>
    private static readonly JsonSerializerOptions ComoATrilha = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    private static JsonElement Json(object dados) => JsonSerializer.SerializeToElement(dados, ComoATrilha);

    /// <summary>A porta como a Infrastructure a usa: aplica a alteração na empresa e guarda o que iria para a trilha.</summary>
    private sealed class SituacoesFalsas(Tenant tenant) : ISituacaoDasEmpresas
    {
        public int Chamadas { get; private set; }

        public Guid? Empresa { get; private set; }

        public string? Versao { get; private set; }

        public AcaoNaTrilha? Trilha { get; private set; }

        public Task AlterarAsync(
            Guid empresaId, string? versaoEsperada, Func<Tenant, TimeZoneInfo, AcaoNaTrilha> alterar, CancellationToken cancellationToken = default)
        {
            Chamadas++;
            Empresa = empresaId;
            Versao = versaoEsperada;
            Trilha = null;
            Trilha = alterar(tenant, SaoPaulo);
            return Task.CompletedTask;
        }
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
