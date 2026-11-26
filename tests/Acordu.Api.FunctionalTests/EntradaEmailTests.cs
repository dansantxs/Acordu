using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Api.Operacao;
using Acordu.Domain.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F15, F15d (F15-05, F15-19, F15-22, F15-89; design D1, D5, D6 e D12; ADR 0016, itens 1 a 3 e 8; contrato §18):
/// a rota <c>/internal/email/entrada</c> que o Worker chama pela borda. Token, tamanho, destino → empresa e os descartes
/// sem gravar nada, com a mesma resposta para todos (o adaptador não aprende nada sobre as empresas). F15e (F15-85, F15-87,
/// F15-88, F15-108; D11, D12 e D16): automático, laço e filtro do provedor viram descarte registrado, e o diário apaga o
/// registro vencido.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class EntradaEmailTests(ApiFixture api)
{
    private const string Rota = "/api/v1/configuracoes/recebimento-email";
    private const string Entrada = "/internal/email/entrada";
    private const string Remetente = "ana@cliente-da-entrada.com.br";

    private static byte[] Mime(
        string? messageId, string de = "Ana <" + Remetente + ">", string cabecalhos = "", string tipo = "text/plain; charset=utf-8") =>
        Encoding.UTF8.GetBytes(
        $"From: {de}\r\n"
        + "To: empresa@entrada.acordu.app.br\r\n"
        + "Subject: Forno parou de novo\r\n"
        + (messageId is null ? string.Empty : $"Message-ID: {messageId}\r\n")
        + "Date: Tue, 06 Oct 2026 10:00:00 -0300\r\n"
        + cabecalhos
        + "MIME-Version: 1.0\r\n"
        + $"Content-Type: {tipo}\r\n"
        + "\r\n"
        + "O forno parou de novo hoje cedo.\r\n");

    private static string NovoMessageId() => $"<{Guid.NewGuid():N}@mail.cliente-da-entrada.com.br>";

    private static async Task<(HttpStatusCode Status, string Corpo)> EntregarAsync(
        HttpClient http,
        string? destino,
        byte[] corpo,
        string? token = ApiFixture.TokenEntrada,
        bool emPartes = false,
        string? envelope = Remetente,
        string? filtro = null)
    {
        using var conteudo = new StreamContent(new MemoryStream(corpo));
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
        if (!emPartes)
            conteudo.Headers.ContentLength = corpo.Length;
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri(Entrada, UriKind.Relative)) { Content = conteudo };
        if (emPartes)
            pedido.Headers.TransferEncodingChunked = true;
        if (token is not null)
            pedido.Headers.TryAddWithoutValidation("X-Email-Token", token);
        if (envelope is not null)
            pedido.Headers.TryAddWithoutValidation("X-Email-Remetente", envelope);
        if (filtro is not null)
            pedido.Headers.TryAddWithoutValidation("X-Email-Filtro", filtro);
        if (destino is not null)
            pedido.Headers.TryAddWithoutValidation("X-Email-Destinatario", destino);
        using var resposta = await http.SendAsync(pedido);
        return (resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
    }

    private async Task<(HttpStatusCode Status, string Corpo)> EntregarAsync(
        string? destino, byte[] corpo, string? token = ApiFixture.TokenEntrada, bool emPartes = false)
    {
        using var http = api.NovoHttp();
        return await EntregarAsync(http, destino, corpo, token, emPartes);
    }

    private async Task<string> CodigoAsync(Guid tenantId)
    {
        await using var db = api.DaEquipe(tenantId);
        return (await db.Tenants.AsNoTracking().SingleAsync()).CodigoEntrada!;
    }

    private async Task<string> EnderecoAsync(Equipe equipe) => $"{await CodigoAsync(equipe.TenantId)}@{ApiFixture.DominioEntrada}";

    private static async Task LigarAsync(Equipe equipe) =>
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, Rota, new { ligado = true });

    private async Task<Equipe> LigadaAsync()
    {
        var equipe = await Equipe.CriarAsync(api);
        await LigarAsync(equipe);
        return equipe;
    }

    private async Task SituacaoAsync(Guid tenantId, string situacao)
    {
        var desde = api.Relogio.Agora.ToUniversalTime();
        await using var db = api.DaEquipe(tenantId);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE tenants SET situacao = {situacao}, somente_leitura_desde = {desde} WHERE id = {tenantId}");
    }

    /// <summary>O que a entrada pode ter gravado na empresa: registro, solicitação ou mensagem.</summary>
    private async Task<(int Registros, int Solicitacoes, int Mensagens)> GravadoAsync(Guid tenantId)
    {
        await using var db = api.DaEquipe(tenantId);
        return (await db.EmailsRecebidos.CountAsync(), await db.Solicitacoes.CountAsync(), await db.Interacoes.CountAsync());
    }

    /// <summary>Registros de entrada de todas as empresas (SQL cru, sem os filtros): a coleção roda os testes em série.</summary>
    private async Task<int> RegistrosEmTodasAsync()
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        return await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM emails_recebidos").SingleAsync();
    }

    [Fact]
    public async Task Entregar_RecebimentoLigado_202SemCorpoERegistroIgnorado() // design D6 e §3: até o F15f, IGNORADO
    {
        var equipe = await LigadaAsync();

        var (status, corpo) = await EntregarAsync(await EnderecoAsync(equipe), Mime(NovoMessageId()));

        status.Should().Be(HttpStatusCode.Accepted);
        corpo.Should().BeEmpty();
        await using var db = api.DaEquipe(equipe.TenantId);
        var registro = await db.EmailsRecebidos.AsNoTracking().SingleAsync();
        registro.Resultado.Should().Be(ResultadoEntradaEmail.Ignorado);
        registro.RecebidoEm.Should().BeCloseTo(api.Relogio.Agora, TimeSpan.FromMilliseconds(1), "o horário é o da recepção, não o Date do e-mail (D14)");
        registro.RemetenteHash.Should().NotBeNull().And.NotContain("ana", "o remetente fica só como hash");
        (await db.Solicitacoes.CountAsync()).Should().Be(0, "a abertura por e-mail é do F15f");
    }

    [Fact]
    public async Task Entregar_RecebimentoDesligado_202SemGravarNemResponder() // F15-05
    {
        var equipe = await Equipe.CriarAsync(api);

        var (status, corpo) = await EntregarAsync(await EnderecoAsync(equipe), Mime(NovoMessageId()));

        status.Should().Be(HttpStatusCode.Accepted);
        corpo.Should().BeEmpty();
        (await GravadoAsync(equipe.TenantId)).Should().Be((0, 0, 0));
        api.Emails.Para(Remetente).Should().BeEmpty("nada é respondido ao remetente");
    }

    [Fact]
    public async Task Entregar_DesligadoDepoisDeLigado_SoOAnteriorFicaRegistrado() // F15-04 e F15-05
    {
        var equipe = await LigadaAsync();
        var endereco = await EnderecoAsync(equipe);
        (await EntregarAsync(endereco, Mime(NovoMessageId()))).Status.Should().Be(HttpStatusCode.Accepted);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, Rota, new { ligado = false });

        var (status, _) = await EntregarAsync(endereco, Mime(NovoMessageId()));

        status.Should().Be(HttpStatusCode.Accepted);
        (await GravadoAsync(equipe.TenantId)).Should().Be((1, 0, 0), "o que já entrou não muda; o que chega depois não grava");
    }

    [Theory]
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task Entregar_EmpresaSoLeituraComRecebimentoLigado_MesmaRespostaSemGravarNemResponder(string situacao) // F15-19
    {
        var soLeitura = await LigadaAsync();
        var ativa = await LigadaAsync();
        await SituacaoAsync(soLeitura.TenantId, situacao);

        var recusada = await EntregarAsync(await EnderecoAsync(soLeitura), Mime(NovoMessageId()));
        var aceita = await EntregarAsync(await EnderecoAsync(ativa), Mime(NovoMessageId()));

        recusada.Should().Be(aceita, "a resposta não revela a situação da empresa (RF-EMP-02)");
        recusada.Status.Should().Be(HttpStatusCode.Accepted);
        (await GravadoAsync(soLeitura.TenantId)).Should().Be((0, 0, 0));
        api.Emails.Para(Remetente).Should().BeEmpty();
    }

    [Theory]
    [InlineData("k7m2qd9w@entrada.acordu.app.br")] // código que nenhuma empresa tem (nunca existiu, ou expurgada)
    [InlineData("k7m2qd9w+abcdefghjkmnpqrs@entrada.acordu.app.br")] // conversa de um código que não existe
    [InlineData("k7m2qd@entrada.acordu.app.br")] // fora do formato do código
    [InlineData("contato@entrada.acordu.app.br")]
    [InlineData("k7m2qd9w@acordu.app.br")] // outro domínio
    [InlineData("")]
    [InlineData(null)]
    public async Task Entregar_DestinoQueNaoEDeEmpresaNenhuma_202SemGravarEmNenhuma(string? destino) // F15-22
    {
        var ligada = await LigadaAsync();
        var antes = await RegistrosEmTodasAsync();

        var (status, corpo) = await EntregarAsync(destino, Mime(NovoMessageId()));

        status.Should().Be(HttpStatusCode.Accepted);
        corpo.Should().BeEmpty();
        (await RegistrosEmTodasAsync()).Should().Be(antes, "nada é gravado em empresa nenhuma");
        (await GravadoAsync(ligada.TenantId)).Should().Be((0, 0, 0));
    }

    [Fact]
    public async Task Entregar_CodigoDeOutraEmpresaEmMaiusculas_SoNaDonaDoCodigo() // F15-22, ADR 0016 item 3
    {
        var dona = await LigadaAsync();
        var outra = await LigadaAsync();

        await EntregarAsync((await EnderecoAsync(dona)).ToUpperInvariant(), Mime(NovoMessageId()));

        (await GravadoAsync(dona.TenantId)).Registros.Should().Be(1, "maiúsculas não contam no endereço");
        (await GravadoAsync(outra.TenantId)).Registros.Should().Be(0);
    }

    [Fact]
    public async Task Entregar_AcimaDoTamanhoMaximo_413SemGravar() // F15-89
    {
        var equipe = await LigadaAsync();
        var endereco = await EnderecoAsync(equipe);
        var mime = Mime(NovoMessageId());
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?>
        {
            ["EmailEntrada:TamanhoMaximoBytes"] = (mime.Length - 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
        using var http = app.GetTestClient();

        var declarado = await EntregarAsync(http, endereco, mime);
        var emPartes = await EntregarAsync(http, endereco, mime, emPartes: true);

        declarado.Status.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        emPartes.Status.Should().Be(HttpStatusCode.RequestEntityTooLarge, "sem Content-Length, a leitura conta os bytes");
        (await GravadoAsync(equipe.TenantId)).Should().Be((0, 0, 0));
        api.Emails.Para(Remetente).Should().BeEmpty("a recusa é a do servidor, sem mensagem do Acordu");
    }

    [Fact]
    public async Task Entregar_NoTamanhoMaximo_Aceito() // F15-89, o limite é inclusivo
    {
        var equipe = await LigadaAsync();
        var mime = Mime(NovoMessageId());
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?>
        {
            ["EmailEntrada:TamanhoMaximoBytes"] = mime.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
        using var http = app.GetTestClient();

        var (status, _) = await EntregarAsync(http, await EnderecoAsync(equipe), mime, emPartes: true);

        status.Should().Be(HttpStatusCode.Accepted);
        (await GravadoAsync(equipe.TenantId)).Registros.Should().Be(1);
    }

    [Fact]
    public async Task Entregar_AcimaDoTamanhoMaximoEmpresaDesligada_413MesmoAssim() // design D12: tamanho antes do destino
    {
        var equipe = await Equipe.CriarAsync(api);
        var mime = Mime(NovoMessageId());
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?> { ["EmailEntrada:TamanhoMaximoBytes"] = "64" });
        using var http = app.GetTestClient();

        var (status, _) = await EntregarAsync(http, await EnderecoAsync(equipe), mime);

        status.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("token-errado")]
    [InlineData("")]
    public async Task Entregar_SemTokenCerto_404SemGravar(string? token) // design D5
    {
        var equipe = await LigadaAsync();

        var (status, _) = await EntregarAsync(await EnderecoAsync(equipe), Mime(NovoMessageId()), token);

        status.Should().Be(HttpStatusCode.NotFound);
        (await GravadoAsync(equipe.TenantId)).Registros.Should().Be(0);
    }

    [Fact]
    public async Task Entregar_SemTokenConfigurado_404ParaTodos() // design D5: sem EmailEntrada__Token, a rota não existe
    {
        var equipe = await LigadaAsync();
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?> { ["EmailEntrada:Token"] = null });
        using var http = app.GetTestClient();

        var (status, _) = await EntregarAsync(http, await EnderecoAsync(equipe), Mime(NovoMessageId()));

        status.Should().Be(HttpStatusCode.NotFound);
        (await GravadoAsync(equipe.TenantId)).Registros.Should().Be(0);
    }

    [Fact]
    public async Task Entregar_MesmaEntregaDuasVezes_UmRegistroSo() // design D6: a repetição do servidor de origem
    {
        var equipe = await LigadaAsync();
        var endereco = await EnderecoAsync(equipe);
        var mime = Mime(NovoMessageId());

        var primeira = await EntregarAsync(endereco, mime);
        var segunda = await EntregarAsync(endereco, mime);

        segunda.Should().Be(primeira);
        (await GravadoAsync(equipe.TenantId)).Registros.Should().Be(1);
    }

    [Fact]
    public async Task Entregar_SemMessageIdDuasVezes_UmRegistroPeloConteudo() // design D6
    {
        var equipe = await LigadaAsync();
        var endereco = await EnderecoAsync(equipe);

        await EntregarAsync(endereco, Mime(null));
        await EntregarAsync(endereco, Mime(null));
        await EntregarAsync(endereco, Mime(null, de: "Bruno <bruno@cliente-da-entrada.com.br>"));

        (await GravadoAsync(equipe.TenantId)).Registros.Should().Be(2, "o conteúdo diferente é outro e-mail");
    }

    [Fact]
    public async Task Entregar_MesmoEmailParaDuasEmpresas_UmRegistroEmCada() // design D6 (base do F15-21)
    {
        var a = await LigadaAsync();
        var b = await LigadaAsync();
        var mime = Mime(NovoMessageId());

        await EntregarAsync(await EnderecoAsync(a), mime);
        await EntregarAsync(await EnderecoAsync(b), mime);

        (await GravadoAsync(a.TenantId)).Registros.Should().Be(1);
        (await GravadoAsync(b.TenantId)).Registros.Should().Be(1);
    }

    [Fact]
    public async Task Entregar_CorpoVazio_202SemGravar() // plano-2, menor 6: MIME ilegível não pode virar 5xx
    {
        var equipe = await LigadaAsync();

        var (status, corpo) = await EntregarAsync(await EnderecoAsync(equipe), []);

        status.Should().Be(HttpStatusCode.Accepted);
        corpo.Should().BeEmpty();
        (await GravadoAsync(equipe.TenantId)).Registros.Should().Be(0);
    }

    [Fact]
    public async Task Entregar_AcimaDoTamanhoSemTokenCerto_404NaoRevelaOLimite() // design D5 antes de D12: token é o primeiro portão
    {
        var equipe = await LigadaAsync();
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?> { ["EmailEntrada:TamanhoMaximoBytes"] = "64" });
        using var http = app.GetTestClient();

        var (status, _) = await EntregarAsync(http, await EnderecoAsync(equipe), Mime(NovoMessageId()), token: "token-errado");

        status.Should().Be(HttpStatusCode.NotFound);
        (await GravadoAsync(equipe.TenantId)).Registros.Should().Be(0);
    }

    [Fact]
    public async Task Entregar_EmPartesAcimaDoTamanhoParaDestinoDescartado_202SemLerNemGravar() // F15-05, F15-22 e D12: descarte antes de ler o corpo
    {
        var desligada = await Equipe.CriarAsync(api);
        var antes = await RegistrosEmTodasAsync();
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?> { ["EmailEntrada:TamanhoMaximoBytes"] = "64" });
        using var http = app.GetTestClient();

        var daDesligada = await EntregarAsync(http, await EnderecoAsync(desligada), Mime(NovoMessageId()), emPartes: true);
        var deNinguem = await EntregarAsync(http, "k7m2qd9w@entrada.acordu.app.br", Mime(NovoMessageId()), emPartes: true);

        daDesligada.Should().Be((HttpStatusCode.Accepted, string.Empty));
        deNinguem.Should().Be((HttpStatusCode.Accepted, string.Empty));
        (await RegistrosEmTodasAsync()).Should().Be(antes);
    }

    [Fact]
    public async Task Entregar_MesmaEntregaEmParalelo_TodasAceitasEUmRegistroSo() // design D6: a corrida cai no índice único e vira 202
    {
        var equipe = await LigadaAsync();
        var endereco = await EnderecoAsync(equipe);
        var mime = Mime(NovoMessageId());

        var respostas = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => EntregarAsync(endereco, mime)));

        respostas.Should().OnlyContain(r => r.Status == HttpStatusCode.Accepted && r.Corpo.Length == 0, "a corrida nunca vira 5xx");
        (await GravadoAsync(equipe.TenantId)).Registros.Should().Be(1);
    }

    [Fact]
    public async Task Entregar_CorpoQueNaoEEmail_202SemCorpo() // plano-2, menor 6: nunca 5xx para o servidor de origem não repetir
    {
        var equipe = await LigadaAsync();

        var (status, corpo) = await EntregarAsync(await EnderecoAsync(equipe), Encoding.UTF8.GetBytes("\0\0 isto não é um e-mail \u0001\u0002"));

        status.Should().Be(HttpStatusCode.Accepted);
        corpo.Should().BeEmpty();
    }

    /// <summary>Uma Api com o log em JSON capturado a partir de Information (a linha do desfecho da entrada, EventId 410).</summary>
    private async Task<(WebApplication App, LogsCapturados Logs)> ApiComLogAsync()
    {
        var logs = new LogsCapturados(LogLevel.Information);
        var app = await api.NovaApiAsync(
            new Dictionary<string, string?> { ["Logs:Formato"] = "json" },
            servicos: s => s.AddSingleton<ILoggerProvider>(logs));
        logs.Formatador = app.Services.GetServices<ConsoleFormatter>().OfType<FormatadorLogJson>().Single();
        return (app, logs);
    }

    private static List<string> LinhasDaEntrada(LogsCapturados logs) =>
        logs.Linhas
            .Select(l => l.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty)
            .Where(m => m.StartsWith("Entrada por e-mail:", StringComparison.Ordinal))
            .ToList();

    [Theory] // F15-85: resposta automática, aviso de entrega, mensagem em massa
    [InlineData("auto-submitted")]
    [InlineData("precedence")]
    [InlineData("lista")]
    [InlineData("x-autoreply")]
    [InlineData("aviso-de-entrega")]
    [InlineData("envelope-vazio")]
    [InlineData("mailer-daemon")]
    public async Task Entregar_EmailAutomatico_202IgnoradoSemCriarNemResponder(string caso)
    {
        var equipe = await LigadaAsync();
        var id = NovoMessageId();
        var (mime, envelope) = caso switch
        {
            "auto-submitted" => (Mime(id, cabecalhos: "Auto-Submitted: auto-replied\r\n"), Remetente),
            "precedence" => (Mime(id, cabecalhos: "Precedence: bulk\r\n"), Remetente),
            "lista" => (Mime(id, cabecalhos: "List-Id: <novidades.cliente-da-entrada.com.br>\r\nList-Unsubscribe: <mailto:sair@cliente-da-entrada.com.br>\r\n"), Remetente),
            "x-autoreply" => (Mime(id, cabecalhos: "X-Autoreply: yes\r\n"), Remetente),
            "aviso-de-entrega" => (Mime(id, tipo: "multipart/report; report-type=delivery-status; boundary=\"b\""), Remetente),
            "envelope-vazio" => (Mime(id), "<>"),
            _ => (Mime(id, de: "Mail Delivery System <MAILER-DAEMON@mx.cliente-da-entrada.com.br>"), "MAILER-DAEMON@mx.cliente-da-entrada.com.br"),
        };
        var (app, logs) = await ApiComLogAsync();
        await using var montada = app;
        using var http = app.GetTestClient();

        var (status, corpo) = await EntregarAsync(http, await EnderecoAsync(equipe), mime, envelope: envelope);

        status.Should().Be(HttpStatusCode.Accepted);
        corpo.Should().BeEmpty("a resposta não diz que foi ignorado");
        await using var db = api.DaEquipe(equipe.TenantId);
        (await db.EmailsRecebidos.AsNoTracking().SingleAsync()).Resultado.Should().Be(ResultadoEntradaEmail.Ignorado);
        (await db.Solicitacoes.CountAsync()).Should().Be(0, "nem solicitação");
        (await db.Interacoes.CountAsync()).Should().Be(0, "nem mensagem");
        api.Emails.Para(Remetente).Should().BeEmpty("nada é respondido");
        LinhasDaEntrada(logs).Should().Equal("Entrada por e-mail: Automatica.");
    }

    [Fact]
    public async Task Entregar_SemCabecalhoDeEnvelope_SegueComoEmailComum() // cabeçalho ausente não é devolução
    {
        var equipe = await LigadaAsync();
        var (app, logs) = await ApiComLogAsync();
        await using var montada = app;
        using var http = app.GetTestClient();

        await EntregarAsync(http, await EnderecoAsync(equipe), Mime(NovoMessageId()), envelope: null);

        LinhasDaEntrada(logs).Should().Equal("Entrada por e-mail: Registrada.");
    }

    [Fact]
    public async Task Entregar_AutoSubmittedNo_SegueComoEmailComum() // F15-85: "no" não é automático (RFC 3834)
    {
        var equipe = await LigadaAsync();
        var (app, logs) = await ApiComLogAsync();
        await using var montada = app;
        using var http = app.GetTestClient();

        await EntregarAsync(http, await EnderecoAsync(equipe), Mime(NovoMessageId(), cabecalhos: "Auto-Submitted: no\r\n"));

        LinhasDaEntrada(logs).Should().Equal("Entrada por e-mail: Registrada.");
    }

    [Theory] // F15-87
    [InlineData("spam")]
    [InlineData("virus")]
    public async Task Entregar_FiltroDoProvedorSpamOuVirus_DescartadoSoComAContagemNoLog(string filtro)
    {
        var equipe = await LigadaAsync();
        var endereco = await EnderecoAsync(equipe);
        var (app, logs) = await ApiComLogAsync();
        await using var montada = app;
        using var http = app.GetTestClient();

        var (status, corpo) = await EntregarAsync(http, endereco, Mime(NovoMessageId()), filtro: filtro);

        status.Should().Be(HttpStatusCode.Accepted);
        corpo.Should().BeEmpty();
        await using var db = api.DaEquipe(equipe.TenantId);
        var registro = await db.EmailsRecebidos.AsNoTracking().SingleAsync();
        registro.Resultado.Should().Be(ResultadoEntradaEmail.Ignorado, "descartado, sem triagem nem solicitação");
        (await db.Solicitacoes.CountAsync()).Should().Be(0);
        (await db.Interacoes.CountAsync()).Should().Be(0);
        api.Emails.Para(Remetente).Should().BeEmpty();
        LinhasDaEntrada(logs).Should().Equal("Entrada por e-mail: Filtrada.");
        var bruto = string.Join("\n", logs.Linhas.Select(l => l.GetRawText()));
        bruto.Should().NotContain(Remetente).And.NotContain(endereco).And.NotContain("Forno parou")
            .And.NotContain("cliente-da-entrada", "só a contagem fica no log (RNF-OPS-01)");
    }

    [Theory] // F15-88
    [InlineData("Acordu <nao-responda@send.acordu.app.br>", Remetente)] // o remetente dos avisos (Email__Remetente padrão)
    [InlineData("k7m2qd9w@entrada.acordu.app.br", Remetente)] // um endereço de entrada
    [InlineData("Ana <" + Remetente + ">", "bounces@send.acordu.app.br")] // o envelope do envio do Acordu
    public async Task Entregar_RemetenteDoProprioAcordu_IgnoradoSemResponder(string de, string envelope)
    {
        var equipe = await LigadaAsync();
        var (app, logs) = await ApiComLogAsync();
        await using var montada = app;
        using var http = app.GetTestClient();

        var (status, corpo) = await EntregarAsync(http, await EnderecoAsync(equipe), Mime(NovoMessageId(), de: de), envelope: envelope);

        status.Should().Be(HttpStatusCode.Accepted);
        corpo.Should().BeEmpty();
        (await GravadoAsync(equipe.TenantId)).Should().Be((1, 0, 0), "só o registro do descarte");
        await using (var db = api.DaEquipe(equipe.TenantId))
            (await db.EmailsRecebidos.AsNoTracking().SingleAsync()).Resultado.Should().Be(ResultadoEntradaEmail.Ignorado);
        api.Emails.Para("nao-responda@send.acordu.app.br").Should().BeEmpty("nada é respondido: sem laço");
        api.Emails.Para("k7m2qd9w@entrada.acordu.app.br").Should().BeEmpty();
        LinhasDaEntrada(logs).Should().Equal("Entrada por e-mail: Laco.");
    }

    [Fact] // F15-108: o diário apaga o registro vencido, em empresa gravável ou só leitura
    public async Task Diario_RegistroComMaisDe7Dias_ApagadoNaGravavelENaSoLeitura()
    {
        var gravavel = await LigadaAsync();
        var soLeitura = await LigadaAsync();
        var inicio = api.Relogio.Agora;
        JsonElement resultado;
        try
        {
            await EntregarAsync(await EnderecoAsync(gravavel), Mime(NovoMessageId()));
            await EntregarAsync(await EnderecoAsync(soLeitura), Mime(NovoMessageId()));
            api.Relogio.Agora = inicio.AddDays(3);
            await EntregarAsync(await EnderecoAsync(gravavel), Mime(NovoMessageId()));
            await SituacaoAsync(soLeitura.TenantId, "VENCIDA");
            (await GravadoAsync(gravavel.TenantId)).Registros.Should().Be(2);
            (await GravadoAsync(soLeitura.TenantId)).Registros.Should().Be(1);

            // 8 dias depois do primeiro: os de 8 dias passaram da retenção (7); o de 5 dias fica.
            api.Relogio.Agora = inicio.AddDays(8);
            using var http = api.NovoHttp();
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/diario", UriKind.Relative));
            requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
            using var resposta = await http.SendAsync(requisicao);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            resultado = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }

        resultado.GetProperty("emailsRecebidosRemovidos").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        (await GravadoAsync(gravavel.TenantId)).Registros.Should().Be(1, "o de 5 dias fica");
        (await GravadoAsync(soLeitura.TenantId)).Registros.Should().Be(0, "o expurgo por prazo segue na empresa só leitura");
    }
}
