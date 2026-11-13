using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14d (F14-22, F14-24, F14-111; ADR 0015, itens 4 e 5): os jobs e a outbox não agem na empresa só
/// leitura, sem contar falha, e as outras seguem; na volta, as recorrências não geram o que passou e o SLA não pausou.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class SomenteLeituraJobsTests(ApiFixture api)
{
    private static readonly int[] DiasDaSemanal = [7, 14, 21, 28];

    private DateOnly Hoje() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(api.Relogio.Agora, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);

    private static string Data(DateOnly data) => data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Semanal no dia da semana de hoje, a partir de hoje, com 14 dias de antecedência.</summary>
    private object Semanal(Guid clienteId)
    {
        var hoje = Hoje();
        var dia = hoje.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)hoje.DayOfWeek;
        return new
        {
            clienteId,
            modelo = new { titulo = "Conferir caixa", estimativaMinutos = 30, visivelCliente = false },
            regra = new { periodicidade = "SEMANAL", diaReferencia = dia, antecedenciaDias = 14, inicio = Data(hoje) },
        };
    }

    /// <summary>Situação gravada direto no banco: o vencimento e as ações da área da Nebrys são do F14a e do F14l.</summary>
    private async Task SituacaoAsync(Guid tenantId, string situacao, DateTimeOffset? gravavelDesde = null)
    {
        var agora = api.Relogio.Agora.ToUniversalTime();
        var volta = gravavelDesde?.ToUniversalTime();
        await using var db = api.DaEquipe(tenantId);
        if (situacao is "VENCIDA" or "CANCELADA")
        {
            await db.Database.ExecuteSqlAsync(
                $"UPDATE tenants SET situacao = {situacao}, somente_leitura_desde = {agora} WHERE id = {tenantId}");
        }
        else
        {
            await db.Database.ExecuteSqlAsync(
                $"UPDATE tenants SET situacao = {situacao}, somente_leitura_desde = NULL, fim_do_teste = NULL, gravavel_desde = {volta} WHERE id = {tenantId}");
        }
    }

    private async Task<JsonElement> JobAsync(string job, TimeSpan adiante)
    {
        var inicio = api.Relogio.Agora;
        api.Relogio.Agora = inicio + adiante;
        try
        {
            using var http = api.NovoHttp();
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri($"/internal/jobs/{job}", UriKind.Relative));
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

    /// <summary>Token novo do Dono no instante do relógio (o do registro vence em 60 min do relógio falso).</summary>
    private async Task<string> EntrarAsync(Equipe equipe)
    {
        using var http = api.NovoHttp();
        using var login = await http.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative), new { email = equipe.EmailDono, senha = Equipe.Senha });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private static async Task<(Guid Id, string Email)> PessoaAsync(Equipe equipe, string? token = null)
    {
        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: token);
        return (eu.GetProperty("usuarioId").GetGuid(), eu.GetProperty("email").GetString()!);
    }

    private static async Task<string[]> PrazosDasTarefasAsync(Equipe equipe, Guid cliente) =>
        [.. (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/tarefas?clienteId={cliente}"))
            .GetProperty("itens").EnumerateArray().Select(t => t.GetProperty("prazo").GetString()!)];

    private async Task<(int Pendentes, int Descartadas, int Total)> OutboxAsync(Guid tenantId)
    {
        await using var db = api.DaEquipe(tenantId);
        var pendentes = await db.Database.SqlQuery<int>(
            $"SELECT count(*)::int AS \"Value\" FROM outbox_mensagens WHERE tenant_id = {tenantId} AND processada_em IS NULL").SingleAsync();
        var descartadas = await db.Database.SqlQuery<int>(
            $"SELECT count(*)::int AS \"Value\" FROM outbox_mensagens WHERE tenant_id = {tenantId} AND erro = {ProcessadorOutbox.MotivoSomenteLeitura}").SingleAsync();
        var total = await db.Database.SqlQuery<int>(
            $"SELECT count(*)::int AS \"Value\" FROM outbox_mensagens WHERE tenant_id = {tenantId}").SingleAsync();
        return (pendentes, descartadas, total);
    }

    /// <summary>Volta as mensagens já entregues da empresa para a fila, como se não tivessem saído no fim da requisição.</summary>
    private async Task OutboxPendenteAsync(Guid tenantId)
    {
        var agora = api.Relogio.Agora.ToUniversalTime();
        await using var db = api.DaEquipe(tenantId);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE outbox_mensagens SET processada_em = NULL, proxima_tentativa_em = {agora} WHERE tenant_id = {tenantId}");
    }

    private sealed record Empresa(
        Equipe Equipe, Guid Cliente, Guid ComSla, Guid Resolvida, string EmailDono, string EmailMembro);

    /// <summary>
    /// Recorrência devida, solicitação urgente com SLA de 1 h, solicitação RESOLVIDA com fechamento em 2 dias e as
    /// mensagens da outbox de volta à fila.
    /// </summary>
    private async Task<Empresa> EmpresaComPendenciasAsync()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var (idDono, emailDono) = await PessoaAsync(equipe);
        var (_, emailMembro) = await PessoaAsync(equipe, membro);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/configuracoes/portal", new { diasFechamentoAutomatico = 2 });
        var cliente = await ClienteAsync(equipe);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", new
        {
            clienteId = cliente, tipo = "BANCO_HORAS", inicio = "2026-09-01", franquiaMinutos = 600, sla = Sla(60, 60),
        });
        var comSla = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, titulo = "Servidor caiu", prioridade = "URGENTE", responsavelId = idDono,
        }));
        var resolvida = await SolicitacaoAsync(equipe, cliente);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{resolvida}/resolucao",
            new { solucao = new { texto = "Feito" } });
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", Semanal(cliente));
        await OutboxPendenteAsync(equipe.TenantId);
        return new Empresa(equipe, cliente, comSla, resolvida, emailDono, emailMembro);
    }

    [Theory]
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task TickEDiario_EmpresaSoLeitura_NadaGeradoFechadoNemEnviado_AsOutrasSeguem(string situacao) // F14-22
    {
        var soLeitura = await EmpresaComPendenciasAsync();
        var gravavel = await EmpresaComPendenciasAsync();
        var emailsMembro = api.Emails.Para(soLeitura.EmailMembro).Count;
        (await OutboxAsync(soLeitura.Equipe.TenantId)).Pendentes.Should().BePositive("há mensagem na fila");
        await SituacaoAsync(soLeitura.Equipe.TenantId, situacao);

        // Três dias depois: SLA de 1 h vencido, fechamento de 2 dias passado, recorrência devida.
        var tick = await JobAsync("tick", TimeSpan.FromDays(3));
        var filaGravavel = await OutboxAsync(gravavel.Equipe.TenantId);
        var diario = await JobAsync("diario", TimeSpan.FromDays(3));

        tick.GetProperty("falhas").GetInt32().Should().Be(0);
        diario.GetProperty("falhas").GetInt32().Should().Be(0);

        // Só leitura: nada gerado, fechado nem enviado; a fila sai descartada.
        (await PrazosDasTarefasAsync(soLeitura.Equipe, soLeitura.Cliente)).Should().BeEmpty();
        (await soLeitura.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{soLeitura.Resolvida}"))
            .GetProperty("status").GetString().Should().Be("RESOLVIDA");
        api.Emails.Para(soLeitura.EmailDono).Should().NotContain(m => m.Categoria.StartsWith("aviso-sla", StringComparison.Ordinal));
        api.Emails.Para(soLeitura.EmailMembro).Should().HaveCount(emailsMembro, "a mensagem pendente não foi entregue de novo");
        var fila = await OutboxAsync(soLeitura.Equipe.TenantId);
        fila.Pendentes.Should().Be(0, "descartada não volta para a fila");
        fila.Descartadas.Should().Be(fila.Total);

        // A mensagem descartada não é reprocessada no tick seguinte: nada muda na fila nem sai e-mail.
        var antes = await OutboxAsync(soLeitura.Equipe.TenantId);
        var emailsDono = api.Emails.Para(soLeitura.EmailDono).Count;
        var segundo = await JobAsync("tick", TimeSpan.FromDays(3));
        segundo.GetProperty("falhas").GetInt32().Should().Be(0);
        (await OutboxAsync(soLeitura.Equipe.TenantId)).Should().Be(antes);
        api.Emails.Para(soLeitura.EmailMembro).Should().HaveCount(emailsMembro);
        api.Emails.Para(soLeitura.EmailDono).Should().HaveCount(emailsDono);

        // Gravável: tudo segue.
        (await PrazosDasTarefasAsync(gravavel.Equipe, gravavel.Cliente)).Should().NotBeEmpty();
        (await gravavel.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{gravavel.Resolvida}"))
            .GetProperty("status").GetString().Should().Be("FECHADA");
        api.Emails.Para(gravavel.EmailDono).Should().Contain(m => m.Categoria == "aviso-sla-violado");
        // A fila logo depois do tick (o diário grava o evento do fechamento, que o tick seguinte entrega).
        filaGravavel.Pendentes.Should().Be(0);
        filaGravavel.Descartadas.Should().Be(0);
    }

    [Fact]
    public async Task EmailsDaConta_EmpresaSoLeitura_SaemMesmoComAFilaDescartada() // F14-22 (o descarte não pega e-mail da conta)
    {
        var equipe = await Equipe.CriarAsync(api, confirmar: false);
        await SolicitacaoAsync(equipe, await ClienteAsync(equipe));
        await OutboxPendenteAsync(equipe.TenantId);
        await SituacaoAsync(equipe.TenantId, "CANCELADA");
        var confirmacoes = api.Emails.Para(equipe.EmailDono).Count(m => m.Categoria == "confirmacao-email");

        (await JobAsync("tick", TimeSpan.Zero)).GetProperty("falhas").GetInt32().Should().Be(0);
        await equipe.EsperarAsync(HttpStatusCode.Accepted, HttpMethod.Post, "/api/v1/auth/confirmacao-email/reenvio");
        using var http = api.NovoHttp();
        using var esqueci = await http.PostAsJsonAsync(
            new Uri("/api/v1/auth/senha/esqueci", UriKind.Relative), new { email = equipe.EmailDono });

        esqueci.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await OutboxAsync(equipe.TenantId)).Pendentes.Should().Be(0);
        api.Emails.Para(equipe.EmailDono).Count(m => m.Categoria == "confirmacao-email").Should().Be(confirmacoes + 1);
        api.Emails.TokenDe(equipe.EmailDono, "redefinicao-senha").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Volta_RecorrenciaNaoGeraAsDatasDoSoLeitura_EAEquipeGrava() // F14-24
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var recorrencia = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", Semanal(cliente)));
        var hoje = Hoje();
        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        // Três semanas só leitura: o tick passa por ela sem gerar.
        (await JobAsync("tick", TimeSpan.FromDays(7))).GetProperty("falhas").GetInt32().Should().Be(0);
        (await PrazosDasTarefasAsync(equipe, cliente)).Should().BeEmpty();

        // Volta (ativação, no F14l) 21 dias depois.
        var inicio = api.Relogio.Agora;
        var volta = inicio + TimeSpan.FromDays(21);
        await SituacaoAsync(equipe.TenantId, "ATIVA", volta);
        api.Relogio.Agora = volta;
        try
        {
            var tick = await JobAsync("tick", TimeSpan.Zero);
            tick.GetProperty("falhas").GetInt32().Should().Be(0);
            await equipe.EsperarAsync(
                HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", new { nome = "Mercado Central" }, await EntrarAsync(equipe));
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }

        (await PrazosDasTarefasAsync(equipe, cliente)).Should()
            .BeEquivalentTo([Data(hoje.AddDays(21)), Data(hoje.AddDays(28)), Data(hoje.AddDays(35))],
                "as datas de 0, 7 e 14 dias passaram no só leitura e não são geradas");
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/recorrencias/{recorrencia}"))
            .GetProperty("proximaData").GetString().Should().Be(Data(hoje.AddDays(42)));
    }

    [Theory]
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task Tick_CronometroEsquecidoNaSoLeitura_NaoAvisa_AGravavelAvisa(string situacao) // F14-22
    {
        var soLeitura = await Equipe.CriarAsync(api);
        var gravavel = await Equipe.CriarAsync(api);
        var emails = new List<string>();
        foreach (var equipe in new[] { soLeitura, gravavel })
        {
            var (_, email) = await PessoaAsync(equipe);
            emails.Add(email);
            await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/cronometro/inicio", new
            {
                tarefaId = await TarefaAsync(equipe), iniciadoEm = api.Relogio.Agora.AddMinutes(-630),
            });
        }

        await SituacaoAsync(soLeitura.TenantId, situacao);
        var tick = await JobAsync("tick", TimeSpan.Zero);

        tick.GetProperty("falhas").GetInt32().Should().Be(0);
        api.Emails.Para(emails[0]).Should().NotContain(m => m.Categoria == "aviso-cronometro-esquecido");
        api.Emails.Para(emails[1]).Should().ContainSingle(m => m.Categoria == "aviso-cronometro-esquecido");
    }

    [Theory]
    [InlineData(7, 0, 7)] // volta à meia-noite do dia 7 (fuso da empresa): a data do dia 7 já vale
    [InlineData(7, 22, 7)] // 22 h do dia 7 em São Paulo já é dia 8 em UTC: vale o dia da empresa, não o de UTC
    [InlineData(8, 0, 14)] // volta no dia 8: a data do dia 7 passou no só leitura
    public async Task Volta_RecorrenciaPelaBordaDoDiaNoFusoDaEmpresa(int diaDaVolta, int hora, int primeiraDataGerada) // F14-24
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", Semanal(cliente));
        var hoje = Hoje();
        await SituacaoAsync(equipe.TenantId, "VENCIDA");
        // São Paulo é UTC-3 (sem horário de verão).
        var volta = new DateTimeOffset(hoje.AddDays(diaDaVolta).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddHours(hora + 3);
        await SituacaoAsync(equipe.TenantId, "ATIVA", volta);

        var tick = await JobAsync("tick", volta - api.Relogio.Agora);

        tick.GetProperty("falhas").GetInt32().Should().Be(0);
        var esperadas = DiasDaSemanal.Where(d => d >= primeiraDataGerada && d <= diaDaVolta + 14).Select(d => Data(hoje.AddDays(d)));
        (await PrazosDasTarefasAsync(equipe, cliente)).Should().BeEquivalentTo(esperadas);
    }

    [Fact]
    public async Task Volta_SlaVencidoNoSoLeitura_ApareceVioladoEAlertaSai() // F14-111
    {
        var empresa = await EmpresaComPendenciasAsync();
        await SituacaoAsync(empresa.Equipe.TenantId, "VENCIDA");
        (await JobAsync("tick", TimeSpan.FromDays(3))).GetProperty("falhas").GetInt32().Should().Be(0);
        api.Emails.Para(empresa.EmailDono).Should().NotContain(m => m.Categoria.StartsWith("aviso-sla", StringComparison.Ordinal));

        var volta = api.Relogio.Agora + TimeSpan.FromDays(3);
        await SituacaoAsync(empresa.Equipe.TenantId, "ATIVA", volta);
        var tick = await JobAsync("tick", TimeSpan.FromDays(3));

        tick.GetProperty("falhas").GetInt32().Should().Be(0);
        api.Emails.Para(empresa.EmailDono).Should().ContainSingle(m => m.Categoria == "aviso-sla-violado")
            .Which.Texto.Should().Contain($"/solicitacoes/{empresa.ComSla}");
        api.Emails.Para(empresa.EmailDono).Should().NotContain(m => m.Categoria == "aviso-sla-risco", "o risco caiu no só leitura e não sai depois");
        var inicio = api.Relogio.Agora;
        api.Relogio.Agora = volta;
        try
        {
            (await empresa.Equipe.EsperarAsync(
                    HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{empresa.ComSla}", token: await EntrarAsync(empresa.Equipe)))
                .GetProperty("situacaoSla").GetString().Should().Be("VIOLADO", "o relógio do SLA não pausou no só leitura");
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }
    }
}
