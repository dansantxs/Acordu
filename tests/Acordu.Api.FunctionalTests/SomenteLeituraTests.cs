using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.Composicao;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Domain.Entities;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14b (F14-13 a F14-19, F14-23, F14-100; design D2 a D4, ADR 0015): na empresa VENCIDA ou CANCELADA a
/// equipe lê como antes; a gravação da empresa é recusada com <c>422 empresa-somente-leitura</c> depois da permissão, sem
/// gravar nem enviar e-mail; o que é da pessoa segue; e a situação de uma empresa não vale em outra.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class SomenteLeituraTests(ApiFixture api)
{
    private const string Erros = "https://acordu.app.br/erros/";
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    /// <summary>
    /// Põe a empresa na situação, direto no banco: o vencimento pelo diário é do F14a e o cancelamento pela área da
    /// Nebrys, do F14l.
    /// </summary>
    private async Task SituacaoAsync(Guid tenantId, string situacao)
    {
        var desde = api.Relogio.Agora.ToUniversalTime();
        await using var db = api.DaEquipe(tenantId);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE tenants SET situacao = {situacao}, somente_leitura_desde = {desde} WHERE id = {tenantId}");
    }

    private static async Task RecusadaAsync(
        Equipe equipe, HttpMethod metodo, string rota, object? corpo = null, string? token = null)
    {
        var problema = await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, metodo, rota, corpo, token);
        problema.GetProperty("type").GetString().Should().Be(Erros + "empresa-somente-leitura");
        problema.GetProperty("detail").GetString().Should().Contain("só leitura");
    }

    private static async Task<Guid> PapelAsync(Equipe equipe, string nome)
    {
        var papeis = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/papeis");
        return Id(papeis.EnumerateArray().Single(p => p.GetProperty("nome").GetString() == nome));
    }

    private static async Task<Guid> UsuarioAsync(Equipe equipe, string? token = null) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: token)).GetProperty("usuarioId").GetGuid();

    private static async Task<(Guid Id, string Email)> ContatoAsync(Equipe equipe, Guid cliente)
    {
        var email = $"contato-{Guid.NewGuid():N}@bomgrao.com.br";
        var contato = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos", new { nome = "Marina Souza", email });
        return (Id(contato), email);
    }

    private async Task<HttpResponseMessage> AuthAsync(string rota, object? corpo, string? refresh = null, string? token = null)
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri(rota, UriKind.Relative));
        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);
        if (refresh is not null)
            requisicao.Headers.Add("Cookie", $"acordu_refresh={refresh}");
        if (token is not null)
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(requisicao);
    }

    private static string? CookieRefresh(HttpResponseMessage resposta)
    {
        if (!resposta.Headers.TryGetValues("Set-Cookie", out var cookies))
            return null;
        var valor = cookies.FirstOrDefault(c => c.StartsWith("acordu_refresh=", StringComparison.Ordinal))
            ?.Split(';')[0]["acordu_refresh=".Length..];
        return string.IsNullOrEmpty(valor) ? null : valor;
    }

    private async Task<Guid> AnexoAsync(Equipe equipe, Guid solicitacao)
    {
        using var formulario = new MultipartFormDataContent
        {
            { new StringContent("Segue o print"), "texto" },
            { new StringContent("EXTERNA"), "visibilidade" },
        };
        var parte = new ByteArrayContent(Png);
        parte.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        formulario.Add(parte, "anexos", "tela.png");
        using var http = api.NovoHttp();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/v1/solicitacoes/{solicitacao}/interacoes", UriKind.Relative))
        {
            Content = formulario,
        };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", equipe.Dono);
        using var resposta = await http.SendAsync(pedido);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        return Id((await Equipe.JsonAsync(resposta)).GetProperty("anexos")[0]);
    }

    /// <summary>Cliente, contrato, solicitação com anexo, tarefa e apontamento: o que as leituras e as recusas usam.</summary>
    private async Task<(Equipe Equipe, Guid Cliente, Guid Contrato, Guid Solicitacao, Guid Tarefa, Guid Anexo)> EmpresaComDadosAsync()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var contrato = await ContratoAsync(equipe, cliente);
        var solicitacao = await SolicitacaoAsync(equipe, cliente);
        var anexo = await AnexoAsync(equipe, solicitacao);
        var tarefa = await TarefaAsync(equipe, solicitacao);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = tarefa, data = "2026-10-05", duracaoMinutos = 90, descricao = "Ajuste de layout", faturavel = true,
        });
        return (equipe, cliente, contrato, solicitacao, tarefa, anexo);
    }

    [Fact]
    public async Task Leituras_EmpresaVencida_RespondemComoAntes() // F14-13
    {
        var (equipe, cliente, contrato, solicitacao, tarefa, anexo) = await EmpresaComDadosAsync();
        string[] rotas =
        [
            "/api/v1/clientes", $"/api/v1/clientes/{cliente}", $"/api/v1/clientes/{cliente}/arquivos",
            "/api/v1/solicitacoes", $"/api/v1/solicitacoes/{solicitacao}", $"/api/v1/solicitacoes/{solicitacao}/interacoes",
            "/api/v1/tarefas", $"/api/v1/tarefas/{tarefa}", "/api/v1/apontamentos",
            "/api/v1/contratos", $"/api/v1/contratos/{contrato}", $"/api/v1/contratos/{contrato}/consumo?periodo=2026-10",
            "/api/v1/agenda", "/api/v1/dashboard", "/api/v1/capacidade", "/api/v1/atividades", "/api/v1/auditoria",
            "/api/v1/configuracoes/empresa", "/api/v1/configuracoes/portal", "/api/v1/equipe", "/api/v1/papeis",
            "/api/v1/projetos", "/api/v1/recorrencias", "/api/v1/notificacoes",
        ];
        var antes = new Dictionary<string, string>();
        foreach (var rota in rotas)
            antes[rota] = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, rota)).GetRawText();

        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        foreach (var rota in rotas)
            (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, rota)).GetRawText().Should().Be(antes[rota], rota);
        var download = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/anexos/{anexo}/download");
        download.GetProperty("url").GetString().Should().Contain($"{equipe.TenantId}/{solicitacao}/{anexo}");
    }

    [Fact]
    public async Task Gravar_EmpresaVencida_Recusa422SemGravar() // F14-14
    {
        var (equipe, cliente, _, _, tarefa, _) = await EmpresaComDadosAsync();
        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        await RecusasDaEmpresaAsync(equipe, cliente, tarefa);
    }

    [Fact]
    public async Task Gravar_EmpresaCancelada_MesmaRecusa() // F14-15
    {
        var (equipe, cliente, _, _, tarefa, _) = await EmpresaComDadosAsync();
        await SituacaoAsync(equipe.TenantId, "CANCELADA");

        await RecusasDaEmpresaAsync(equipe, cliente, tarefa);
    }

    /// <summary>Criar solicitação, apontar horas, editar cliente e outras gravações: 422 e nada muda.</summary>
    private static async Task RecusasDaEmpresaAsync(Equipe equipe, Guid cliente, Guid tarefa)
    {
        var solicitacoes = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes")).GetRawText();
        var apontamentos = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/apontamentos")).GetRawText();
        var detalhe = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/clientes/{cliente}")).GetRawText();
        var tarefas = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/tarefas")).GetRawText();
        var empresa = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/configuracoes/empresa")).GetRawText();

        await RecusadaAsync(equipe, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, titulo = "Trocar a foto da vitrine", prioridade = "MEDIA",
        });
        await RecusadaAsync(equipe, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = tarefa, data = "2026-10-06", duracaoMinutos = 30, descricao = "Revisão", faturavel = true,
        });
        await RecusadaAsync(equipe, HttpMethod.Patch, $"/api/v1/clientes/{cliente}", new { nome = "Outro nome" });
        await RecusadaAsync(equipe, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos", new
        {
            nome = "Carlos Prado", email = $"carlos-{Guid.NewGuid():N}@bomgrao.com.br",
        });
        await RecusadaAsync(equipe, HttpMethod.Post, "/api/v1/tarefas", new { titulo = "Nova arte" });
        await RecusadaAsync(equipe, HttpMethod.Post, "/api/v1/cronometro/inicio", new { tarefaId = tarefa });
        await RecusadaAsync(equipe, HttpMethod.Post, "/api/v1/clientes", new { nome = "Mercado Central" });

        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes")).GetRawText().Should().Be(solicitacoes);
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/apontamentos")).GetRawText().Should().Be(apontamentos);
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/clientes/{cliente}")).GetRawText().Should().Be(detalhe);
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/tarefas")).GetRawText().Should().Be(tarefas);
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/configuracoes/empresa")).GetRawText().Should().Be(empresa);
        // Sem cronômetro ativo: 204.
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Get, "/api/v1/cronometro");
    }

    [Fact]
    public async Task Gravar_EmpresaVencidaSemPermissao_403AntesDaSituacao() // F14-16
    {
        var equipe = await Equipe.CriarAsync(api);
        var leitor = await equipe.ComPapelAsync(Papel.NomeLeitor);
        var cliente = await ClienteAsync(equipe);
        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        var criar = await equipe.EsperarAsync(
            HttpStatusCode.Forbidden, HttpMethod.Post, "/api/v1/clientes", new { nome = "Mercado Central" }, leitor);
        var editar = await equipe.EsperarAsync(
            HttpStatusCode.Forbidden, HttpMethod.Patch, $"/api/v1/clientes/{cliente}", new { nome = "Outro nome" }, leitor);
        var convidar = await equipe.EsperarAsync(
            HttpStatusCode.Forbidden, HttpMethod.Post, "/api/v1/convites",
            new { email = $"pessoa-{Guid.NewGuid():N}@estudio.com", papelId = await PapelAsync(equipe, Papel.NomeMembro) }, leitor);

        foreach (var problema in new[] { criar, editar, convidar })
            problema.GetProperty("type").GetString().Should().Be(Erros + "sem-permissao");
    }

    [Fact]
    public async Task ConvidarEEmitirLink_EmpresaVencida_RecusaSemEmail() // F14-17
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var solicitacao = await SolicitacaoAsync(equipe, cliente);
        var (contato, emailContato) = await ContatoAsync(equipe, cliente);
        var membro = await PapelAsync(equipe, Papel.NomeMembro);
        var convidado = $"pessoa-{Guid.NewGuid():N}@estudio.com";
        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        await RecusadaAsync(equipe, HttpMethod.Post, "/api/v1/convites", new { email = convidado, papelId = membro });
        await RecusadaAsync(
            equipe, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos/{contato}/acesso-portal",
            new { escopoPortal = "TODAS_DO_CLIENTE" });
        await RecusadaAsync(equipe, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal", new { contatoId = contato });
        await RecusadaAsync(
            equipe, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal", new { contatoId = contato, enviarEmail = false });

        api.Emails.Para(convidado).Should().BeEmpty();
        api.Emails.Para(emailContato).Should().BeEmpty();
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/convites")).GetArrayLength().Should().Be(0);
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal"))
            .GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task SessaoESino_EmpresaVencida_SeguemFuncionando() // F14-18
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var cliente = await ClienteAsync(equipe);
        await SolicitacaoAsync(equipe, cliente);
        await SolicitacaoAsync(equipe, cliente);
        var sino = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: membro);
        sino.GetProperty("naoLidas").GetInt32().Should().Be(2);
        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        // Sino: marcar um e marcar todos.
        await equipe.EsperarAsync(
            HttpStatusCode.NoContent, HttpMethod.Post, $"/api/v1/notificacoes/{Id(sino.GetProperty("itens")[0])}/leitura", token: membro);
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: membro))
            .GetProperty("naoLidas").GetInt32().Should().Be(1);
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Post, "/api/v1/notificacoes/leitura", token: membro);
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: membro))
            .GetProperty("naoLidas").GetInt32().Should().Be(0);

        // Entrar, renovar, trocar de empresa e sair.
        using var login = await AuthAsync("/api/v1/auth/login", new { email = equipe.EmailDono, senha = Equipe.Senha });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        using var renovacao = await AuthAsync("/api/v1/auth/refresh", null, CookieRefresh(login));
        renovacao.StatusCode.Should().Be(HttpStatusCode.OK);
        var acesso = (await Equipe.JsonAsync(renovacao)).GetProperty("accessToken").GetString()!;
        using var troca = await AuthAsync("/api/v1/auth/tenant-ativo", new { tenantId = equipe.TenantId }, CookieRefresh(renovacao), acesso);
        troca.StatusCode.Should().Be(HttpStatusCode.OK);
        using var saida = await AuthAsync("/api/v1/auth/logout", null, CookieRefresh(troca));
        saida.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Mudar a própria senha (pelo link do e-mail).
        using var esqueci = await AuthAsync("/api/v1/auth/senha/esqueci", new { email = equipe.EmailDono });
        esqueci.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var token = api.Emails.TokenDe(equipe.EmailDono, "redefinicao-senha");
        using var redefinir = await AuthAsync("/api/v1/auth/senha/redefinir", new { token, novaSenha = "outra-senha-bem-comprida" });
        redefinir.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var novaEntrada = await AuthAsync("/api/v1/auth/login", new { email = equipe.EmailDono, senha = "outra-senha-bem-comprida" });
        novaEntrada.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ConfirmarEmail_EmpresaVencida_FuncionaELiberaOEnvio() // F14-18: gravação fora do change tracker (ExecuteUpdate)
    {
        var equipe = await Equipe.CriarAsync(api, confirmar: false);
        await SituacaoAsync(equipe.TenantId, "VENCIDA");
        await using (var antes = api.DaEquipe(equipe.TenantId))
            (await antes.Tenants.SingleAsync()).EnvioLiberado.Should().BeFalse();

        using var confirmacao = await AuthAsync(
            "/api/v1/auth/confirmacao-email", new { token = api.Emails.TokenDe(equipe.EmailDono, "confirmacao-email") });

        confirmacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var depois = api.DaEquipe(equipe.TenantId);
        var tenant = await depois.Tenants.SingleAsync();
        tenant.EnvioLiberado.Should().BeTrue();
        tenant.Situacao.Should().Be(Acordu.Domain.Enums.SituacaoEmpresa.Vencida);
    }

    [Fact]
    public async Task SugerirMelhoria_EmpresaVencida_AceitaEAvisaANebrys() // F14-19
    {
        var equipe = await Equipe.CriarAsync(api);
        await SituacaoAsync(equipe.TenantId, "VENCIDA");
        var discord = new DiscordFalso();
        await using var app = await api.NovaApiAsync(
            new Dictionary<string, string?> { ["Discord:Sugestoes"] = DiscordFalso.Sugestoes },
            servicos: s => s.AddHttpClient(ComposicaoApi.NomeClienteDiscord).ConfigurePrimaryHttpMessageHandler(() => discord));

        using var http = app.GetTestClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/sugestoes", UriKind.Relative))
        {
            Content = JsonContent.Create(new { texto = "Seria bom exportar o extrato em planilha.", tela = "/contratos" }),
        };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", equipe.Dono);
        using var resposta = await http.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        var numero = (await Equipe.JsonAsync(resposta)).GetProperty("numero").GetInt32();
        discord.Textos(DiscordFalso.Sugestoes).Should().ContainSingle()
            .Which.Should().StartWith($"**Sugestão #{numero}**").And.Contain("exportar o extrato em planilha");
    }

    [Fact]
    public async Task TrocarDeEmpresa_UmaVencidaOutraEmTeste_GravaSoNaOutra() // F14-23
    {
        var vencida = await Equipe.CriarAsync(api);
        var emTeste = await Equipe.CriarAsync(api);
        // O Dono da empresa em teste também tem vínculo na que vai vencer (gravado direto, como no apoio dos testes).
        var pessoa = await UsuarioAsync(emTeste);
        await using (var db = api.DaEquipe(vencida.TenantId))
        {
            var membro = (await db.Papeis.ToListAsync()).Single(p => p.Nome == Papel.NomeMembro);
            db.Add(Vinculo.Criar(Guid.NewGuid(), pessoa, membro));
            await db.SaveChangesAsync();
        }

        await SituacaoAsync(vencida.TenantId, "VENCIDA");

        var naVencida = (await emTeste.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Post, "/api/v1/auth/tenant-ativo", new { tenantId = vencida.TenantId })).GetProperty("accessToken").GetString()!;
        await RecusadaAsync(emTeste, HttpMethod.Post, "/api/v1/clientes", new { nome = "Mercado Central" }, naVencida);
        var naEmTeste = (await emTeste.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Post, "/api/v1/auth/tenant-ativo", new { tenantId = emTeste.TenantId }, naVencida))
            .GetProperty("accessToken").GetString()!;

        var cliente = await emTeste.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", new { nome = "Mercado Central" }, naEmTeste);
        await emTeste.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = Id(cliente), titulo = "Trocar a foto da vitrine", prioridade = "MEDIA",
        }, naEmTeste);

        (await emTeste.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/clientes", token: naVencida))
            .GetProperty("total").GetInt32().Should().Be(0, "nada foi gravado na vencida");
    }

    [Theory]
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task CoisasDaPessoa_EmpresaSoLeitura_SeguemFuncionando(string situacao) // F14-100
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var cliente = await ClienteAsync(equipe);
        var solicitacao = await SolicitacaoAsync(equipe, cliente);
        var tarefa = await TarefaAsync(equipe, solicitacao);
        await SituacaoAsync(equipe.TenantId, situacao);

        // Preferências de aviso.
        var salvas = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/notificacoes/preferencias", new
        {
            categorias = new[] { new { categoria = "NOVA_SOLICITACAO", email = false } },
        }, membro);
        salvas.GetProperty("categorias").EnumerateArray()
            .Single(c => c.GetProperty("categoria").GetString() == "NOVA_SOLICITACAO").GetProperty("email").GetBoolean().Should().BeFalse();

        // Link da agenda: emitir, mudar e revogar.
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/agenda/assinatura", new { discreta = false }, membro);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, "/api/v1/agenda/assinatura", new { discreta = true }, membro);
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, "/api/v1/agenda/assinatura", token: membro);

        // Seguir e deixar de seguir uma solicitação e uma tarefa.
        foreach (var rota in new[] { $"/api/v1/solicitacoes/{solicitacao}", $"/api/v1/tarefas/{tarefa}" })
        {
            await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"{rota}/seguidores/eu", token: membro);
            await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"{rota}/seguidores/eu", token: membro);
        }
    }

    [Fact]
    public async Task Gravar_EmpresaVencida_404E400VemAntesDaRecusaEIfMatchVelhoDa422() // F14-14, ordem das conferências
    {
        var (equipe, cliente, _, _, _, _) = await EmpresaComDadosAsync();
        var outra = await Equipe.CriarAsync(api);
        var deOutra = await ClienteAsync(outra);
        var antiga = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/clientes/{cliente}")).GetProperty("versao").GetString();
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/clientes/{cliente}", new { nome = "Padaria Atualizada" }, ifMatch: antiga);
        var versao = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/clientes/{cliente}")).GetProperty("versao").GetString();
        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        // Não existe, ou é de outro tenant: 404 (isolamento), não 422.
        var inexistente = await equipe.EsperarAsync(
            HttpStatusCode.NotFound, HttpMethod.Patch, $"/api/v1/clientes/{Guid.NewGuid()}", new { nome = "Outro nome" });
        var alheio = await equipe.EsperarAsync(
            HttpStatusCode.NotFound, HttpMethod.Patch, $"/api/v1/clientes/{deOutra}", new { nome = "Outro nome" });
        foreach (var problema in new[] { inexistente, alheio })
            problema.GetProperty("type").GetString().Should().Be(Erros + "nao-encontrado");

        // Dado inválido: 400 com errors, não 422.
        var invalido = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/clientes", new { nome = "" });
        invalido.GetProperty("type").GetString().Should().Be(Erros + "validacao");
        invalido.TryGetProperty("errors", out _).Should().BeTrue();

        // If-Match velho: o 409 sai no SaveChanges, depois da guarda; na empresa só leitura a recusa é a mesma 422 e nada muda
        // (ADR 0015 e design D2 não pedem 409 antes dela).
        var velho = await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Patch, $"/api/v1/clientes/{cliente}", new { nome = "Outro nome" }, ifMatch: antiga);
        velho.GetProperty("type").GetString().Should().Be(Erros + "empresa-somente-leitura");

        // Sem If-Match, a guarda recusa e a empresa fica como estava.
        await RecusadaAsync(equipe, HttpMethod.Patch, $"/api/v1/clientes/{cliente}", new { nome = "Outro nome" });
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/clientes/{cliente}")).GetProperty("versao").GetString().Should().Be(versao);
    }

    [Fact]
    public async Task Gravar_UmaEmpresaVencida_NaoAfetaAGravacaoDeOutra() // isolamento entre tenants
    {
        var vencida = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        await SituacaoAsync(vencida.TenantId, "VENCIDA");

        await RecusadaAsync(vencida, HttpMethod.Post, "/api/v1/clientes", new { nome = "Mercado Central" });
        var cliente = await ClienteAsync(outra);
        await SolicitacaoAsync(outra, cliente);

        (await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/clientes")).GetProperty("total").GetInt32().Should().Be(1);
        (await vencida.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/clientes")).GetProperty("total").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData("TESTE")]
    [InlineData("ATIVA")]
    public async Task Gravar_EmpresaEmTesteOuAtiva_Grava(string situacao)
    {
        var equipe = await Equipe.CriarAsync(api);
        await SituacaoAsync(equipe.TenantId, situacao);

        var cliente = await ClienteAsync(equipe);
        await SolicitacaoAsync(equipe, cliente);
    }

    [Fact]
    public async Task FechamentoAutomatico_EmpresaVencida_FicaDeForaSemFalha() // ADR 0015, item 4 (F14-22 completo no F14d)
    {
        var equipe = await Equipe.CriarAsync(api);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/configuracoes/portal", new { diasFechamentoAutomatico = 2 });
        var solicitacao = await SolicitacaoAsync(equipe, await ClienteAsync(equipe));
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/resolucao",
            new { solucao = new { texto = "Feito" } });
        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        var inicio = api.Relogio.Agora;
        api.Relogio.Agora = inicio.AddDays(3);
        JsonElement resultado;
        try
        {
            using var http = api.NovoHttp();
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/fechamento-automatico", UriKind.Relative));
            requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
            using var resposta = await http.SendAsync(requisicao);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            resultado = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }

        resultado.GetProperty("falhas").GetInt32().Should().Be(0);
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}"))
            .GetProperty("status").GetString().Should().Be("RESOLVIDA");
    }
}
