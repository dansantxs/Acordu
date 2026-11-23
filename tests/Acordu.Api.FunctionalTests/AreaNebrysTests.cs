using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14k (F14-61 a F14-63; ADR 0014, design D4): a sessão de operação da Nebrys, sem tenant, abre a lista e o
/// detalhe das empresas só com os campos da relação; qualquer outra sessão, ou ninguém, recebe o 404 de uma rota
/// inexistente em toda a área, e a sessão de operação recebe 404 fora dela.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed partial class AreaNebrysTests(ApiFixture api)
{
    private const string Area = "/api/v1/nebrys";
    private static readonly TimeSpan PrazoAteExpurgo = TimeSpan.FromDays(30);

    // ---------- F14-61: lista ----------

    [Fact]
    public async Task ListarEmpresas_Operador_VeOsCamposDaRelacaoDeCadaEmpresa() // F14-61
    {
        var criadaEm = api.Relogio.Agora;
        var equipe = await Equipe.CriarAsync(api);
        await equipe.ComPapelAsync(Papel.NomeMembro);
        await ContatosDoPortal.CriarAsync(api, equipe, await Cenarios.ClienteAsync(equipe));
        var token = await OperadorNebrys.TokenAsync(api);

        var empresa = await NaListaAsync(token, equipe.TenantId);

        string documento;
        await using (var db = api.DaEquipe(equipe.TenantId))
            documento = (await db.Tenants.AsNoTracking().SingleAsync()).Documento!;
        empresa.GetProperty("nome").GetString().Should().Be("Estúdio Traço");
        empresa.GetProperty("documento").GetString().Should().Be(documento);
        empresa.GetProperty("situacao").GetString().Should().Be("TESTE");
        empresa.GetProperty("criadaEm").GetDateTimeOffset().Should().Be(criadaEm);
        empresa.GetProperty("fimDoTeste").GetDateTimeOffset().Should().Be(criadaEm + TimeSpan.FromDays(30));
        empresa.GetProperty("somenteLeituraDesde").ValueKind.Should().Be(JsonValueKind.Null);
        empresa.GetProperty("canceladaEm").ValueKind.Should().Be(JsonValueKind.Null);
        empresa.GetProperty("expurgoPrevistoEm").ValueKind.Should().Be(JsonValueKind.Null);
        empresa.GetProperty("criador").GetProperty("nome").GetString().Should().Be("Pessoa da equipe");
        empresa.GetProperty("criador").GetProperty("email").GetString().Should().Be(equipe.EmailDono);
        // Dono e Membro; o contato do portal não é da equipe.
        empresa.GetProperty("pessoasNaEquipe").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task ListarEmpresas_VencidaECancelada_TrazADataDoVencimentoOuCancelamentoEOExpurgoPrevisto() // F14-61
    {
        var vencida = await Equipe.CriarAsync(api);
        var cancelada = await Equipe.CriarAsync(api);
        var vencimento = api.Relogio.Agora - TimeSpan.FromDays(3);
        var cancelamento = api.Relogio.Agora - TimeSpan.FromDays(1);
        await GravarSituacaoAsync(vencida.TenantId, "VENCIDA", vencimento, null);
        await GravarSituacaoAsync(cancelada.TenantId, "CANCELADA", cancelamento, cancelamento);
        var token = await OperadorNebrys.TokenAsync(api);

        var linhaVencida = await NaListaAsync(token, vencida.TenantId);
        var linhaCancelada = await NaListaAsync(token, cancelada.TenantId);

        linhaVencida.GetProperty("situacao").GetString().Should().Be("VENCIDA");
        linhaVencida.GetProperty("somenteLeituraDesde").GetDateTimeOffset().Should().Be(vencimento);
        linhaVencida.GetProperty("canceladaEm").ValueKind.Should().Be(JsonValueKind.Null);
        linhaVencida.GetProperty("expurgoPrevistoEm").GetDateTimeOffset().Should().Be(vencimento + PrazoAteExpurgo);
        linhaCancelada.GetProperty("situacao").GetString().Should().Be("CANCELADA");
        linhaCancelada.GetProperty("canceladaEm").GetDateTimeOffset().Should().Be(cancelamento);
        linhaCancelada.GetProperty("expurgoPrevistoEm").GetDateTimeOffset().Should().Be(cancelamento + PrazoAteExpurgo);
    }

    [Fact]
    public async Task ListarEmpresas_FiltroPorSituacao_SoAsDaSituacao() // F14-61
    {
        var emTeste = await Equipe.CriarAsync(api);
        var vencida = await Equipe.CriarAsync(api);
        await GravarSituacaoAsync(vencida.TenantId, "VENCIDA", api.Relogio.Agora, null);
        var token = await OperadorNebrys.TokenAsync(api);

        var vencidas = await TodasAsync(token, "?situacao=VENCIDA");
        var emTesteMinusculo = await TodasAsync(token, "?situacao=teste");

        vencidas.Should().OnlyContain(e => e.GetProperty("situacao").GetString() == "VENCIDA");
        vencidas.Select(Cenarios.Id).Should().Contain(vencida.TenantId).And.NotContain(emTeste.TenantId);
        emTesteMinusculo.Should().OnlyContain(e => e.GetProperty("situacao").GetString() == "TESTE");
        emTesteMinusculo.Select(Cenarios.Id).Should().Contain(emTeste.TenantId).And.NotContain(vencida.TenantId);
        using var invalida = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Get, $"{Area}/empresas?situacao=SUMIDA", token);
        invalida.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListarEmpresas_Paginada_TotalEPaginasDoContrato() // F14-61
    {
        await Equipe.CriarAsync(api);
        await Equipe.CriarAsync(api);
        var token = await OperadorNebrys.TokenAsync(api);

        var primeira = await JsonAsync(token, $"{Area}/empresas?pagina=1&tamanhoPagina=1");
        var segunda = await JsonAsync(token, $"{Area}/empresas?pagina=2&tamanhoPagina=1");

        primeira.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        primeira.GetProperty("tamanhoPagina").GetInt32().Should().Be(1);
        primeira.GetProperty("itens").GetArrayLength().Should().Be(1);
        Cenarios.Id(segunda.GetProperty("itens")[0]).Should().NotBe(Cenarios.Id(primeira.GetProperty("itens")[0]));
        // Criadas por último primeiro.
        segunda.GetProperty("itens")[0].GetProperty("criadaEm").GetDateTimeOffset()
            .Should().BeOnOrBefore(primeira.GetProperty("itens")[0].GetProperty("criadaEm").GetDateTimeOffset());
    }

    // ---------- F14-62: detalhe sem dado de negócio ----------

    [Fact]
    public async Task DetalheEmpresa_ComDadosDeNegocio_SoOsCamposDaRelacaoEAVersao() // F14-62
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe, "Padaria Segredo do Prestador");
        await Cenarios.SolicitacaoAsync(equipe, cliente);
        await Cenarios.TarefaAsync(equipe, titulo: "Tarefa secreta do prestador");
        var token = await OperadorNebrys.TokenAsync(api);

        var (status, texto) = await TextoAsync(token, $"{Area}/empresas/{equipe.TenantId}");

        status.Should().Be(HttpStatusCode.OK);
        var detalhe = JsonDocument.Parse(texto).RootElement;
        detalhe.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
        [
            "id", "nome", "documento", "situacao", "criadaEm", "fimDoTeste", "somenteLeituraDesde", "canceladaEm",
            "expurgoPrevistoEm", "criador", "pessoasNaEquipe", "versao",
        ]);
        detalhe.GetProperty("criador").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["nome", "email"]);
        texto.Should().NotContain("Padaria Segredo do Prestador").And.NotContain("Tarefa secreta").And.NotContain("banner");
        Cenarios.Id(detalhe).Should().Be(equipe.TenantId);
    }

    [Fact] // D5: a versão do detalhe é a do Tenant e muda com ele (o If-Match das ações do F14l)
    public async Task DetalheEmpresa_TenantMuda_VersaoMuda()
    {
        var equipe = await Equipe.CriarAsync(api);
        var token = await OperadorNebrys.TokenAsync(api);
        var antes = (await JsonAsync(token, $"{Area}/empresas/{equipe.TenantId}")).GetProperty("versao").GetString();

        await GravarSituacaoAsync(equipe.TenantId, "VENCIDA", api.Relogio.Agora, null);
        var depois = (await JsonAsync(token, $"{Area}/empresas/{equipe.TenantId}")).GetProperty("versao").GetString();

        antes.Should().MatchRegex("^[0-9]+$");
        depois.Should().MatchRegex("^[0-9]+$").And.NotBe(antes);
    }

    [Fact]
    public async Task DetalheEmpresa_Inexistente_404()
    {
        var token = await OperadorNebrys.TokenAsync(api);

        var (status, _) = await TextoAsync(token, $"{Area}/empresas/{Guid.NewGuid()}");

        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory] // F14-62: a sessão de operação não consegue pedir dado de negócio por nenhuma rota
    [InlineData("GET", "/api/v1/clientes")]
    [InlineData("GET", "/api/v1/clientes/{cliente}")]
    [InlineData("GET", "/api/v1/clientes/{cliente}/arquivos")]
    [InlineData("GET", "/api/v1/solicitacoes")]
    [InlineData("GET", "/api/v1/tarefas")]
    [InlineData("GET", "/api/v1/apontamentos")]
    [InlineData("GET", "/api/v1/contratos")]
    [InlineData("GET", "/api/v1/dashboard")]
    [InlineData("GET", "/api/v1/auditoria")]
    [InlineData("GET", "/api/v1/auth/eu")]
    [InlineData("GET", "/api/v1/portal/eu")]
    [InlineData("GET", "/api/v1/portal/solicitacoes")]
    [InlineData("POST", "/api/v1/clientes")]
    [InlineData("POST", "/api/v1/sugestoes")]
    [InlineData("GET", "/api/v1/nebrys/empresas/{tenant}/clientes")]
    [InlineData("GET", "/api/v1/nebrys/empresas/{tenant}/solicitacoes")]
    public async Task RotasDeNegocio_SessaoDeOperacao_404(string metodo, string modelo)
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe, "Cliente que a Nebrys não vê");
        var token = await OperadorNebrys.TokenAsync(api);
        var rota = modelo.Replace("{cliente}", cliente.ToString(), StringComparison.Ordinal)
            .Replace("{tenant}", equipe.TenantId.ToString(), StringComparison.Ordinal);

        using var resposta = await OperadorNebrys.EnviarAsync(
            api.NovoHttp(), new HttpMethod(metodo), rota, token,
            metodo == "POST" ? JsonContent.Create(new { nome = "Criado pela operação", texto = "Sugestão da operação" }) : null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain("Cliente que a Nebrys não vê");
        (await ClientesAsync(equipe.TenantId)).Should().Be(1, "nada foi criado pela sessão de operação");
    }

    [Fact] // F14-62 e ADR 0014, item 4: o documento da área só tem as rotas dela, e o interno não as tem
    public async Task DocumentoNebrys_SoAsRotasDaArea()
    {
        using var http = api.NovoHttp();

        var nebrys = JsonDocument.Parse(await http.GetStringAsync(new Uri("/openapi/nebrys.json", UriKind.Relative))).RootElement;
        var interno = await http.GetStringAsync(new Uri("/openapi/interno.json", UriKind.Relative));

        nebrys.GetProperty("paths").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            [
                "/api/v1/nebrys/sessao", "/api/v1/nebrys/empresas", "/api/v1/nebrys/empresas/{id}",
                "/api/v1/nebrys/empresas/{id}/ativacao", "/api/v1/nebrys/empresas/{id}/prorrogacao",
                "/api/v1/nebrys/empresas/{id}/cancelamento", "/api/v1/nebrys/empresas/{id}/reativacao",
                "/api/v1/nebrys/documentos/liberacao",
                "/api/v1/nebrys/convites", "/api/v1/nebrys/convites/{id}",
                "/api/v1/nebrys/convites/{id}/reenvio", "/api/v1/nebrys/convites/{id}/revogacao",
            ]);
        interno.Should().NotContain("/api/v1/nebrys");
    }

    // ---------- F14-63: 404 para quem não é a operação ----------

    public static TheoryData<string, string> RotasDaArea => new()
    {
        { "GET", "/api/v1/nebrys/empresas" },
        { "GET", "/api/v1/nebrys/empresas?situacao=TESTE" },
        { "GET", "/api/v1/nebrys/empresas/{tenant}" },
        { "DELETE", "/api/v1/nebrys/empresas/{tenant}" },
        { "POST", "/api/v1/nebrys/empresas/{tenant}/ativacao" },
        { "POST", "/api/v1/nebrys/empresas/{tenant}/prorrogacao" },
        { "POST", "/api/v1/nebrys/empresas/{tenant}/cancelamento" },
        { "POST", "/api/v1/nebrys/empresas/{tenant}/reativacao" },
        { "POST", "/api/v1/nebrys/documentos/liberacao" },
        { "GET", "/api/v1/nebrys/documentos/liberacao" },
        { "GET", "/api/v1/nebrys/sessao" },
        // F14n: convites de empresa. O {tenant} é só um Guid qualquer para o {id} do convite.
        { "GET", "/api/v1/nebrys/convites" },
        { "POST", "/api/v1/nebrys/convites" },
        { "GET", "/api/v1/nebrys/convites/{tenant}" },
        { "POST", "/api/v1/nebrys/convites/{tenant}/reenvio" },
        { "POST", "/api/v1/nebrys/convites/{tenant}/revogacao" },
        { "GET", "/api/v1/nebrys" },
        { "GET", "/api/v1/nebrys/qualquer-coisa" },
        { "GET", "/API/V1/NEBRYS/EMPRESAS" },
        { "PUT", "/api/v1/nebrys/empresas/{tenant}" },
        { "PATCH", "/api/v1/nebrys/empresas/{tenant}" },
        { "HEAD", "/api/v1/nebrys/empresas" },
        { "OPTIONS", "/api/v1/nebrys/empresas" },
        { "POST", "/api/v1/nebrys/empresas" },
        { "POST", "/api/v1/nebrys/sessao/" },
        { "GET", "/Api/V1/Nebrys/Empresas/{tenant}" },
    };

    [Theory]
    [MemberData(nameof(RotasDaArea))]
    public async Task RotaDaArea_DonoContatoLinkOuAnonimo_404IgualARotaInexistente(string metodo, string modelo) // F14-63
    {
        var (equipe, sessoes) = await SessoesAsync();
        var rota = modelo.Replace("{tenant}", equipe.TenantId.ToString(), StringComparison.Ordinal);

        foreach (var (nome, token) in sessoes)
        {
            var inexistente = await RespostaAsync(metodo, "/api/v1/rota-que-nao-existe", token);
            var area = await RespostaAsync(metodo, rota, token);

            area.Status.Should().Be(HttpStatusCode.NotFound, "sessão {0} em {1} {2}", nome, metodo, rota);
            area.Should().BeEquivalentTo(inexistente, "a área não pode se distinguir de uma rota inexistente ({0})", nome);
        }
    }

    [Fact] // F14-63: a abertura da sessão pedida por qualquer outra sessão também é o 404 de rota inexistente
    public async Task Sessao_PedidaPorOutraSessao_404IgualARotaInexistente()
    {
        await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);
        var (_, sessoes) = await SessoesAsync();
        var inexistente = await RespostaAsync("POST", "/api/v1/rota-que-nao-existe", null);

        foreach (var (nome, token) in sessoes)
        {
            using var resposta = await OperadorNebrys.EnviarAsync(
                api.NovoHttp(), HttpMethod.Post, $"{Area}/sessao", token,
                JsonContent.Create(new { email = "naoexiste-xyz@nebrys.test", senha = OperadorNebrys.Senha }));

            (await ResumoAsync(resposta)).Should().BeEquivalentTo(inexistente, nome);
        }
    }

    [Fact] // F14-61: cada situação traz o criador (nome e e-mail); ATIVA sem fim do teste nem expurgo
    public async Task ListarEmpresas_AtivaECancelada_TrazemOCriador() // F14-61
    {
        var ativa = await Equipe.CriarAsync(api);
        var cancelada = await Equipe.CriarAsync(api);
        await GravarSituacaoAsync(ativa.TenantId, "ATIVA", null, null);
        await GravarSituacaoAsync(cancelada.TenantId, "CANCELADA", api.Relogio.Agora, api.Relogio.Agora);
        var token = await OperadorNebrys.TokenAsync(api);

        foreach (var (equipe, situacao) in new[] { (ativa, "ATIVA"), (cancelada, "CANCELADA") })
        {
            var linha = await NaListaAsync(token, equipe.TenantId);
            linha.GetProperty("situacao").GetString().Should().Be(situacao);
            linha.GetProperty("criador").GetProperty("nome").GetString().Should().Be("Pessoa da equipe");
            linha.GetProperty("criador").GetProperty("email").GetString().Should().Be(equipe.EmailDono);
            linha.GetProperty("pessoasNaEquipe").GetInt32().Should().Be(1);
        }

        var linhaAtiva = await NaListaAsync(token, ativa.TenantId);
        linhaAtiva.GetProperty("expurgoPrevistoEm").ValueKind.Should().Be(JsonValueKind.Null);
        linhaAtiva.GetProperty("canceladaEm").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact] // ADR 0014, item 6: cada recusa grava o motivo na trilha sem tenant
    public async Task Sessao_Recusas_GravamOMotivoNaTrilhaSemTenant()
    {
        await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);
        await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.EmailSemConfirmar, confirmado: false);
        var desconhecido = $"ninguem-{Guid.NewGuid():N}@nebrys.test";

        using var a = await OperadorNebrys.SessaoAsync(api.NovoHttp(), desconhecido, OperadorNebrys.Senha);
        using var b = await OperadorNebrys.SessaoAsync(api.NovoHttp(), OperadorNebrys.EmailSemConfirmar, OperadorNebrys.Senha);
        using var c = await OperadorNebrys.SessaoAsync(api.NovoHttp(), OperadorNebrys.Email, "senha-errada-123");

        new[] { a, b, c }.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NotFound);
        (await MotivosDasFalhasAsync(desconhecido)).Should().Contain("email-desconhecido");
        (await MotivosDasFalhasAsync(OperadorNebrys.EmailSemConfirmar)).Should().Contain("email-nao-confirmado");
        (await MotivosDasFalhasAsync(OperadorNebrys.Email)).Should().Contain("senha");
    }

    [Fact]
    public async Task Sessao_CredencialDeDonoErradaOuMalformada_404IgualARotaInexistente() // F14-63
    {
        await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);
        await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.EmailSemConfirmar, confirmado: false);
        var equipe = await Equipe.CriarAsync(api);
        var inexistente = await RespostaAsync("POST", "/api/v1/rota-que-nao-existe", null);
        var tentativas = new (string Nome, HttpContent Corpo)[]
        {
            ("Dono com a senha dele", JsonContent.Create(new { email = equipe.EmailDono, senha = Equipe.Senha })),
            ("operador com a senha errada", JsonContent.Create(new { email = OperadorNebrys.Email, senha = "senha-errada-123" })),
            ("e-mail sem conta", JsonContent.Create(new { email = "ninguem@nebrys.test", senha = OperadorNebrys.Senha })),
            ("operador com o e-mail não confirmado", JsonContent.Create(new { email = OperadorNebrys.EmailSemConfirmar, senha = OperadorNebrys.Senha })),
            ("corpo vazio", JsonContent.Create(new { })),
            ("JSON quebrado", new StringContent("{ \"email\": ", Encoding.UTF8, "application/json")),
            ("sem JSON", new StringContent("email=x", Encoding.UTF8, "application/x-www-form-urlencoded")),
        };

        foreach (var (nome, corpo) in tentativas)
        {
            using var resposta = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Post, $"{Area}/sessao", equipe.Dono, corpo);
            var obtida = await ResumoAsync(resposta);

            obtida.Status.Should().Be(HttpStatusCode.NotFound, nome);
            obtida.Should().BeEquivalentTo(inexistente, nome);
            resposta.Headers.Contains("Set-Cookie").Should().BeFalse(nome);
        }

        // A recusa vai para a trilha sem tenant, com o motivo (ADR 0014, item 6); a resposta não o diz.
        var motivos = await MotivosDasFalhasAsync(equipe.EmailDono);
        motivos.Should().Contain("nao-operador");
    }

    [Fact]
    public async Task Sessao_Operador_TokenDeTrintaMinutosSemTenantSemRefreshNaTrilha() // F14-63 (o outro lado) e ADR 0014, item 2
    {
        var operador = await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);

        using var resposta = await OperadorNebrys.SessaoAsync(api.NovoHttp(), OperadorNebrys.Email.ToUpperInvariant(), OperadorNebrys.Senha);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.Headers.Contains("Set-Cookie").Should().BeFalse("a sessão de operação não tem refresh nem cookie");
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("tipo").GetString().Should().Be("OPERACAO");
        corpo.GetProperty("expiraEm").GetDateTimeOffset().Should().Be(api.Relogio.Agora + TimeSpan.FromMinutes(30));
        var claims = Claims(corpo.GetProperty("accessToken").GetString()!);
        claims.GetProperty("sub").GetString().Should().Be(operador.ToString());
        claims.GetProperty("tipo").GetString().Should().Be("OPERACAO");
        claims.TryGetProperty("tenant_id", out _).Should().BeFalse("a sessão de operação não pertence a um tenant");
        claims.TryGetProperty("permissoes", out _).Should().BeFalse();
        await using var db = api.DaEquipe(Guid.NewGuid());
        var registros = await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM auditoria
            WHERE acao = 'nebrys.sessao' AND tenant_id IS NULL AND usuario_id = {operador}
            """).SingleAsync();
        registros.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Sessao_TokenExpirado_404()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var inicio = api.Relogio.Agora;
        try
        {
            api.Relogio.Agora = inicio + TimeSpan.FromMinutes(31);

            var (status, _) = await TextoAsync(token, $"{Area}/empresas");

            status.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }
    }

    [Fact] // ADR 0014, item 1: sem a variável, ninguém é operador e a área inteira responde 404
    public async Task Sessao_SemOperadoresConfigurados_AreaInteira404()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        await using var semOperadores = await api.NovaApiAsync(new Dictionary<string, string?> { ["Nebrys:Operadores"] = "" });

        using var sessao = await OperadorNebrys.SessaoAsync(semOperadores.GetTestClient(), OperadorNebrys.Email, OperadorNebrys.Senha);
        using var lista = await OperadorNebrys.EnviarAsync(semOperadores.GetTestClient(), HttpMethod.Get, $"{Area}/empresas", token);

        sessao.StatusCode.Should().Be(HttpStatusCode.NotFound);
        lista.StatusCode.Should().Be(HttpStatusCode.NotFound, "o token antigo não vale sem o operador na lista");
    }

    [Fact] // Tirar alguém da lista vale na revisão seguinte, sem esperar o token expirar
    public async Task Sessao_OperadorTiradoDaLista_TokenAntigoRecebe404()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        await using var outraLista = await api.NovaApiAsync(
            new Dictionary<string, string?> { ["Nebrys:Operadores"] = "outra-pessoa@nebrys.test" });

        using var lista = await OperadorNebrys.EnviarAsync(outraLista.GetTestClient(), HttpMethod.Get, $"{Area}/empresas", token);

        lista.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // ADR 0014, item 2: o bloqueio do Identity vale; com a conta bloqueada, nem a senha certa abre
    public async Task Sessao_CincoSenhasErradas_BloqueiaEASenhaCertaRecebe404()
    {
        var email = $"operacao-{Guid.NewGuid():N}@nebrys.test";
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?> { ["Nebrys:Operadores"] = email });
        await OperadorNebrys.ContaAsync(app, email);

        for (var i = 0; i < 5; i++)
        {
            using var errada = await OperadorNebrys.SessaoAsync(app.GetTestClient(), email, "senha-errada-123");
            errada.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        using var certa = await OperadorNebrys.SessaoAsync(app.GetTestClient(), email, OperadorNebrys.Senha);
        certa.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MotivosDasFalhasAsync(email)).Should().Contain("bloqueado");
    }

    [Fact] // ADR 0014, item 2: o limite de /auth vale, e o estouro responde 404 na área (o 429 revelaria a rota)
    public async Task Sessao_LimiteDeAuthEstourado_404()
    {
        await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);
        await using var app = await api.NovaApiAsync(new Dictionary<string, string?> { ["RateLimit:Auth:PorMinuto"] = "1" });

        using var primeira = await OperadorNebrys.SessaoAsync(app.GetTestClient(), OperadorNebrys.Email, OperadorNebrys.Senha);
        using var segunda = await OperadorNebrys.SessaoAsync(app.GetTestClient(), OperadorNebrys.Email, OperadorNebrys.Senha);
        using var http = app.GetTestClient();
        using var login = await http.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative), new { email = OperadorNebrys.Email, senha = OperadorNebrys.Senha });

        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        segunda.StatusCode.Should().Be(HttpStatusCode.NotFound);
        login.StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "o limite é o mesmo das rotas de /auth");
    }

    // ---------- apoio ----------

    private sealed record Resumo(HttpStatusCode Status, string? Tipo, string? Titulo, bool TemAllow, string? ContentType);

    /// <summary>Uma empresa e as sessões que não são de operação: Dono, contato com conta, sessão de link e ninguém.</summary>
    private async Task<(Equipe Equipe, List<(string Nome, string? Token)> Sessoes)> SessoesAsync()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);
        var semConta = Cenarios.Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos",
            new { nome = "Contato do link", email = $"link-{Guid.NewGuid():N}@bomgrao.com.br" }));
        var emitido = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal", new { contatoId = semConta });
        using var http = api.NovoHttp();
        using var troca = await http.PostAsJsonAsync(
            new Uri("/api/v1/auth/portal/link", UriKind.Relative),
            new { token = TokenDoLink().Match(emitido.GetProperty("link").GetString()!).Groups[1].Value });
        troca.StatusCode.Should().Be(HttpStatusCode.OK);
        var link = (await troca.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        return (equipe, [("Dono", equipe.Dono), ("Membro", membro), ("contato do portal", contato.Token), ("link", link), ("anônimo", null)]);
    }

    private async Task<Resumo> RespostaAsync(string metodo, string rota, string? token)
    {
        using var resposta = await OperadorNebrys.EnviarAsync(api.NovoHttp(), new HttpMethod(metodo), rota, token);
        return await ResumoAsync(resposta);
    }

    private static async Task<Resumo> ResumoAsync(HttpResponseMessage resposta)
    {
        var texto = await resposta.Content.ReadAsStringAsync();
        var json = texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement;
        return new Resumo(
            resposta.StatusCode,
            json.ValueKind == JsonValueKind.Object && json.TryGetProperty("type", out var tipo) ? tipo.GetString() : null,
            json.ValueKind == JsonValueKind.Object && json.TryGetProperty("title", out var titulo) ? titulo.GetString() : null,
            resposta.Content.Headers.Allow.Count > 0,
            resposta.Content.Headers.ContentType?.MediaType);
    }

    private async Task<JsonElement> JsonAsync(string token, string rota)
    {
        var (status, texto) = await TextoAsync(token, rota);
        status.Should().Be(HttpStatusCode.OK, "corpo: {0}", texto);
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    private async Task<(HttpStatusCode Status, string Texto)> TextoAsync(string token, string rota)
    {
        using var resposta = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Get, rota, token);
        return (resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
    }

    /// <summary>Todas as páginas da lista: a coleção compartilha o banco com as empresas dos outros testes.</summary>
    private async Task<List<JsonElement>> TodasAsync(string token, string filtro = "")
    {
        var itens = new List<JsonElement>();
        var separador = filtro.Length == 0 ? "?" : "&";
        for (var pagina = 1; ; pagina++)
        {
            var resultado = await JsonAsync(token, $"{Area}/empresas{filtro}{separador}pagina={pagina}&tamanhoPagina=200");
            var desta = resultado.GetProperty("itens").EnumerateArray().ToList();
            itens.AddRange(desta);
            if (desta.Count == 0 || itens.Count >= resultado.GetProperty("total").GetInt32())
                return itens;
        }
    }

    private async Task<JsonElement> NaListaAsync(string token, Guid tenantId) =>
        (await TodasAsync(token)).Single(e => Cenarios.Id(e) == tenantId);

    private async Task GravarSituacaoAsync(Guid tenantId, string situacao, DateTimeOffset? somenteLeituraDesde, DateTimeOffset? canceladaEm)
    {
        await using var db = api.DaEquipe(tenantId);
        await db.Database.ExecuteSqlAsync($"""
            UPDATE tenants SET situacao = {situacao}, somente_leitura_desde = {somenteLeituraDesde}, cancelada_em = {canceladaEm}
            WHERE id = {tenantId}
            """);
    }

    private async Task<int> ClientesAsync(Guid tenantId)
    {
        await using var db = api.DaEquipe(tenantId);
        return await db.Clientes.CountAsync();
    }

    private async Task<List<string>> MotivosDasFalhasAsync(string email)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        var dados = await db.Database.SqlQuery<string>($"""
            SELECT dados::text AS "Value" FROM auditoria
            WHERE acao = 'nebrys.sessao.falha' AND tenant_id IS NULL AND dados ->> 'email' = {email}
            """).ToListAsync();
        return dados.Select(d => JsonDocument.Parse(d).RootElement.GetProperty("motivo").GetString()!).ToList();
    }

    private static JsonElement Claims(string jwt)
    {
        var meio = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        meio = meio.PadRight(meio.Length + ((4 - (meio.Length % 4)) % 4), '=');
        return JsonDocument.Parse(Convert.FromBase64String(meio)).RootElement.Clone();
    }

    [GeneratedRegex(@"/portal/l/([A-Za-z0-9_-]{43})$")]
    private static partial Regex TokenDoLink();
}
