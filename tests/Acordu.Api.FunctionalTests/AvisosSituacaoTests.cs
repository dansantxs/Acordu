using System.Globalization;
using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Application.Abstractions;
using Acordu.Application.Empresas;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14j (F14-39 a F14-43, F14-101 a F14-103 e F14-113; design D7): o passo <c>avisos-situacao</c> do diário
/// manda aos Donos o e-mail antes do fim do teste, no vencimento e antes do expurgo, uma vez por momento, com a marca no
/// <c>Tenant</c>. O banco é compartilhado: os e-mails são filtrados pelo destinatário e pelo nome da empresa.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class AvisosSituacaoTests(ApiFixture api)
{
    private const string Empresa = "Estúdio Traço";
    private static readonly TimeSpan Teste = TimeSpan.FromDays(30);
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById(CalendarioUtil.FusoPadrao);

    // O diário de produção roda às 06:40 UTC (deploy-ci §5); o cadastro dos testes é às 13:00 UTC (10:00 em São Paulo).
    // O momento do aviso é o dia civil no fuso da empresa: com o fim às 13:00 UTC, o diário do dia de "7 dias antes" fica
    // a mais de 7 × 24 h do fim, e o da véspera, a 30 h. Os testes rodam nesses horários para expor a conta por instante.

    [Fact]
    public async Task Diario_TesteNoDiaDoAvisoDeSeteDias_CadaDonoRecebeOFimDoTesteEComoAtivar() // F14-39
    {
        var cadastro = api.Relogio.Agora;
        var equipe = await Equipe.CriarAsync(api);
        var outroDono = await PessoaAsync(equipe, Papel.NomeDono);
        var membro = await PessoaAsync(equipe, Papel.NomeMembro);

        await DiarioAsync(AteODiario(22));
        Avisos(equipe.EmailDono).Should().BeEmpty("no diário de 8 dias antes do último dia ainda não é hora");
        var resultado = await DiarioAsync(AteODiario(23));

        resultado.GetProperty("avisosSituacao").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        resultado.GetProperty("falhas").GetInt32().Should().Be(0);
        var fim = Dia(cadastro + Teste - TimeSpan.FromTicks(1));
        foreach (var dono in new[] { equipe.EmailDono, outroDono })
        {
            var avisos = Avisos(dono, MensagensDeSituacao.CategoriaFimDoTeste);
            avisos.Should().ContainSingle();
            avisos[0].Texto.Should().Contain($"vai até {fim} (faltam 7 dias)")
                .And.Contain($"fale com a Nebrys: {ApiFixture.ContatoNebrys}");
        }

        Avisos(membro).Should().BeEmpty("só os Donos recebem");
    }

    [Fact]
    public async Task Diario_RodadoDeNovoNoMesmoDiaENoSeguinte_OAvisoNaoSaiDeNovo() // F14-40
    {
        var equipe = await Equipe.CriarAsync(api);

        await DiarioAsync(AteODiario(23));
        await DiarioAsync(AteODiario(23) + TimeSpan.FromHours(10));
        await DiarioAsync(AteODiario(24));

        Avisos(equipe.EmailDono).Should().ContainSingle();
        (await MarcasAsync(equipe.TenantId)).Should().ContainSingle().Which.Should().StartWith("fim-do-teste-7:");
    }

    [Fact]
    public async Task Diario_DepoisDoDeSeteChegaODeUmDia_SaiOSegundoAviso() // F14-39 (os dois momentos)
    {
        var equipe = await Equipe.CriarAsync(api);

        await DiarioAsync(AteODiario(23));
        await DiarioAsync(AteODiario(28));
        Avisos(equipe.EmailDono).Should().ContainSingle("o diário de dois dias antes não manda o de 1 dia");
        await DiarioAsync(AteODiario(29));

        var avisos = Avisos(equipe.EmailDono, MensagensDeSituacao.CategoriaFimDoTeste);
        avisos.Should().HaveCount(2, "o de 1 dia sai no diário da véspera, a 30 h do fim");
        avisos[1].Texto.Should().Contain("termina amanhã");
    }

    [Fact]
    public async Task Diario_EmpresaVenceu_CadaDonoRecebeSoLeituraDataDoExpurgoEComoAtivar() // F14-41
    {
        var equipe = await Equipe.CriarAsync(api);
        var outroDono = await PessoaAsync(equipe, Papel.NomeDono);
        var adiante = AteODiario(31);
        var expurgo = Dia(api.Relogio.Agora + adiante + TimeSpan.FromDays(30));

        var resultado = await DiarioAsync(adiante);

        (await SituacaoAsync(equipe.TenantId)).Should().Be(SituacaoEmpresa.Vencida);
        resultado.GetProperty("avisosSituacao").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        foreach (var dono in new[] { equipe.EmailDono, outroDono })
        {
            var avisos = Avisos(dono, MensagensDeSituacao.CategoriaVencimento);
            avisos.Should().ContainSingle();
            avisos[0].Texto.Should().Contain("ficou só leitura").And.Contain($"a partir de {expurgo}")
                .And.Contain($"Para ativar a conta e manter os dados, fale com a Nebrys: {ApiFixture.ContatoNebrys}");
        }
    }

    [Fact]
    public async Task Diario_VencidaNoDiaDoAvisoAntesDoExpurgo_CadaDonoRecebeADataDoExpurgo() // F14-42
    {
        var equipe = await Equipe.CriarAsync(api);
        var outroDono = await PessoaAsync(equipe, Papel.NomeDono);
        // Vence no diário de 31 dias depois; o expurgo cai 30 dias depois, também às 06:40 UTC.
        var vencimento = AteODiario(31);
        var expurgo = Dia(api.Relogio.Agora + vencimento + TimeSpan.FromDays(30));
        await DiarioAsync(vencimento);

        await DiarioAsync(AteODiario(31 + 22));
        Avisos(equipe.EmailDono, MensagensDeSituacao.CategoriaExpurgo).Should().BeEmpty("ainda faltam 8 dias");
        await DiarioAsync(AteODiario(31 + 23));

        foreach (var dono in new[] { equipe.EmailDono, outroDono })
        {
            var avisos = Avisos(dono, MensagensDeSituacao.CategoriaExpurgo);
            avisos.Should().ContainSingle();
            avisos[0].Assunto.Should().Contain(expurgo);
            avisos[0].Texto.Should().Contain($"A partir de {expurgo}");
        }
    }

    [Fact]
    public async Task Diario_CanceladaNoDiaDoAvisoAntesDoExpurgo_DonoRecebeADataDoExpurgo() // F14-42
    {
        var equipe = await Equipe.CriarAsync(api);
        var desde = api.Relogio.Agora;
        // O cancelamento pela área da Nebrys é do F14l; aqui o estado é gravado direto. Expurgo às 13:00 UTC de 30 dias
        // depois: o aviso sai no diário (06:40 UTC) do dia de 7 dias antes, a mais de 7 × 24 h do expurgo.
        await GravarAsync(equipe.TenantId, "CANCELADA", fimDoTeste: null, somenteLeituraDesde: desde);

        await DiarioAsync(AteODiario(22));
        Avisos(equipe.EmailDono).Should().BeEmpty();
        await DiarioAsync(AteODiario(23));

        var avisos = Avisos(equipe.EmailDono);
        avisos.Should().ContainSingle().Which.Categoria.Should().Be(MensagensDeSituacao.CategoriaExpurgo);
        avisos[0].Texto.Should().Contain($"A partir de {Dia(desde + TimeSpan.FromDays(30))}");
    }

    [Fact]
    public async Task Diario_EmpresaNaoLiberada_SoOCriadorRecebe() // F14-43
    {
        var equipe = await Equipe.CriarAsync(api, confirmar: false);
        var outroDono = await PessoaAsync(equipe, Papel.NomeDono);

        await DiarioAsync(AteODiario(23));
        await DiarioAsync(AteODiario(31));

        Avisos(equipe.EmailDono, MensagensDeSituacao.CategoriaFimDoTeste).Should().ContainSingle();
        Avisos(equipe.EmailDono, MensagensDeSituacao.CategoriaVencimento).Should().ContainSingle();
        Avisos(outroDono).Should().BeEmpty("antes da liberação, só o criador recebe (RF-AUT-06)");
    }

    [Fact]
    public async Task Diario_SemDestinatario_NadaEMarcadoEOAvisoSaiQuandoHaQuemReceba() // design D7 (F14-39, F14-43)
    {
        var equipe = await Equipe.CriarAsync(api, confirmar: false);
        var outroDono = await PessoaAsync(equipe, Papel.NomeDono);
        // Antes da liberação só o criador recebe, e ele deixou de ser Dono: ninguém pode receber.
        await using (var db = api.DaEquipe(equipe.TenantId))
        {
            await db.Database.ExecuteSqlAsync($"""
                UPDATE vinculos SET papel_id = (SELECT id FROM papeis WHERE tenant_id = {equipe.TenantId} AND nome = 'Membro')
                WHERE tenant_id = {equipe.TenantId}
                  AND usuario_id = (SELECT criado_por_usuario_id FROM tenants WHERE id = {equipe.TenantId})
                """);
        }

        await DiarioAsync(AteODiario(23));

        Avisos(equipe.EmailDono).Should().BeEmpty();
        Avisos(outroDono).Should().BeEmpty();
        (await MarcasAsync(equipe.TenantId)).Should().BeEmpty("sem quem receba, o aviso continua devido");

        // Liberado o envio, o outro Dono passa a receber: o aviso sai no diário seguinte, com os dias de então.
        await using (var db = api.DaEquipe(equipe.TenantId))
            await db.Database.ExecuteSqlAsync($"UPDATE tenants SET envio_liberado_em = {api.Relogio.Agora} WHERE id = {equipe.TenantId}");
        await DiarioAsync(AteODiario(24));

        Avisos(outroDono, MensagensDeSituacao.CategoriaFimDoTeste).Should().ContainSingle()
            .Which.Texto.Should().Contain("faltam 6 dias");
        (await MarcasAsync(equipe.TenantId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Diario_ProrrogadoDepoisDoAviso_OAvisoSaiDeNovoComANovaData() // F14-101
    {
        var cadastro = api.Relogio.Agora;
        var equipe = await Equipe.CriarAsync(api);
        await DiarioAsync(AteODiario(23));
        // A prorrogação pela área da Nebrys é do F14l: o novo fim é gravado direto, 10 dias depois do primeiro.
        var novoFim = cadastro + Teste + TimeSpan.FromDays(10);
        await GravarAsync(equipe.TenantId, "TESTE", fimDoTeste: novoFim, somenteLeituraDesde: null);

        await DiarioAsync(AteODiario(24));
        Avisos(equipe.EmailDono).Should().ContainSingle("o novo fim ainda está a 16 dias");
        await DiarioAsync(AteODiario(33));

        var avisos = Avisos(equipe.EmailDono, MensagensDeSituacao.CategoriaFimDoTeste);
        avisos.Should().HaveCount(2);
        avisos[1].Texto.Should().Contain($"vai até {Dia(novoFim - TimeSpan.FromTicks(1))} (faltam 7 dias)");
    }

    [Fact]
    public async Task Diario_MomentoDoDeSetePassou_SaiUmAvisoSoComOsDiasQueFaltam() // F14-102
    {
        var cadastro = api.Relogio.Agora;
        var equipe = await Equipe.CriarAsync(api);
        // Prorrogado (ou encurtado) para 4 dias à frente sem o aviso de 7 dias ter saído.
        await GravarAsync(equipe.TenantId, "TESTE", fimDoTeste: cadastro + TimeSpan.FromDays(4), somenteLeituraDesde: null);

        await DiarioAsync(TimeSpan.FromHours(1));
        await DiarioAsync(TimeSpan.FromDays(1));

        var avisos = Avisos(equipe.EmailDono);
        avisos.Should().ContainSingle();
        avisos[0].Texto.Should().Contain("faltam 4 dias").And.NotContain("7 dias");
    }

    [Fact]
    public async Task Diario_DoisAvisosDevidosNoMesmoDiario_SaiSoODeUmDiaEOOutroNaoSaiDepois() // F14-103
    {
        var cadastro = api.Relogio.Agora;
        var equipe = await Equipe.CriarAsync(api);
        // Prorrogado para o dia seguinte: os momentos do de 7 e do de 1 dia já chegaram.
        await GravarAsync(equipe.TenantId, "TESTE", fimDoTeste: cadastro + TimeSpan.FromHours(30), somenteLeituraDesde: null);

        await DiarioAsync(TimeSpan.FromHours(10));
        await DiarioAsync(TimeSpan.FromHours(20));

        var avisos = Avisos(equipe.EmailDono);
        avisos.Should().ContainSingle();
        avisos[0].Texto.Should().Contain("termina amanhã");
        (await MarcasAsync(equipe.TenantId)).Should().HaveCount(2).And.Contain(m => m.StartsWith("fim-do-teste-1:", StringComparison.Ordinal))
            .And.Contain(m => m.StartsWith("fim-do-teste-7:", StringComparison.Ordinal));
    }

    [Theory] // F14-113
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task Diario_SoLeituraComADataDoExpurgoJaPassada_NenhumAvisoSai(string situacao)
    {
        var equipe = await Equipe.CriarAsync(api);
        // Como as empresas que a migration do F14 cancelou (F14-08): só leitura desde 2026-01-01, expurgo já vencido.
        await GravarAsync(
            equipe.TenantId, situacao, fimDoTeste: null, somenteLeituraDesde: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        await DiarioAsync(TimeSpan.FromMinutes(5));

        Avisos(equipe.EmailDono).Should().BeEmpty();
    }

    [Fact]
    public async Task Diario_OutrosPapeisContatosDoPortalEOutraEmpresa_NaoRecebem() // F14-39 a F14-42, destinatários
    {
        var equipe = await Equipe.CriarAsync(api);
        var leitor = await PessoaAsync(equipe, Papel.NomeLeitor);
        var contato = $"contato-{Guid.NewGuid():N}@exemplo.com";
        await using (var db = api.DaEquipe(equipe.TenantId))
        {
            var cliente = Cliente.Criar(Guid.NewGuid(), equipe.TenantId, "Padaria Bom Grão", null, null);
            cliente.AdicionarContato("Marina", contato, null, null);
            db.Add(cliente);
            await db.SaveChangesAsync();
        }

        var outra = await Equipe.CriarAsync(api);
        await DiarioAsync(Teste - TimeSpan.FromDays(7));

        Avisos(equipe.EmailDono).Should().ContainSingle();
        Avisos(leitor).Should().BeEmpty("só o papel Dono recebe");
        api.Emails.Para(contato).Should().NotContain(m => m.Categoria.StartsWith("situacao-", StringComparison.Ordinal));
        // A outra empresa, no mesmo diário, tem o aviso dela e o Dono dela recebe um só (o da própria empresa).
        Avisos(outra.EmailDono).Should().ContainSingle();
        (await MarcasAsync(outra.TenantId)).Should().ContainSingle();
        (await MarcasAsync(equipe.TenantId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Diario_EmpresaEmOutroFuso_AsDatasDoEmailSaoNoFusoDaEmpresa() // F14-39
    {
        var equipe = await Equipe.CriarAsync(api);
        var manaus = TimeZoneInfo.FindSystemTimeZoneById("America/Manaus");
        using (var resposta = await equipe.EnviarAsync(HttpMethod.Put, "/api/v1/configuracoes/calendario", new
        {
            fusoHorario = "America/Manaus",
            expediente = new[] { new { diaSemana = "SEGUNDA", inicio = "08:00", fim = "18:00" } },
            feriados = Array.Empty<string>(),
        }))
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        // 03:30 UTC: o instante antes do fim ainda é o dia anterior em Manaus (23:29) e já é o mesmo dia em São Paulo (00:29).
        var fim = new DateTimeOffset(api.Relogio.Agora.UtcDateTime.Date.AddDays(5).AddHours(3).AddMinutes(30), TimeSpan.Zero);
        await GravarAsync(equipe.TenantId, "TESTE", fimDoTeste: fim, somenteLeituraDesde: null);

        await DiarioAsync(TimeSpan.FromHours(1));

        var emManaus = TimeZoneInfo.ConvertTime(fim - TimeSpan.FromTicks(1), manaus).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        emManaus.Should().NotBe(Dia(fim - TimeSpan.FromTicks(1)), "o cenário só vale se os fusos divergem");
        Avisos(equipe.EmailDono).Should().ContainSingle().Which.Texto.Should().Contain($"vai até {emManaus}");
    }

    [Fact]
    public async Task Diario_EmpresaAtiva_NenhumAvisoSai()
    {
        var equipe = await Equipe.CriarAsync(api);
        await GravarAsync(equipe.TenantId, "ATIVA", fimDoTeste: null, somenteLeituraDesde: null);

        await DiarioAsync(Teste - TimeSpan.FromDays(1));

        Avisos(equipe.EmailDono).Should().BeEmpty();
    }

    [Fact]
    public async Task Diario_EnvioFalha_ContaEmFalhasTiraAMarcaEOProximoDiarioTentaDeNovo() // design D7
    {
        var equipe = await Equipe.CriarAsync(api);
        var falho = new EnviadorQueFalha(api.Emails, equipe.EmailDono);
        await using (var app = await api.NovaApiAsync(
            new Dictionary<string, string?>(), servicos: s => s.AddSingleton<IEnviadorEmail>(falho)))
        {
            var resultado = await DiarioAsync(Teste - TimeSpan.FromDays(7), app);

            resultado.GetProperty("falhas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
            (await MarcasAsync(equipe.TenantId)).Should().BeEmpty();
            Avisos(equipe.EmailDono).Should().BeEmpty();
        }

        await DiarioAsync(Teste - TimeSpan.FromDays(7) + TimeSpan.FromHours(1));

        Avisos(equipe.EmailDono, MensagensDeSituacao.CategoriaFimDoTeste).Should().ContainSingle();
        (await MarcasAsync(equipe.TenantId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Configuracao_AvisosAntesDoFimEDoExpurgo_PadraoEConfiguravel() // F14-39, F14-42 (decisão 3 do G2)
    {
        await using (var padrao = await api.NovaApiAsync(new Dictionary<string, string?>()))
        {
            var prazos = padrao.Services.GetRequiredService<OpcoesEmpresas>().PrazosDosAvisos;
            prazos.DiasAntesDoFim.Should().Equal(1, 7);
            prazos.DiasAntesDoExpurgo.Should().Be(7);
        }

        await using var app = await api.NovaApiAsync(new Dictionary<string, string?>
        {
            ["Empresas:AvisosAntesDoFim"] = "10, 3",
            ["Empresas:AvisoAntesDoExpurgo"] = "5",
        });
        var configurados = app.Services.GetRequiredService<OpcoesEmpresas>().PrazosDosAvisos;
        configurados.DiasAntesDoFim.Should().Equal(3, 10);
        configurados.DiasAntesDoExpurgo.Should().Be(5);
    }

    [Theory]
    [InlineData("Empresas:AvisosAntesDoFim", "7,0", "*Empresas__AvisosAntesDoFim*")]
    [InlineData("Empresas:AvisosAntesDoFim", "sete", "*Empresas__AvisosAntesDoFim*")]
    [InlineData("Empresas:AvisosAntesDoFim", "7,,1", "*Empresas__AvisosAntesDoFim*")]
    [InlineData("Empresas:AvisoAntesDoExpurgo", "0", "*Empresas__AvisoAntesDoExpurgo*")]
    public async Task Configuracao_AvisosInvalidos_ApiNaoSobe(string chave, string valor, string mensagem)
    {
        var subir = () => api.NovaApiAsync(new Dictionary<string, string?> { [chave] = valor });

        (await subir.Should().ThrowAsync<InvalidOperationException>()).WithMessage(mensagem);
    }

    /// <summary>Os e-mails de situação sobre a empresa deste teste ao endereço (o nome dela vem no texto).</summary>
    private List<MensagemEmail> Avisos(string email, string? categoria = null) =>
        [.. api.Emails.Para(email).Where(m => m.Categoria.StartsWith("situacao-", StringComparison.Ordinal)
            && m.Texto.Contains(Empresa, StringComparison.Ordinal)
            && (categoria is null || m.Categoria == categoria))];

    /// <summary>
    /// Uma pessoa com o papel na empresa (vínculo gravado direto, pelo apoio); devolve o e-mail dela. Ela tem também a
    /// própria empresa ("Própria"), cujos avisos o filtro por nome deixa de fora.
    /// </summary>
    private static async Task<string> PessoaAsync(Equipe equipe, string papel)
    {
        var token = await equipe.ComPapelAsync(papel);
        return (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: token))
            .GetProperty("email").GetString()!;
    }

    private async Task GravarAsync(Guid tenantId, string situacao, DateTimeOffset? fimDoTeste, DateTimeOffset? somenteLeituraDesde)
    {
        await using var db = api.DaEquipe(tenantId);
        await db.Database.ExecuteSqlAsync($"""
            UPDATE tenants SET situacao = {situacao}, fim_do_teste = {fimDoTeste}, somente_leitura_desde = {somenteLeituraDesde}
            WHERE id = {tenantId}
            """);
    }

    private async Task<IReadOnlyCollection<string>> MarcasAsync(Guid tenantId)
    {
        await using var db = api.DaEquipe(tenantId);
        return (await db.Tenants.AsNoTracking().SingleAsync()).AvisosSituacao;
    }

    private async Task<SituacaoEmpresa> SituacaoAsync(Guid tenantId)
    {
        await using var db = api.DaEquipe(tenantId);
        return (await db.Tenants.AsNoTracking().SingleAsync()).Situacao;
    }

    /// <summary>Quanto adiantar o relógio até o diário de produção (06:40 UTC) do dia que fica <paramref name="dias"/> depois de hoje (UTC).</summary>
    private TimeSpan AteODiario(int dias)
    {
        var agora = api.Relogio.Agora;
        return new DateTimeOffset(agora.UtcDateTime.Date.AddDays(dias).AddHours(6).AddMinutes(40), TimeSpan.Zero) - agora;
    }

    private static string Dia(DateTimeOffset instante) =>
        TimeZoneInfo.ConvertTime(instante, SaoPaulo).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Roda o diário com o relógio adiantado e volta o relógio (as empresas nascem no instante do relógio).</summary>
    private async Task<JsonElement> DiarioAsync(TimeSpan adiante, WebApplication? app = null)
    {
        var inicio = api.Relogio.Agora;
        api.Relogio.Agora = inicio + adiante;
        try
        {
            using var http = app?.GetTestClient() ?? api.NovoHttp();
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

    /// <summary>Falha o envio a um endereço e entrega os demais na caixa dos testes.</summary>
    private sealed class EnviadorQueFalha(IEnviadorEmail caixa, string falhaPara) : IEnviadorEmail
    {
        public Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancellationToken = default) =>
            mensagem.Para == falhaPara
                ? throw new HttpRequestException("Falha simulada no envio.")
                : caixa.EnviarAsync(mensagem, cancellationToken);
    }
}
