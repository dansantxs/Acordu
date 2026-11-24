using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Application.Auditoria;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14-70 (F14p; design D14; ADR 0014, item 6; ADR 0008): toda ação da Nebrys na área (situação, convite,
/// lista de espera, liberação de documento) fica na trilha sem tenant, com quem (o operador), quando, a empresa ou o e-mail
/// afetado e o antes e o depois. A conferência cobre todas as ações: uma ação <c>nebrys.*</c> ou uma rota de ação nova da
/// área sem caso aqui quebra os testes de cobertura.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class TrilhaNebrysTests(ApiFixture api)
{
    private const string Area = "/api/v1/nebrys";
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>As ações da área que mudam algo, cada uma com a rota que a faz (a sessão de operação não é ação sobre alvo).</summary>
    public static TheoryData<string> Acoes => new()
    {
        AcoesAuditoria.NebrysEmpresaAtivacao,
        AcoesAuditoria.NebrysEmpresaProrrogacao,
        AcoesAuditoria.NebrysEmpresaCancelamento,
        AcoesAuditoria.NebrysEmpresaReativacao,
        AcoesAuditoria.NebrysDocumentoLiberacao,
        AcoesAuditoria.NebrysConviteEmissao,
        AcoesAuditoria.NebrysConviteReenvio,
        AcoesAuditoria.NebrysConviteRevogacao,
        AcoesAuditoria.NebrysListaConvite,
        AcoesAuditoria.NebrysListaDescarte,
    };

    /// <summary>A rota (método e caminho do OpenAPI da área) de cada ação conferida aqui.</summary>
    private static readonly Dictionary<string, string> RotaDaAcao = new(StringComparer.Ordinal)
    {
        [AcoesAuditoria.NebrysEmpresaAtivacao] = "POST /api/v1/nebrys/empresas/{id}/ativacao",
        [AcoesAuditoria.NebrysEmpresaProrrogacao] = "POST /api/v1/nebrys/empresas/{id}/prorrogacao",
        [AcoesAuditoria.NebrysEmpresaCancelamento] = "POST /api/v1/nebrys/empresas/{id}/cancelamento",
        [AcoesAuditoria.NebrysEmpresaReativacao] = "POST /api/v1/nebrys/empresas/{id}/reativacao",
        [AcoesAuditoria.NebrysDocumentoLiberacao] = "POST /api/v1/nebrys/documentos/liberacao",
        [AcoesAuditoria.NebrysConviteEmissao] = "POST /api/v1/nebrys/convites",
        [AcoesAuditoria.NebrysConviteReenvio] = "POST /api/v1/nebrys/convites/{id}/reenvio",
        [AcoesAuditoria.NebrysConviteRevogacao] = "POST /api/v1/nebrys/convites/{id}/revogacao",
        [AcoesAuditoria.NebrysListaConvite] = "POST /api/v1/nebrys/lista-espera/{id}/convite",
        [AcoesAuditoria.NebrysListaDescarte] = "DELETE /api/v1/nebrys/lista-espera/{id}",
    };

    // ---------- F14-70: cada ação, na trilha ----------

    [Theory]
    [MemberData(nameof(Acoes))]
    public async Task Acao_Feita_TrilhaSemTenantComOperadorInstanteAfetadoEAntesEDepois(string acao) // F14-70
    {
        var operador = await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);
        var token = await OperadorNebrys.TokenAsync(api);
        var alvo = await PrepararAsync(acao, token);
        var antes = await IdsDaTrilhaAsync();

        await FazerAsync(acao, token, alvo);

        var novas = (await TrilhaAsync()).Where(l => !antes.Contains(l.Id)).ToList();
        var linha = novas.Should().ContainSingle(l => l.Acao == acao, "a ação deixa a sua linha").Which;
        linha.TenantId.Should().BeNull("a trilha da área fica fora do alcance de qualquer empresa");
        linha.UsuarioId.Should().Be(operador, "quem fez é o operador da sessão");
        linha.OcorridoEm.Should().Be(api.Relogio.Agora, "quando");
        var dados = JsonDocument.Parse(linha.Dados ?? "{}").RootElement;
        dados.TryGetProperty("antes", out _).Should().BeTrue("o antes, nulo quando o alvo não existia ({0})", linha.Dados);
        dados.TryGetProperty("depois", out _).Should().BeTrue("o depois, nulo quando o alvo deixou de existir ({0})", linha.Dados);
        ConferirAfetado(acao, alvo, linha, dados);
        novas.Should().OnlyContain(
            l => l.UsuarioId == operador && l.TenantId == null,
            "a ação não deixa outra linha fora da trilha da operação");
    }

    [Fact] // F14-70: o convite a partir da lista deixa também a emissão do convite, no mesmo formato
    public async Task ConviteDaLista_Feito_EmissaoDoConviteTambemNaTrilha()
    {
        var operador = await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);
        var token = await OperadorNebrys.TokenAsync(api);
        var alvo = await PrepararAsync(AcoesAuditoria.NebrysListaConvite, token);
        var antes = await IdsDaTrilhaAsync();

        await FazerAsync(AcoesAuditoria.NebrysListaConvite, token, alvo);

        var emissao = (await TrilhaAsync()).Where(l => !antes.Contains(l.Id))
            .Should().ContainSingle(l => l.Acao == AcoesAuditoria.NebrysConviteEmissao).Which;
        emissao.UsuarioId.Should().Be(operador);
        emissao.AlvoTipo.Should().Be("convite");
        var dados = JsonDocument.Parse(emissao.Dados!).RootElement;
        dados.GetProperty("email").GetString().Should().Be(alvo.Email);
        dados.GetProperty("entradaListaEspera").GetGuid().Should().Be(alvo.Id);
        dados.GetProperty("antes").ValueKind.Should().Be(JsonValueKind.Null);
        dados.GetProperty("depois").GetProperty("situacao").GetString().Should().Be("PENDENTE");
    }

    [Fact] // F14-70: a ação recusada não deixa linha
    public async Task Acao_Recusada_NadaNaTrilha()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var alvo = await PrepararAsync(AcoesAuditoria.NebrysEmpresaReativacao, token);
        await FazerAsync(AcoesAuditoria.NebrysEmpresaReativacao, token, alvo);
        var antes = await IdsDaTrilhaAsync();

        using var resposta = await PostAsync(token, $"{Area}/empresas/{alvo.Id}/reativacao");

        ((int)resposta.StatusCode).Should().Be(422, "a empresa já está ativa");
        (await TrilhaAsync()).Where(l => !antes.Contains(l.Id)).Should().BeEmpty();
    }

    // ---------- F14-70: todas as ações da área estão conferidas ----------

    [Fact]
    public void Acoes_TodaAcaoNebrysDaAuditoria_TemCasoNaConferencia()
    {
        var declaradas = typeof(AcoesAuditoria)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(a => a.StartsWith("nebrys.", StringComparison.Ordinal))
            .Except([AcoesAuditoria.NebrysSessao, AcoesAuditoria.NebrysSessaoFalha]);

        Acoes.Select(a => (string)a[0]).Should().BeEquivalentTo(
            declaradas, "toda ação nova da área ganha um caso em Acao_Feita_TrilhaSemTenantComOperadorInstanteAfetadoEAntesEDepois");
    }

    [Fact]
    public async Task Rotas_TodaRotaQueMudaAlgoNaArea_TemCasoNaConferencia()
    {
        using var http = api.NovoHttp();
        var nebrys = JsonDocument.Parse(await http.GetStringAsync(new Uri("/openapi/nebrys.json", UriKind.Relative))).RootElement;

        var rotas = nebrys.GetProperty("paths").EnumerateObject()
            .SelectMany(caminho => caminho.Value.EnumerateObject().Select(metodo => $"{metodo.Name.ToUpperInvariant()} {caminho.Name}"))
            .Where(r => !r.StartsWith("GET ", StringComparison.Ordinal) && r != "POST /api/v1/nebrys/sessao");

        rotas.Should().BeEquivalentTo(
            RotaDaAcao.Values, "toda rota de ação da área tem a trilha conferida aqui (a sessão tem a sua, no F14k)");
    }

    // ---------- apoio ----------

    /// <summary>O alvo da ação: a empresa, o convite ou a entrada (com o e-mail), ou o documento (sem id).</summary>
    private sealed record Alvo(Guid Id, string? Email = null, string? Documento = null);

    /// <summary>Cria o alvo no estado em que a ação vale, sem deixar linha depois da foto da trilha.</summary>
    private async Task<Alvo> PrepararAsync(string acao, string token)
    {
        switch (acao)
        {
            case AcoesAuditoria.NebrysEmpresaAtivacao or AcoesAuditoria.NebrysEmpresaProrrogacao or AcoesAuditoria.NebrysEmpresaCancelamento:
                return new Alvo((await Equipe.CriarAsync(api)).TenantId);
            case AcoesAuditoria.NebrysEmpresaReativacao:
            {
                var empresa = (await Equipe.CriarAsync(api)).TenantId;
                (await PostAsync(token, $"{Area}/empresas/{empresa}/cancelamento")).Dispose();
                return new Alvo(empresa);
            }

            case AcoesAuditoria.NebrysDocumentoLiberacao:
            {
                var cpf = DocumentosDeTeste.NovoCpf();
                using var http = api.NovoHttp();
                using var registro = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), new
                {
                    nome = "Ana Souza", email = NovoEmail(), senha = Equipe.Senha, nomeEmpresa = "Estúdio Traço", documento = cpf,
                    versaoTermos = TextosAceitos.Termos, versaoPrivacidade = TextosAceitos.Privacidade,
                });
                registro.StatusCode.Should().Be(HttpStatusCode.Created, "o documento precisa ter tido teste");
                return new Alvo(Guid.Empty, Documento: cpf);
            }

            case AcoesAuditoria.NebrysConviteEmissao:
                return new Alvo(Guid.Empty, NovoEmail());
            case AcoesAuditoria.NebrysConviteReenvio or AcoesAuditoria.NebrysConviteRevogacao:
            {
                var email = NovoEmail();
                using var emissao = await PostAsync(token, $"{Area}/convites", new { email });
                emissao.StatusCode.Should().Be(HttpStatusCode.Created);
                var corpo = await emissao.Content.ReadFromJsonAsync<JsonElement>();
                return new Alvo(corpo.GetProperty("convite").GetProperty("id").GetGuid(), email);
            }

            case AcoesAuditoria.NebrysListaConvite or AcoesAuditoria.NebrysListaDescarte:
            {
                var email = NovoEmail();
                using var http = api.NovoHttp();
                using var inscricao = await http.PostAsJsonAsync(
                    new Uri("/api/v1/lista-espera", UriKind.Relative), new { nome = "Ana Souza", email, nomeEmpresa = "Estúdio Traço" });
                inscricao.StatusCode.Should().Be(HttpStatusCode.Accepted);
                await using var db = api.DaEquipe(Guid.NewGuid());
                return new Alvo(await db.Set<EntradaListaEspera>().Where(e => e.Email == email).Select(e => e.Id).SingleAsync(), email);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(acao), acao, "Ação sem preparo na conferência da trilha.");
        }
    }

    private async Task FazerAsync(string acao, string token, Alvo alvo)
    {
        using var resposta = acao switch
        {
            AcoesAuditoria.NebrysEmpresaAtivacao => await PostAsync(token, $"{Area}/empresas/{alvo.Id}/ativacao"),
            AcoesAuditoria.NebrysEmpresaProrrogacao => await PostAsync(
                token, $"{Area}/empresas/{alvo.Id}/prorrogacao", new { ate = Dia(Hoje().AddDays(5)) }),
            AcoesAuditoria.NebrysEmpresaCancelamento => await PostAsync(token, $"{Area}/empresas/{alvo.Id}/cancelamento"),
            AcoesAuditoria.NebrysEmpresaReativacao => await PostAsync(token, $"{Area}/empresas/{alvo.Id}/reativacao"),
            AcoesAuditoria.NebrysDocumentoLiberacao => await PostAsync(token, $"{Area}/documentos/liberacao", new { documento = alvo.Documento }),
            AcoesAuditoria.NebrysConviteEmissao => await PostAsync(token, $"{Area}/convites", new { email = alvo.Email }),
            AcoesAuditoria.NebrysConviteReenvio => await PostAsync(token, $"{Area}/convites/{alvo.Id}/reenvio"),
            AcoesAuditoria.NebrysConviteRevogacao => await PostAsync(token, $"{Area}/convites/{alvo.Id}/revogacao"),
            AcoesAuditoria.NebrysListaConvite => await PostAsync(token, $"{Area}/lista-espera/{alvo.Id}/convite"),
            AcoesAuditoria.NebrysListaDescarte => await OperadorNebrys.EnviarAsync(
                api.NovoHttp(), HttpMethod.Delete, $"{Area}/lista-espera/{alvo.Id}", token),
            _ => throw new ArgumentOutOfRangeException(nameof(acao), acao, "Ação sem rota na conferência da trilha."),
        };
        resposta.IsSuccessStatusCode.Should().BeTrue("{0} → {1}: {2}", acao, resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
    }

    /// <summary>A empresa afetada no alvo, o e-mail afetado nos dados ou, na liberação, nada que identifique o documento.</summary>
    private static void ConferirAfetado(string acao, Alvo alvo, LinhaTrilha linha, JsonElement dados)
    {
        if (acao.StartsWith("nebrys.empresa.", StringComparison.Ordinal))
        {
            linha.AlvoTipo.Should().Be("empresa");
            linha.AlvoId.Should().Be(alvo.Id);
            dados.GetProperty("antes").GetProperty("situacao").ValueKind.Should().Be(JsonValueKind.String);
            dados.GetProperty("depois").GetProperty("situacao").ValueKind.Should().Be(JsonValueKind.String);
            return;
        }

        if (acao == AcoesAuditoria.NebrysDocumentoLiberacao)
        {
            linha.AlvoTipo.Should().Be("documento");
            linha.AlvoId.Should().BeNull("o documento não tem id, e ele nem o HMAC vão para a trilha");
            linha.Dados.Should().NotContain(alvo.Documento!).And.NotContain(DocumentosDeTeste.Hash(alvo.Documento!));
            dados.GetProperty("antes").GetProperty("liberadoEm").ValueKind.Should().Be(JsonValueKind.Null);
            dados.GetProperty("depois").GetProperty("liberadoEm").GetDateTimeOffset().Should().Be(linha.OcorridoEm);
            return;
        }

        dados.GetProperty("email").GetString().Should().Be(alvo.Email, "o e-mail afetado");
        if (alvo.Id != Guid.Empty)
            linha.AlvoId.Should().Be(alvo.Id);
        switch (acao)
        {
            case AcoesAuditoria.NebrysConviteEmissao:
                dados.GetProperty("antes").ValueKind.Should().Be(JsonValueKind.Null, "o convite não existia");
                dados.GetProperty("depois").GetProperty("situacao").GetString().Should().Be("PENDENTE");
                break;
            case AcoesAuditoria.NebrysConviteReenvio:
                dados.GetProperty("antes").GetProperty("situacao").GetString().Should().Be("PENDENTE");
                dados.GetProperty("depois").GetProperty("situacao").GetString().Should().Be("PENDENTE");
                break;
            case AcoesAuditoria.NebrysConviteRevogacao:
                dados.GetProperty("antes").GetProperty("situacao").GetString().Should().Be("PENDENTE");
                dados.GetProperty("depois").GetProperty("situacao").GetString().Should().Be("REVOGADO");
                break;
            case AcoesAuditoria.NebrysListaConvite:
                dados.GetProperty("antes").GetProperty("situacao").GetString().Should().Be("AGUARDANDO");
                dados.GetProperty("depois").GetProperty("situacao").GetString().Should().Be("CONVIDADA");
                break;
            case AcoesAuditoria.NebrysListaDescarte:
                dados.GetProperty("antes").GetProperty("situacao").GetString().Should().Be("AGUARDANDO");
                dados.GetProperty("depois").ValueKind.Should().Be(JsonValueKind.Null, "a entrada foi apagada");
                break;
        }
    }

    private async Task<HttpResponseMessage> PostAsync(string token, string rota, object? corpo = null)
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri(rota, UriKind.Relative));
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);
        return await http.SendAsync(requisicao);
    }

    private async Task<HashSet<Guid>> IdsDaTrilhaAsync() => [.. (await TrilhaAsync()).Select(l => l.Id)];

    /// <summary>As linhas <c>nebrys.*</c> da trilha (lidas fora dos filtros), menos as da sessão de operação.</summary>
    private async Task<List<LinhaTrilha>> TrilhaAsync()
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        return await db.Database.SqlQuery<LinhaTrilha>($"""
            SELECT id, tenant_id, usuario_id, acao, alvo_tipo, alvo_id, dados::text AS dados, ocorrido_em FROM auditoria
            WHERE acao LIKE 'nebrys.%' AND acao NOT LIKE 'nebrys.sessao%'
            """).ToListAsync();
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(api.Relogio.Agora, SaoPaulo).DateTime);

    private static string Dia(DateOnly dia) => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string NovoEmail() => $"trilha-{Guid.NewGuid():N}@exemplo.com";

    private sealed class LinhaTrilha
    {
        // Colunas pelo nome em snake_case (a convenção do contexto vale também para o tipo não mapeado).
        public Guid Id { get; set; }

        public Guid? TenantId { get; set; }

        public Guid? UsuarioId { get; set; }

        public string Acao { get; set; } = "";

        public string? AlvoTipo { get; set; }

        public Guid? AlvoId { get; set; }

        public string? Dados { get; set; }

        public DateTimeOffset OcorridoEm { get; set; }
    }
}
