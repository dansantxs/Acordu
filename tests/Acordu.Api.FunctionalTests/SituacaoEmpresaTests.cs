using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Application.Empresas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14a (F14-01 a F14-09; design D1 e D7): a empresa nasce em TESTE, o <c>GET /auth/eu</c> traz a situação e
/// as datas, e o passo <c>vencimento-testes</c> do diário vence a que passou do fim do teste, uma vez, só ela.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class SituacaoEmpresaTests(ApiFixture api)
{
    private static readonly TimeSpan DuracaoDoTeste = TimeSpan.FromDays(30);
    private static readonly TimeSpan PrazoAteExpurgo = TimeSpan.FromDays(30);

    [Fact]
    public async Task Registro_EmpresaNasceEmTesteComFimNoCadastroMaisTrintaDias() // F14-01
    {
        var cadastro = api.Relogio.Agora;

        var equipe = await Equipe.CriarAsync(api);

        await using var db = api.DaEquipe(equipe.TenantId);
        var tenant = await db.Tenants.AsNoTracking().SingleAsync();
        tenant.Situacao.Should().Be(SituacaoEmpresa.Teste);
        tenant.FimDoTeste.Should().Be(cadastro + DuracaoDoTeste);
        tenant.SomenteLeituraDesde.Should().BeNull();
        tenant.CanceladaEm.Should().BeNull();
    }

    [Fact]
    public async Task Eu_EmpresaEmTeste_TrazSituacaoEFimDoTesteParaQualquerPessoa() // F14-02
    {
        var cadastro = api.Relogio.Agora;
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);

        foreach (var token in new[] { equipe.Dono, membro })
        {
            var tenant = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: token))
                .GetProperty("tenant");

            tenant.GetProperty("situacao").GetString().Should().Be("TESTE");
            tenant.GetProperty("fimDoTeste").GetDateTimeOffset().Should().Be(cadastro + DuracaoDoTeste);
            tenant.GetProperty("somenteLeituraDesde").ValueKind.Should().Be(JsonValueKind.Null);
            tenant.GetProperty("expurgoPrevistoEm").ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    [Fact]
    public async Task Diario_FimDoTestePassado_VenceGravaADataEConta() // F14-03
    {
        var equipe = await Equipe.CriarAsync(api);
        var adiante = DuracaoDoTeste + TimeSpan.FromHours(1);
        var vencimento = api.Relogio.Agora + adiante;

        var resultado = await DiarioAsync(adiante);

        // ">= 1": a coleção compartilha o banco, e as empresas de outros testes também vencem.
        resultado.GetProperty("empresasVencidas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        resultado.GetProperty("falhas").GetInt32().Should().Be(0);
        await using (var db = api.DaEquipe(equipe.TenantId))
        {
            var tenant = await db.Tenants.AsNoTracking().SingleAsync();
            tenant.Situacao.Should().Be(SituacaoEmpresa.Vencida);
            tenant.SomenteLeituraDesde.Should().Be(vencimento);
        }

        var eu = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("tenant");
        eu.GetProperty("situacao").GetString().Should().Be("VENCIDA");
        eu.GetProperty("somenteLeituraDesde").GetDateTimeOffset().Should().Be(vencimento);
        eu.GetProperty("expurgoPrevistoEm").GetDateTimeOffset().Should().Be(vencimento + PrazoAteExpurgo);
    }

    [Fact]
    public async Task Diario_FimDoTesteAindaPorVir_ContinuaEmTeste() // F14-04
    {
        var cadastro = api.Relogio.Agora;
        var equipe = await Equipe.CriarAsync(api);

        await DiarioAsync(DuracaoDoTeste - TimeSpan.FromMinutes(1));

        await using var db = api.DaEquipe(equipe.TenantId);
        var tenant = await db.Tenants.AsNoTracking().SingleAsync();
        tenant.Situacao.Should().Be(SituacaoEmpresa.Teste);
        tenant.FimDoTeste.Should().Be(cadastro + DuracaoDoTeste);
        tenant.SomenteLeituraDesde.Should().BeNull();
    }

    [Fact]
    public async Task Diario_RodadoDeNovo_NaoMudaANemContaOutraVez() // F14-05
    {
        var equipe = await Equipe.CriarAsync(api);
        var adiante = DuracaoDoTeste + TimeSpan.FromDays(1);
        (await DiarioAsync(adiante)).GetProperty("empresasVencidas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        var antes = await EstadoAsync(equipe.TenantId);
        antes.Situacao.Should().Be(SituacaoEmpresa.Vencida);

        var segundo = await DiarioAsync(adiante + TimeSpan.FromHours(2));

        segundo.GetProperty("empresasVencidas").GetInt32().Should().Be(0);
        segundo.GetProperty("falhas").GetInt32().Should().Be(0);
        (await EstadoAsync(equipe.TenantId)).Should().Be(antes, "a empresa já vencida não é gravada de novo");
    }

    [Theory]
    [InlineData("ATIVA")]
    [InlineData("CANCELADA")]
    public async Task Diario_EmpresaForaDoTeste_NuncaVence(string situacao) // F14-06
    {
        var equipe = await Equipe.CriarAsync(api);
        // A ativação e o cancelamento pela área da Nebrys são do F14l; aqui o estado é gravado direto. A empresa de
        // demonstração é ATIVA (design D1, a1).
        await DefinirSituacaoAsync(equipe.TenantId, situacao);

        await DiarioAsync(TimeSpan.FromDays(400));

        (await EstadoAsync(equipe.TenantId)).Situacao.Should().Be(Enum.Parse<SituacaoEmpresa>(situacao, ignoreCase: true));
    }

    [Fact]
    public async Task Convidar_EmpresaEmTesteComTrintaPessoas_ConviteSai() // F14-07
    {
        var equipe = await Equipe.CriarAsync(api);
        Guid membro;
        await using (var db = api.DaEquipe(equipe.TenantId))
        {
            var papel = (await db.Papeis.ToListAsync()).Single(p => p.Nome == Papel.NomeMembro);
            membro = papel.Id;
            // O Dono e mais 29: 30 pessoas na equipe. Vínculos gravados direto, como no apoio dos testes.
            db.AddRange(Enumerable.Range(0, 29).Select(_ => Vinculo.Criar(Guid.NewGuid(), Guid.NewGuid(), papel)));
            await db.SaveChangesAsync();
            (await db.Vinculos.CountAsync()).Should().Be(30);
            (await db.Tenants.AsNoTracking().SingleAsync()).Situacao.Should().Be(SituacaoEmpresa.Teste);
        }

        var email = $"pessoa-{Guid.NewGuid():N}@estudio.com";
        await equipe.EsperarAsync(HttpStatusCode.Accepted, HttpMethod.Post, "/api/v1/convites", new { email, papelId = membro });

        api.Emails.TokenDoConviteEquipe(email).Should().NotBeNullOrEmpty("não há limite de pessoas no teste");
    }

    [Fact]
    public async Task Diario_UmaEmpresaVence_AsOutrasNaoMudamDeSituacao() // F14-09
    {
        var vence = await Equipe.CriarAsync(api);
        var ativa = await Equipe.CriarAsync(api);
        await DefinirSituacaoAsync(ativa.TenantId, "ATIVA");
        var inicio = api.Relogio.Agora;
        var adiante = DuracaoDoTeste + TimeSpan.FromHours(1);
        // A outra em TESTE nasce no instante do diário: o fim do teste dela ainda está 30 dias à frente.
        Guid recente;
        api.Relogio.Agora = inicio + adiante;
        try
        {
            recente = (await Equipe.CriarAsync(api)).TenantId;
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }

        var antesRecente = await EstadoAsync(recente);
        var antesAtiva = await EstadoAsync(ativa.TenantId);

        await DiarioAsync(adiante);

        (await EstadoAsync(vence.TenantId)).Situacao.Should().Be(SituacaoEmpresa.Vencida);
        (await EstadoAsync(recente)).Should().Be(antesRecente);
        (await EstadoAsync(ativa.TenantId)).Should().Be(antesAtiva);
    }

    [Fact]
    public async Task Diario_FimDoTesteExatamenteNoInstanteDoDiario_Vence() // F14-03 (borda)
    {
        var equipe = await Equipe.CriarAsync(api);
        var vencimento = api.Relogio.Agora + DuracaoDoTeste;

        await DiarioAsync(DuracaoDoTeste);

        var estado = await EstadoAsync(equipe.TenantId);
        estado.Situacao.Should().Be(SituacaoEmpresa.Vencida);
        estado.SomenteLeituraDesde.Should().Be(vencimento);
    }

    [Fact]
    public async Task Eu_UmaEmpresaVenceu_AsOutrasContinuamComASituacaoDelas() // F14-09 (isolamento do /auth/eu)
    {
        var vence = await Equipe.CriarAsync(api);
        var inicio = api.Relogio.Agora;
        var adiante = DuracaoDoTeste + TimeSpan.FromHours(1);
        Equipe recente;
        api.Relogio.Agora = inicio + adiante;
        try
        {
            recente = await Equipe.CriarAsync(api);
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }

        await DiarioAsync(adiante);

        var deVence = (await vence.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("tenant");
        deVence.GetProperty("situacao").GetString().Should().Be("VENCIDA");
        // O token da recente nasceu no relógio adiantado: a leitura dela acontece nele.
        JsonElement deRecente;
        api.Relogio.Agora = inicio + adiante;
        try
        {
            deRecente = (await recente.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("tenant");
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }

        deRecente.GetProperty("situacao").GetString().Should().Be("TESTE");
        deRecente.GetProperty("fimDoTeste").GetDateTimeOffset().Should().Be(inicio + adiante + DuracaoDoTeste);
        deRecente.GetProperty("somenteLeituraDesde").ValueKind.Should().Be(JsonValueKind.Null);
        deRecente.GetProperty("expurgoPrevistoEm").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Configuracao_DiasDeTesteEDeExpurgo_PadraoTrintaEConfiguravel() // F14-01
    {
        await using (var padrao = await api.NovaApiAsync(new Dictionary<string, string?>()))
        {
            var opcoes = padrao.Services.GetRequiredService<OpcoesEmpresas>();
            opcoes.DiasDeTeste.Should().Be(30);
            opcoes.DiasAteExpurgo.Should().Be(30);
        }

        await using var app = await api.NovaApiAsync(new Dictionary<string, string?>
        {
            ["Empresas:DiasDeTeste"] = "14",
            ["Empresas:DiasAteExpurgo"] = "45",
        });
        var configurada = app.Services.GetRequiredService<OpcoesEmpresas>();
        configurada.DuracaoDoTeste.Should().Be(TimeSpan.FromDays(14));
        configurada.PrazoAteExpurgo.Should().Be(TimeSpan.FromDays(45));
    }

    [Theory]
    [InlineData("Empresas:DiasDeTeste", "0", "*Empresas__DiasDeTeste*")]
    [InlineData("Empresas:DiasDeTeste", "-3", "*Empresas__DiasDeTeste*")]
    [InlineData("Empresas:DiasDeTeste", "trinta", "*Empresas__DiasDeTeste*")]
    [InlineData("Empresas:DiasDeTeste", "7,5", "*Empresas__DiasDeTeste*")]
    [InlineData("Empresas:DiasAteExpurgo", "0", "*Empresas__DiasAteExpurgo*")]
    [InlineData("Empresas:DiasAteExpurgo", "abc", "*Empresas__DiasAteExpurgo*")]
    public async Task Configuracao_DiasInvalidos_ApiNaoSobe(string chave, string valor, string mensagem) // F14-01
    {
        var subir = () => api.NovaApiAsync(new Dictionary<string, string?> { [chave] = valor });

        (await subir.Should().ThrowAsync<InvalidOperationException>()).WithMessage(mensagem);
    }

    private async Task DefinirSituacaoAsync(Guid tenantId, string situacao)
    {
        await using var db = api.DaEquipe(tenantId);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE tenants SET situacao = {situacao}, fim_do_teste = NULL WHERE id = {tenantId}");
    }

    /// <summary>Situação, início do só leitura e versão (<c>xmin</c>): a versão muda a cada gravação da linha.</summary>
    private async Task<(SituacaoEmpresa Situacao, DateTimeOffset? SomenteLeituraDesde, uint Versao)> EstadoAsync(Guid tenantId)
    {
        await using var db = api.DaEquipe(tenantId);
        var tenant = await db.Tenants.SingleAsync();
        return (tenant.Situacao, tenant.SomenteLeituraDesde, db.Entry(tenant).Property<uint>("Versao").CurrentValue);
    }

    /// <summary>Roda o diário com o relógio adiantado e volta o relógio (as empresas nascem no instante do relógio).</summary>
    private async Task<JsonElement> DiarioAsync(TimeSpan adiante)
    {
        var inicio = api.Relogio.Agora;
        api.Relogio.Agora = inicio + adiante;
        try
        {
            using var http = api.NovoHttp();
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/diario", UriKind.Relative));
            requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
            using var resposta = await http.SendAsync(requisicao);
            var texto = await resposta.Content.ReadAsStringAsync();
            resposta.StatusCode.Should().Be(HttpStatusCode.OK, texto);
            return JsonDocument.Parse(texto).RootElement.Clone();
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }
    }
}
