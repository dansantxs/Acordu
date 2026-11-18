using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Atividades;
using Acordu.Infrastructure.Persistence.Auditoria;
using Acordu.Infrastructure.Persistence.Outbox;
using Acordu.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// RF-AUT-07 (spec do F9, F9d): o job diário apaga a empresa não liberada há 7 dias ou mais e sem dados, com o criador
/// se ele não tiver outro vínculo. Empresa com dado, liberada ou recente fica; as outras empresas não mudam.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class ExpurgoCadastrosTests(ApiFixture api)
{
    [Fact]
    public async Task Diario_CadastroAbandonadoSemDados_ApagaEmpresaECriador() // F9-28
    {
        var cadastro = await RegistrarAsync();

        var resultado = await DiarioAsync(TimeSpan.FromDays(7));

        // ">= 1": a coleção compartilha o banco, e cadastros abandonados de outros testes também vencem.
        resultado.GetProperty("cadastrosRemovidos").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        resultado.GetProperty("falhas").GetInt32().Should().Be(0);
        await using var db = api.DaEquipe(cadastro.TenantId);
        (await db.Tenants.AnyAsync()).Should().BeFalse();
        (await db.Papeis.AnyAsync()).Should().BeFalse();
        (await db.Vinculos.AnyAsync()).Should().BeFalse();
        (await db.Calendarios.AnyAsync()).Should().BeFalse();
        (await db.Users.AnyAsync(u => u.Id == cadastro.UsuarioId)).Should().BeFalse();
        (await db.Set<RefreshToken>().AnyAsync(r => r.UsuarioId == cadastro.UsuarioId)).Should().BeFalse();
        (await db.Set<TokenConta>().AnyAsync(t => t.UsuarioId == cadastro.UsuarioId)).Should().BeFalse("o token da confirmação sai junto");
        (await db.Set<MensagemOutbox>().IgnoreQueryFilters().AnyAsync(m => m.TenantId == cadastro.TenantId)).Should().BeFalse();
    }

    [Fact]
    public async Task Diario_CadastroComMenosDeSeteDias_Fica() // F9-29
    {
        var cadastro = await RegistrarAsync();

        await DiarioAsync(TimeSpan.FromDays(7) - TimeSpan.FromMinutes(1));

        await using var db = api.DaEquipe(cadastro.TenantId);
        (await db.Tenants.AnyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Diario_CadastroAbandonadoComCliente_Fica() // F9-30
    {
        var equipe = await Equipe.CriarAsync(api, confirmar: false);
        await Cenarios.ClienteAsync(equipe);

        await DiarioAsync(TimeSpan.FromDays(8));

        await using var db = api.DaEquipe(equipe.TenantId);
        (await db.Tenants.AnyAsync()).Should().BeTrue();
        (await db.Clientes.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Diario_EmpresaLiberadaSemDados_Fica() // F9-31
    {
        var equipe = await Equipe.CriarAsync(api);

        await DiarioAsync(TimeSpan.FromDays(8));

        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu");
    }

    [Fact]
    public async Task Diario_CriadorComVinculoEmOutraEmpresa_FicaComOOutroVinculo() // F9-32
    {
        var outra = await Equipe.CriarAsync(api);
        // A pessoa registrou a própria empresa (sem confirmar, sem dados) e também é da equipe da "outra".
        var pessoa = await outra.ComPapelAsync(Papel.NomeMembro);
        var usuario = (await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: pessoa))
            .GetProperty("usuarioId").GetGuid();
        Guid propria;
        await using (var db = api.DaEquipe(outra.TenantId))
            propria = await db.Tenants.IgnoreQueryFilters().Where(t => t.CriadoPorUsuarioId == usuario).Select(t => t.Id).SingleAsync();

        await DiarioAsync(TimeSpan.FromDays(8));

        await using (var db = api.DaEquipe(propria))
            (await db.Tenants.AnyAsync()).Should().BeFalse("a empresa abandonada sai");
        await using (var db = api.DaEquipe(outra.TenantId))
        {
            (await db.Users.AnyAsync(u => u.Id == usuario)).Should().BeTrue("o usuário tem vínculo com outra empresa");
            (await db.Vinculos.AnyAsync(v => v.UsuarioId == usuario)).Should().BeTrue();
        }

        await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: pessoa);
    }

    [Fact] // D10 (F14h): o aceite sai com a conta do cadastro abandonado
    public async Task Diario_CadastroAbandonadoComAceites_AceitesSaemComAConta()
    {
        var cadastro = await RegistrarAsync();
        await using (var db = api.DaEquipe(cadastro.TenantId))
            (await db.Set<AceiteTexto>().CountAsync(a => a.UsuarioId == cadastro.UsuarioId)).Should().Be(2, "o cadastro gravou os dois aceites");

        await DiarioAsync(TimeSpan.FromDays(7));

        await using var verificacao = api.DaEquipe(cadastro.TenantId);
        (await verificacao.Users.AnyAsync(u => u.Id == cadastro.UsuarioId)).Should().BeFalse();
        (await verificacao.Set<AceiteTexto>().CountAsync(a => a.UsuarioId == cadastro.UsuarioId)).Should().Be(0);
    }

    [Fact] // D10 (F14h): o criador que fica (vínculo em outra empresa) mantém os aceites
    public async Task Diario_CriadorComVinculoEmOutraEmpresa_MantemOsAceites()
    {
        var outra = await Equipe.CriarAsync(api);
        var pessoa = await outra.ComPapelAsync(Papel.NomeMembro);
        var usuario = (await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: pessoa))
            .GetProperty("usuarioId").GetGuid();
        await using (var db = api.DaEquipe(outra.TenantId))
            (await db.Set<AceiteTexto>().CountAsync(a => a.UsuarioId == usuario)).Should().BeGreaterThan(0, "aceitou o convite com os textos publicados");

        await DiarioAsync(TimeSpan.FromDays(8));

        await using var verificacao = api.DaEquipe(outra.TenantId);
        (await verificacao.Users.AnyAsync(u => u.Id == usuario)).Should().BeTrue();
        (await verificacao.Set<AceiteTexto>().CountAsync(a => a.UsuarioId == usuario)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Refresh_DepoisDoExpurgo_Retorna401() // F9-33
    {
        var cadastro = await RegistrarAsync();

        await DiarioAsync(TimeSpan.FromDays(8));

        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/auth/refresh", UriKind.Relative));
        requisicao.Headers.Add("Cookie", $"acordu_refresh={cadastro.Refresh}");
        using var resposta = await http.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Eu_ComAccessTokenDaEmpresaExpurgada_Retorna401() // F9-34
    {
        var cadastro = await RegistrarAsync();

        await DiarioAsync(TimeSpan.FromDays(8));

        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/auth/eu", UriKind.Relative));
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cadastro.Token);
        using var resposta = await http.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Diario_ComExpurgo_NaoTocaOutrasEmpresas() // F9-35
    {
        var abandonada = await RegistrarAsync();
        var outra = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(outra);
        var solicitacao = await Cenarios.SolicitacaoAsync(outra, cliente);
        var antes = await RastrosAsync(outra.TenantId);
        var dono = (await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("usuarioId").GetGuid();

        await DiarioAsync(TimeSpan.FromDays(8));

        await using (var db = api.DaEquipe(abandonada.TenantId))
            (await db.Tenants.AnyAsync()).Should().BeFalse();
        await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/clientes/{cliente}");
        await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}");
        await using (var db = api.DaEquipe(outra.TenantId))
        {
            (await db.Papeis.CountAsync()).Should().Be(4);
            (await db.Vinculos.CountAsync()).Should().Be(1);
            (await db.Users.AnyAsync(u => u.Id == dono)).Should().BeTrue();
        }

        (await RastrosAsync(outra.TenantId)).Should().Be(antes, "auditoria, feed e sino da outra empresa ficam");
    }

    [Fact]
    public async Task Diario_ExpurgoDeUmaEmpresaFalha_ContaEmFalhasESegueComAsDemais() // F9-37
    {
        var quebrada = await RegistrarAsync();
        var normal = await RegistrarAsync();
        // Falha forçada só na empresa "quebrada": apagar os papéis dela dispara um erro no banco.
        await using (var db = api.DaEquipe(quebrada.TenantId))
        {
            await db.Database.ExecuteSqlRawAsync(string.Format(CultureInfo.InvariantCulture, """
                CREATE OR REPLACE FUNCTION teste_f9_falha() RETURNS trigger AS $$ BEGIN RAISE EXCEPTION 'falha forçada'; END $$ LANGUAGE plpgsql;
                CREATE TRIGGER teste_f9_falha BEFORE DELETE ON papeis FOR EACH ROW WHEN (OLD.tenant_id = '{0}') EXECUTE FUNCTION teste_f9_falha();
                """, quebrada.TenantId));
        }

        JsonElement resultado;
        try
        {
            resultado = await DiarioAsync(TimeSpan.FromDays(8));
        }
        finally
        {
            await using var db = api.DaEquipe(quebrada.TenantId);
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER teste_f9_falha ON papeis; DROP FUNCTION teste_f9_falha();");
        }

        resultado.GetProperty("falhas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        resultado.GetProperty("cadastrosRemovidos").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        await using (var db = api.DaEquipe(quebrada.TenantId))
            (await db.Tenants.AnyAsync()).Should().BeTrue("a transação da empresa que falhou foi desfeita");
        await using (var db = api.DaEquipe(normal.TenantId))
            (await db.Tenants.AnyAsync()).Should().BeFalse("a falha de uma não para as demais");
    }

    [Fact]
    public async Task Diario_CadastroComSugestao_ExpurgaEASugestaoFicaSemEmpresaNemPessoa() // F13e, RNF-PRIV-01
    {
        var cadastro = await RegistrarAsync();
        using var http = api.NovoHttp();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/sugestoes", UriKind.Relative))
        {
            Content = JsonContent.Create(new { texto = "Sugestão de quem desistiu do cadastro." }),
        };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cadastro.Token);
        using var resposta = await http.SendAsync(pedido);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await DiarioAsync(TimeSpan.FromDays(8));

        await using var db = api.DaEquipe(Guid.NewGuid());
        (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == cadastro.TenantId)).Should().BeFalse("a sugestão não conta como dado da empresa");
        var restantes = await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM sugestoes WHERE id = {id} AND empresa_id IS NULL AND pessoa_id IS NULL")
            .SingleAsync();
        restantes.Should().Be(1, "a sugestão é da Nebrys: fica, sem a empresa nem a pessoa");
    }

    [Fact]
    public async Task Diario_RodadoDeNovo_NaoRemoveMais() // F9-36
    {
        await RegistrarAsync();
        (await DiarioAsync(TimeSpan.FromDays(8))).GetProperty("cadastrosRemovidos").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        var segundo = await DiarioAsync(TimeSpan.FromDays(8));

        segundo.GetProperty("cadastrosRemovidos").GetInt32().Should().Be(0);
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

    private async Task<(int Auditoria, int Atividades, int Notificacoes)> RastrosAsync(Guid tenantId)
    {
        await using var db = api.DaEquipe(tenantId);
        return (await db.Set<RegistroAuditoria>().CountAsync(), await db.Set<Atividade>().CountAsync(),
            await db.Set<Infrastructure.Persistence.Notificacoes.Notificacao>().CountAsync());
    }

    private async Task<Cadastro> RegistrarAsync()
    {
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), new
        {
            nome = "Ana Souza", email = $"abandono-{Guid.NewGuid():N}@exemplo.com", senha = Equipe.Senha, nomeEmpresa = "Estúdio Traço", documento = Apoio.DocumentosDeTeste.Novo(), versaoTermos = Apoio.TextosAceitos.Termos, versaoPrivacidade = Apoio.TextosAceitos.Privacidade,
        });
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var cookie = resposta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("acordu_refresh=", StringComparison.Ordinal));
        var token = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        using var eu = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/auth/eu", UriKind.Relative));
        eu.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var perfil = await http.SendAsync(eu);
        var corpo = await perfil.Content.ReadFromJsonAsync<JsonElement>();
        return new Cadastro(
            corpo.GetProperty("tenant").GetProperty("id").GetGuid(), corpo.GetProperty("usuarioId").GetGuid(), token,
            cookie.Split(';')[0]["acordu_refresh=".Length..]);
    }

    private sealed record Cadastro(Guid TenantId, Guid UsuarioId, string Token, string Refresh);
}
