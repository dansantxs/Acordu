using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Application.Exemplos;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Infrastructure.Persistence.Outbox;
using Acordu.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14s (F14-87 a F14-90, F14-110; RF-NEB-02; design D13; ADR 0014, item 5): a Nebrys cria a empresa de
/// demonstração com os dados fictícios do manual e fora do ciclo de situação, restaura sem tocar outra empresa e indica
/// quem entra como Dono (só conta que já existe); nada sai da demonstração por e-mail. Há uma demonstração por banco: cada
/// teste começa e termina sem nenhuma.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class DemonstracaoTests(ApiFixture api) : IAsyncLifetime
{
    private const string Rota = Demonstracoes.Rota;
    private const string Erros = "https://acordu.app.br/erros/";

    private static readonly DadosFicticios Dados = DadosFicticios.Arquivo;

    public Task InitializeAsync() => Demonstracoes.ApagarAsync(api);

    public Task DisposeAsync() => Demonstracoes.ApagarAsync(api);

    // ---------- leitura ----------

    [Fact]
    public async Task Obter_SemDemonstracao_200ComAEmpresaNula()
    {
        var token = await OperadorNebrys.TokenAsync(api);

        var (status, corpo) = await AreaAsync(HttpMethod.Get, Rota, token);

        status.Should().Be(HttpStatusCode.OK, "o 404 da área é só a barreira");
        corpo.GetProperty("empresaId").ValueKind.Should().Be(JsonValueKind.Null);
        corpo.GetProperty("indicadas").GetArrayLength().Should().Be(0);
    }

    // ---------- #225: a marca na lista e no detalhe da área ----------

    [Fact]
    public async Task ListarEDetalhar_Demonstracao_SoElaComAMarca()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var id = (await Demonstracoes.CriarAsync(api, token)).GetProperty("empresaId").GetGuid();
        var comum = await Equipe.CriarAsync(api);

        var lista = await EmpresasDaAreaAsync(token);
        var (_, detalhe) = await AreaAsync(HttpMethod.Get, $"/api/v1/nebrys/empresas/{id}", token);
        var (_, detalheComum) = await AreaAsync(HttpMethod.Get, $"/api/v1/nebrys/empresas/{comum.TenantId}", token);

        lista.Single(e => Cenarios.Id(e) == id).GetProperty("demonstracao").GetBoolean().Should().BeTrue();
        lista.Single(e => Cenarios.Id(e) == comum.TenantId).GetProperty("demonstracao").GetBoolean().Should().BeFalse();
        lista.Where(e => e.GetProperty("demonstracao").GetBoolean()).Select(Cenarios.Id)
            .Should().ContainSingle("há uma demonstração só").Which.Should().Be(id);
        detalhe.GetProperty("demonstracao").GetBoolean().Should().BeTrue();
        detalhe.GetProperty("situacao").GetString().Should().Be("ATIVA");
        detalheComum.GetProperty("demonstracao").GetBoolean().Should().BeFalse();
    }

    // ---------- F14-87: criar ----------

    [Fact]
    public async Task Criar_Operador_NasceComOsDadosFicticiosForaDoCicloESemEmail() // F14-87
    {
        var token = await OperadorNebrys.TokenAsync(api);

        var (status, corpo) = await AreaAsync(HttpMethod.Post, Rota, token);

        status.Should().Be(HttpStatusCode.Created, "corpo: {0}", corpo);
        var id = corpo.GetProperty("empresaId").GetGuid();
        corpo.GetProperty("nome").GetString().Should().Be(Dados.Empresa.Nome);
        corpo.GetProperty("indicadas").GetArrayLength().Should().Be(0);

        await using var db = api.DaEquipe(id);
        var tenant = await db.Tenants.SingleAsync();
        tenant.Demonstracao.Should().BeTrue();
        tenant.Situacao.Should().Be(SituacaoEmpresa.Ativa, "fora do ciclo de situação (design D1 a1)");
        tenant.FimDoTeste.Should().BeNull();
        var clientes = await db.Clientes.ToListAsync();
        clientes.Select(c => c.Nome).Should().BeEquivalentTo(Dados.Clientes.Select(c => c.Nome), "os mesmos dados do manual");
        clientes.Should().OnlyContain(c => !c.Exemplo, "a demonstração não é dado de exemplo");
        (await db.Solicitacoes.CountAsync()).Should().Be(Dados.Solicitacoes.Count);
        (await db.Solicitacoes.CountAsync(s => s.CategoriaId != null)).Should().Be(Dados.Solicitacoes.Count(s => s.Categoria != null));
        (await db.Tarefas.CountAsync()).Should().Be(Dados.Tarefas.Count);
        (await db.Apontamentos.CountAsync()).Should().BePositive();
        (await db.Categorias.CountAsync()).Should().Be(Dados.Categorias.Count);
        (await db.Recorrencias.CountAsync()).Should().Be(Dados.Recorrencias.Count);
        (await db.Cronometros.CountAsync()).Should().Be(1);
        (await db.Vinculos.CountAsync(v => v.ClienteId == null)).Should().Be(Dados.Pessoas.Count, "a equipe fictícia");
        (await db.Vinculos.CountAsync(v => v.ClienteId != null)).Should().Be(1, "o contato com acesso ao portal");
        (await db.Set<MensagemOutbox>().IgnoreQueryFilters().CountAsync(m => m.TenantId == id)).Should().Be(0, "nada vai à outbox");
        EmailsFicticios().SelectMany(e => api.Emails.Para(e)).Should().BeEmpty("nenhum e-mail sai (F14-89)");

        var detalhe = (await AreaAsync(HttpMethod.Get, $"/api/v1/nebrys/empresas/{id}", token)).Corpo;
        detalhe.GetProperty("situacao").GetString().Should().Be("ATIVA");
        detalhe.GetProperty("fimDoTeste").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Criar_ContasFicticias_SemSenhaNinguemEntra() // F14-87, D13: contas fictícias em .invalid
    {
        var token = await OperadorNebrys.TokenAsync(api);
        await Demonstracoes.CriarAsync(api, token);

        await using var escopo = api.App.Services.CreateAsyncScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        foreach (var email in ContasFicticias())
        {
            var conta = await usuarios.FindByEmailAsync(email);
            conta.Should().NotBeNull(email);
            conta!.PasswordHash.Should().BeNull("ninguém entra com a conta fictícia ({0})", email);
            email.Should().EndWith(".invalid");
        }

        foreach (var email in EmailsFicticios().Except(ContasFicticias(), StringComparer.OrdinalIgnoreCase))
            (await usuarios.FindByEmailAsync(email)).Should().BeNull("o contato sem portal não ganha conta ({0})", email);

        using var http = api.NovoHttp();
        using var login = await http.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative), new { email = Dados.Pessoas[0].Email, senha = Equipe.Senha });
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EsqueciSenha_ContaFicticia_RespostaNeutraESemEmail() // RF-NEB-02, #226: nada sai da demonstração
    {
        var token = await OperadorNebrys.TokenAsync(api);
        await Demonstracoes.CriarAsync(api, token);
        var comum = await Equipe.CriarAsync(api);
        var desconhecido = $"ninguem-{Guid.NewGuid():N}@exemplo.com";

        var (statusDesconhecido, corpoDesconhecido) = await EsqueciAsync(desconhecido);
        foreach (var email in ContasFicticias())
        {
            var (status, corpo) = await EsqueciAsync(email);
            status.Should().Be(statusDesconhecido, "a resposta não revela nada ({0})", email);
            corpo.Should().Be(corpoDesconhecido);
            api.Emails.Para(email).Should().BeEmpty("a conta fictícia não recebe a redefinição ({0})", email);
        }

        statusDesconhecido.Should().Be(HttpStatusCode.Accepted);
        await using (var escopo = api.App.Services.CreateAsyncScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            await using var db = api.DaEquipe(Guid.NewGuid());
            foreach (var email in ContasFicticias())
            {
                var conta = await usuarios.FindByEmailAsync(email);
                (await db.Set<TokenConta>().IgnoreQueryFilters().CountAsync(t => t.UsuarioId == conta!.Id))
                    .Should().Be(0, "nenhum link de redefinição é emitido ({0})", email);
            }
        }

        var (statusComum, corpoComum) = await EsqueciAsync(comum.EmailDono);
        statusComum.Should().Be(statusDesconhecido);
        corpoComum.Should().Be(corpoDesconhecido);
        api.Emails.Para(comum.EmailDono).Should().Contain(m => m.Categoria == "redefinicao-senha", "a conta comum continua recebendo");
    }

    [Fact]
    public async Task Criar_JaExiste_422SemOutra() // há uma demonstração só
    {
        var token = await OperadorNebrys.TokenAsync(api);
        await Demonstracoes.CriarAsync(api, token);

        var (status, corpo) = await AreaAsync(HttpMethod.Post, Rota, token);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        corpo.GetProperty("type").GetString().Should().Be(Erros + "transicao-invalida");
        await using var db = api.DaEquipe(Guid.NewGuid());
        (await db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Demonstracao)).Should().Be(1);
    }

    [Fact]
    public async Task Diario_DemonstracaoDeQualquerIdade_NuncaVenceNemEExpurgada() // RF-NEB-02
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var id = (await Demonstracoes.CriarAsync(api, token)).GetProperty("empresaId").GetGuid();
        var antigo = api.Relogio.Agora.AddDays(-400).ToUniversalTime();
        await using (var db = api.DaEquipe(id))
            await db.Database.ExecuteSqlAsync($"UPDATE tenants SET criado_em = {antigo} WHERE id = {id}");

        await DiarioAsync();

        await using var depois = api.DaEquipe(id);
        var tenant = await depois.Tenants.SingleOrDefaultAsync();
        tenant.Should().NotBeNull("o diário não apaga a demonstração");
        tenant!.Situacao.Should().Be(SituacaoEmpresa.Ativa);
        (await depois.Clientes.CountAsync()).Should().Be(Dados.Clientes.Count);
    }

    [Theory]
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task Diario_DemonstracaoMarcadaComSituacaoDeExpurgo_NaoEApagada(string situacao) // RF-NEB-02: a marca vale além da situação
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var id = (await Demonstracoes.CriarAsync(api, token)).GetProperty("empresaId").GetGuid();
        var agora = api.Relogio.Agora.ToUniversalTime();
        await using (var db = api.DaEquipe(id))
        {
            var desde = agora.AddYears(-2);
            var canceladaEm = situacao == "CANCELADA" ? desde : (DateTimeOffset?)null;
            await db.Database.ExecuteSqlAsync(
                $"UPDATE tenants SET situacao = {situacao}, somente_leitura_desde = {desde}, criado_em = {desde}, cancelada_em = {canceladaEm} WHERE id = {id}");
        }

        await DiarioAsync();

        await using var depois = api.DaEquipe(id);
        (await depois.Tenants.AnyAsync()).Should().BeTrue("a marca de demonstração impede o expurgo mesmo fora da situação ATIVA");
        (await depois.Clientes.CountAsync()).Should().Be(Dados.Clientes.Count);
    }

    [Fact]
    public async Task Diario_DemonstracaoEmTesteComOFimPassado_NaoVence() // RF-NEB-02: a marca vale além da situação
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var id = (await Demonstracoes.CriarAsync(api, token)).GetProperty("empresaId").GetGuid();
        var agora = api.Relogio.Agora.ToUniversalTime();
        await using (var db = api.DaEquipe(id))
        {
            var fim = agora.AddDays(-3);
            var criado = agora.AddDays(-20);
            await db.Database.ExecuteSqlAsync(
                $"UPDATE tenants SET situacao = {"TESTE"}, fim_do_teste = {fim}, criado_em = {criado} WHERE id = {id}");
        }

        await DiarioAsync();

        await using var depois = api.DaEquipe(id);
        (await depois.Tenants.SingleAsync()).Situacao.Should().Be(SituacaoEmpresa.Teste, "não vira VENCIDA");
    }

    [Fact]
    public async Task Criar_ContaComSenhaNoEmailFicticio_422SemCriarNada()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var email = Dados.Pessoas[0].Email;
        await using (var escopo = api.App.Services.CreateAsyncScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var conta = new Usuario(email, "Alguém que se cadastrou");
            (await usuarios.CreateAsync(conta, Equipe.Senha)).Succeeded.Should().BeTrue();
        }

        try
        {
            var (status, corpo) = await AreaAsync(HttpMethod.Post, Rota, token);

            status.Should().Be(HttpStatusCode.UnprocessableEntity);
            corpo.GetProperty("type").GetString().Should().Be(Erros + "transicao-invalida");
            await using var db = api.DaEquipe(Guid.NewGuid());
            (await db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Demonstracao)).Should().Be(0, "nada é criado");
        }
        finally
        {
            await using var limpeza = api.App.Services.CreateAsyncScope();
            var usuarios = limpeza.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            if (await usuarios.FindByEmailAsync(email) is { } conta)
                await usuarios.DeleteAsync(conta);
        }
    }

    // ---------- F14-88: restaurar ----------

    [Fact]
    public async Task Restaurar_DemonstracaoUsada_DadosVoltamAoInicioENenhumaOutraEmpresaETocada() // F14-88
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var id = (await Demonstracoes.CriarAsync(api, token)).GetProperty("empresaId").GetGuid();
        var indicada = await Equipe.CriarAsync(api);
        var clienteDela = await Cenarios.ClienteAsync(indicada, "Cliente da empresa da pessoa indicada");
        var outra = await Equipe.CriarAsync(api);
        await Cenarios.ClienteAsync(outra, "Cliente de outra empresa");
        await Demonstracoes.IndicarAsync(api, token, indicada.EmailDono);
        var naDemonstracao = await Demonstracoes.SessaoNaDemonstracaoAsync(api, indicada, id);
        // A demonstração é usada: um cliente novo e a empresa renomeada.
        await indicada.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", new { nome = "Cliente criado na demonstração" }, naDemonstracao);
        await using (var db = api.DaEquipe(id))
            await db.Database.ExecuteSqlAsync($"UPDATE tenants SET nome = {"Nome mudado"} WHERE id = {id}");
        var clientesDasOutras = await ClientesAsync(indicada.TenantId, outra.TenantId);

        var (status, corpo) = await AreaAsync(HttpMethod.Post, $"{Rota}/restauracao", token);

        status.Should().Be(HttpStatusCode.OK, "corpo: {0}", corpo);
        corpo.GetProperty("nome").GetString().Should().Be(Dados.Empresa.Nome);
        await using (var db = api.DaEquipe(id))
        {
            (await db.Tenants.SingleAsync()).Nome.Should().Be(Dados.Empresa.Nome);
            (await db.Clientes.Select(c => c.Nome).ToListAsync()).Should().BeEquivalentTo(Dados.Clientes.Select(c => c.Nome));
            (await db.Solicitacoes.CountAsync()).Should().Be(Dados.Solicitacoes.Count);
            (await db.Solicitacoes.Select(s => s.Numero).ToListAsync()).Should().BeEquivalentTo(
                Enumerable.Range(1, Dados.Solicitacoes.Count), "o protocolo recomeça");
            (await db.Tarefas.CountAsync()).Should().Be(Dados.Tarefas.Count);
            (await db.Categorias.CountAsync()).Should().Be(Dados.Categorias.Count);
            (await db.Recorrencias.CountAsync()).Should().Be(Dados.Recorrencias.Count);
            (await db.Cronometros.CountAsync()).Should().Be(1);
            (await db.Papeis.CountAsync()).Should().Be(Papel.Padroes(id).Count);
            (await db.Set<MensagemOutbox>().IgnoreQueryFilters().CountAsync(m => m.TenantId == id)).Should().Be(0);
        }

        corpo.GetProperty("indicadas").EnumerateArray().Select(p => p.GetProperty("email").GetString())
            .Should().BeEquivalentTo([indicada.EmailDono], "quem foi indicado continua");
        (await PapelNaDemonstracaoAsync(indicada, id)).Should().Be(Papel.NomeDono);
        (await ClientesAsync(indicada.TenantId, outra.TenantId)).Should().BeEquivalentTo(clientesDasOutras, "nenhuma outra empresa é tocada");
        (await indicada.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/clientes/{clienteDela}")).GetProperty("nome").GetString()
            .Should().Be("Cliente da empresa da pessoa indicada");
        EmailsFicticios().SelectMany(e => api.Emails.Para(e)).Should().BeEmpty();
    }

    [Fact]
    public async Task Restaurar_SemDemonstracao_404()
    {
        var token = await OperadorNebrys.TokenAsync(api);

        var (status, _) = await AreaAsync(HttpMethod.Post, $"{Rota}/restauracao", token);

        status.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- F14-89: nada sai para fora ----------

    [Fact]
    public async Task ConviteDaEquipe_NaDemonstracao_422SemEnvioENadaGravado() // F14-89
    {
        var (demonstracao, pessoa, sessao) = await NaDemonstracaoAsync();
        var convidado = $"convidado-{Guid.NewGuid():N}@exemplo.com";
        Guid membro;
        await using (var db = api.DaEquipe(demonstracao))
            membro = (await db.Papeis.SingleAsync(p => p.Padrao && p.Nome == Papel.NomeMembro)).Id;

        var problema = await pessoa.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, "/api/v1/convites", new { email = convidado, papelId = membro }, sessao);

        problema.GetProperty("type").GetString().Should().Be(Erros + "demonstracao-sem-envio");
        problema.GetProperty("detail").GetString().Should().Contain("demonstração");
        api.Emails.Para(convidado).Should().BeEmpty();
        await using var depois = api.DaEquipe(demonstracao);
        (await depois.ConvitesEquipe.CountAsync()).Should().Be(0, "nada é gravado");
    }

    [Fact]
    public async Task AcessoAoPortal_NaDemonstracao_422SemEnvio() // F14-89
    {
        var (demonstracao, pessoa, sessao) = await NaDemonstracaoAsync();
        var (cliente, contato, email) = await ContatoSemPortalAsync(demonstracao);

        var problema = await pessoa.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos/{contato}/acesso-portal",
            new { escopoPortal = "PROPRIAS" }, sessao);

        problema.GetProperty("type").GetString().Should().Be(Erros + "demonstracao-sem-envio");
        api.Emails.Para(email).Should().BeEmpty();
        await using var db = api.DaEquipe(demonstracao);
        (await db.ConvitesPortal.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task LinkDeAcesso_ComEmail422_SemEmailEmitido() // F14-89
    {
        var (demonstracao, pessoa, sessao) = await NaDemonstracaoAsync();
        var (solicitacao, contato, email) = await SolicitacaoComContatoAsync(demonstracao, "Caixa não imprime o cupom desde ontem");

        var comEmail = await pessoa.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal",
            new { contatoId = contato }, sessao);
        await pessoa.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal",
            new { contatoId = contato, enviarEmail = false }, sessao);

        comEmail.GetProperty("type").GetString().Should().Be(Erros + "demonstracao-sem-envio");
        api.Emails.Para(email).Should().BeEmpty("o link sem e-mail é emitido e não sai nada");
    }

    [Fact]
    public async Task Interacao_ExternaNaDemonstracao_NaoAvisaOContato() // F14-89
    {
        var (demonstracao, pessoa, sessao) = await NaDemonstracaoAsync();
        var (solicitacao, _, email) = await SolicitacaoComContatoAsync(demonstracao, "Caixa não imprime o cupom desde ontem");
        var (comum, solicitacaoComum, emailComum) = await SolicitacaoComContatoNumaEquipeComumAsync();

        await pessoa.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/interacoes",
            new { texto = "A peça nova chegou.", visibilidade = "EXTERNA" }, sessao);
        await comum.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacaoComum}/interacoes",
            new { texto = "A peça nova chegou.", visibilidade = "EXTERNA" });
        await TickAsync();

        api.Emails.Para(email).Should().BeEmpty();
        EmailsFicticios().SelectMany(e => api.Emails.Para(e)).Should().BeEmpty();
        api.Emails.Para(emailComum).Should().Contain(m => m.Categoria == "aviso-mensagem", "numa empresa comum a mesma ação avisa o contato");
    }

    [Fact]
    public async Task AlertasSla_NaDemonstracao_NenhumEmailNemAQuemFoiIndicado() // F14-89
    {
        var (_, pessoa, _) = await NaDemonstracaoAsync();
        var comum = await EquipeComumComSlaUrgenteAsync();
        var inicio = api.Relogio.Agora;

        api.Relogio.Agora = inicio.AddDays(3);
        try
        {
            await TickAsync();
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }

        api.Emails.Para(pessoa.EmailDono).Should().NotContain(m => m.Categoria.StartsWith("aviso-sla", StringComparison.Ordinal));
        EmailsFicticios().SelectMany(e => api.Emails.Para(e)).Should().BeEmpty();
        api.Emails.Para(comum.EmailDono).Should().Contain(
            m => m.Categoria == "aviso-sla-violado", "numa empresa comum o mesmo tick manda o alerta");
    }

    // ---------- F14-90: quem foi indicado vê; quem não foi, não ----------

    [Fact]
    public async Task Entrar_PessoaIndicada_VeADemonstracaoComoDona() // F14-90
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var id = (await Demonstracoes.CriarAsync(api, token)).GetProperty("empresaId").GetGuid();
        var pessoa = await Equipe.CriarAsync(api);

        await Demonstracoes.IndicarAsync(api, token, pessoa.EmailDono);

        var eu = await EuDoLoginAsync(pessoa.EmailDono);
        eu.GetProperty("tenants").EnumerateArray().Should().Contain(
            t => t.GetProperty("id").GetGuid() == id && t.GetProperty("papel").GetString() == Papel.NomeDono);
        var naDemonstracao = await Demonstracoes.SessaoNaDemonstracaoAsync(api, pessoa, id);
        var perfil = await pessoa.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: naDemonstracao);
        perfil.GetProperty("papel").GetString().Should().Be(Papel.NomeDono);
        perfil.GetProperty("tenant").GetProperty("id").GetGuid().Should().Be(id);
        perfil.GetProperty("tenant").GetProperty("demonstracao").GetBoolean().Should().BeTrue();
        perfil.GetProperty("tenant").GetProperty("situacao").GetString().Should().Be("ATIVA");
        var clientes = await pessoa.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/clientes?tamanhoPagina=50", token: naDemonstracao);
        clientes.GetProperty("itens").EnumerateArray().Select(c => c.GetProperty("nome").GetString())
            .Should().BeEquivalentTo(Dados.Clientes.Select(c => c.Nome));
        api.Emails.Para(pessoa.EmailDono).Should().NotContain(m => m.Categoria == "convite-equipe", "a indicação não manda convite");
    }

    [Fact]
    public async Task Entrar_PessoaNaoIndicada_NaoVeADemonstracao() // F14-90
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var id = (await Demonstracoes.CriarAsync(api, token)).GetProperty("empresaId").GetGuid();
        var indicada = await Equipe.CriarAsync(api);
        await Demonstracoes.IndicarAsync(api, token, indicada.EmailDono);
        var outra = await Equipe.CriarAsync(api);

        var eu = await EuDoLoginAsync(outra.EmailDono);

        eu.GetProperty("tenants").EnumerateArray().Should().NotContain(t => t.GetProperty("id").GetGuid() == id);
        using (var troca = await outra.EnviarAsync(HttpMethod.Post, "/api/v1/auth/tenant-ativo", new { tenantId = id }))
            troca.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var clientes = await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/clientes?tamanhoPagina=50");
        clientes.GetProperty("itens").EnumerateArray().Select(c => c.GetProperty("nome").GetString())
            .Should().NotIntersectWith(Dados.Clientes.Select(c => c.Nome), "os dados da demonstração ficam no tenant dela");
    }

    [Fact]
    public async Task Retirar_PessoaIndicada_DeixaDeVerASessaoParaNaRenovacao()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var id = (await Demonstracoes.CriarAsync(api, token)).GetProperty("empresaId").GetGuid();
        var pessoa = await Equipe.CriarAsync(api);
        await Demonstracoes.IndicarAsync(api, token, pessoa.EmailDono);
        var usuario = (await EuDoLoginAsync(pessoa.EmailDono)).GetProperty("usuarioId").GetGuid();
        await Demonstracoes.SessaoNaDemonstracaoAsync(api, pessoa, id);

        var (status, _) = await AreaAsync(HttpMethod.Delete, $"{Rota}/indicacoes/{usuario}", token);

        status.Should().Be(HttpStatusCode.NoContent);
        (await EuDoLoginAsync(pessoa.EmailDono)).GetProperty("tenants").EnumerateArray()
            .Should().NotContain(t => t.GetProperty("id").GetGuid() == id);
        (await AreaAsync(HttpMethod.Get, Rota, token)).Corpo.GetProperty("indicadas").GetArrayLength().Should().Be(0);
        await using var db = api.DaEquipe(id);
        (await db.Set<RefreshToken>().Where(r => r.UsuarioId == usuario && r.TenantId == id).ToListAsync())
            .Should().NotBeEmpty().And.OnlyContain(r => r.RevogadoEm != null || r.ConsumidoEm != null, "a sessão na demonstração para");
    }

    [Fact]
    public async Task Retirar_ContaFicticia_404()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var id = (await Demonstracoes.CriarAsync(api, token)).GetProperty("empresaId").GetGuid();
        Guid ficticia;
        await using (var db = api.DaEquipe(id))
            ficticia = await db.Vinculos.Where(v => v.ClienteId == null).Select(v => v.UsuarioId).FirstAsync();

        var (status, _) = await AreaAsync(HttpMethod.Delete, $"{Rota}/indicacoes/{ficticia}", token);

        status.Should().Be(HttpStatusCode.NotFound);
        await using var depois = api.DaEquipe(id);
        (await depois.Vinculos.CountAsync(v => v.ClienteId == null)).Should().Be(Dados.Pessoas.Count);
    }

    // ---------- F14-110: e-mail sem conta ----------

    [Fact]
    public async Task Indicar_EmailSemConta_400ComAExplicacaoSemCriarNadaNemEnviar() // F14-110
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var id = (await Demonstracoes.CriarAsync(api, token)).GetProperty("empresaId").GetGuid();
        var email = $"sem-conta-{Guid.NewGuid():N}@exemplo.com";

        var (status, corpo) = await AreaAsync(HttpMethod.Post, $"{Rota}/indicacoes", token, new { email });

        status.Should().Be(HttpStatusCode.BadRequest);
        corpo.GetProperty("errors").GetProperty("email")[0].GetString().Should().Contain("não tem conta");
        await using (var escopo = api.App.Services.CreateAsyncScope())
            (await escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>().FindByEmailAsync(email)).Should().BeNull("nenhuma conta é criada");
        api.Emails.Para(email).Should().BeEmpty("nenhum e-mail sai");
        await using var db = api.DaEquipe(id);
        (await db.Vinculos.CountAsync()).Should().Be(Dados.Pessoas.Count + 1, "nenhum vínculo novo");
    }

    [Fact]
    public async Task Indicar_EmailDeContaFicticia_400ComoSemConta() // F14-110: a conta fictícia não entra
    {
        var token = await OperadorNebrys.TokenAsync(api);
        await Demonstracoes.CriarAsync(api, token);

        var (status, corpo) = await AreaAsync(HttpMethod.Post, $"{Rota}/indicacoes", token, new { email = Dados.Pessoas[1].Email });

        status.Should().Be(HttpStatusCode.BadRequest);
        corpo.GetProperty("errors").TryGetProperty("email", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Indicar_JaIndicada_422()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        await Demonstracoes.CriarAsync(api, token);
        var pessoa = await Equipe.CriarAsync(api);
        await Demonstracoes.IndicarAsync(api, token, pessoa.EmailDono);

        var (status, corpo) = await AreaAsync(HttpMethod.Post, $"{Rota}/indicacoes", token, new { email = pessoa.EmailDono });

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        corpo.GetProperty("type").GetString().Should().Be(Erros + "transicao-invalida");
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-arroba")]
    public async Task Indicar_EmailInvalido_400NoCampo(string email)
    {
        var token = await OperadorNebrys.TokenAsync(api);
        await Demonstracoes.CriarAsync(api, token);

        var (status, corpo) = await AreaAsync(HttpMethod.Post, $"{Rota}/indicacoes", token, new { email });

        status.Should().Be(HttpStatusCode.BadRequest);
        corpo.GetProperty("errors").TryGetProperty("email", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Indicar_SemDemonstracao_404()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var pessoa = await Equipe.CriarAsync(api);

        var (status, _) = await AreaAsync(HttpMethod.Post, $"{Rota}/indicacoes", token, new { email = pessoa.EmailDono });

        status.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- apoio ----------

    /// <summary>Pede a redefinição de senha (rota pública) e devolve o status e o corpo como texto.</summary>
    private async Task<(HttpStatusCode Status, string Corpo)> EsqueciAsync(string email)
    {
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/senha/esqueci", UriKind.Relative), new { email });
        return (resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
    }

    /// <summary>Os e-mails das contas e dos contatos fictícios.</summary>
    private static IEnumerable<string> EmailsFicticios() =>
        Dados.Pessoas.Select(p => p.Email).Concat(Dados.Clientes.Select(c => c.Contato.Email));

    /// <summary>Os e-mails que ganham conta fictícia: a equipe e o contato com acesso ao portal (os outros contatos, não).</summary>
    private static IEnumerable<string> ContasFicticias() =>
        Dados.Pessoas.Select(p => p.Email).Concat(Dados.Clientes.Where(c => c.Chave == Dados.Portal?.Cliente).Select(c => c.Contato.Email));

    /// <summary>Demonstração criada, uma pessoa indicada e a sessão dela lá.</summary>
    private async Task<(Guid Demonstracao, Equipe Pessoa, string Sessao)> NaDemonstracaoAsync()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var id = (await Demonstracoes.CriarAsync(api, token)).GetProperty("empresaId").GetGuid();
        var pessoa = await Equipe.CriarAsync(api);
        await Demonstracoes.IndicarAsync(api, token, pessoa.EmailDono);
        return (id, pessoa, await Demonstracoes.SessaoNaDemonstracaoAsync(api, pessoa, id));
    }

    private async Task<(Guid Cliente, Guid Contato, string Email)> ContatoSemPortalAsync(Guid demonstracao)
    {
        await using var db = api.DaEquipe(demonstracao);
        var cliente = (await db.Clientes.ToListAsync()).First(c => c.Contatos.All(x => x.UsuarioId == null));
        var contato = cliente.Contatos.Single();
        return (cliente.Id, contato.Id, contato.Email);
    }

    private async Task<(Guid Solicitacao, Guid Contato, string Email)> SolicitacaoComContatoAsync(Guid demonstracao, string titulo)
    {
        await using var db = api.DaEquipe(demonstracao);
        var solicitacao = await db.Solicitacoes.SingleAsync(s => s.Titulo == titulo);
        var cliente = await db.Clientes.SingleAsync(c => c.Id == solicitacao.ClienteId);
        var contato = cliente.Contatos.Single(c => c.Id == solicitacao.ContatoId);
        return (solicitacao.Id, contato.Id, contato.Email);
    }

    /// <summary>Controle do F14-89: uma empresa comum com uma solicitação de um contato sem conta.</summary>
    private async Task<(Equipe Equipe, Guid Solicitacao, string Email)> SolicitacaoComContatoNumaEquipeComumAsync()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var email = $"contato-{Guid.NewGuid():N}@bomgrao.com.br";
        var contato = Cenarios.Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos", new { nome = "Carlos Prado", email }));
        var solicitacao = Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, contatoId = contato, titulo = "Caixa não imprime o cupom desde ontem", prioridade = "MEDIA",
        }));
        return (equipe, solicitacao, email);
    }

    /// <summary>Controle do F14-89: uma empresa comum com uma solicitação urgente de SLA de 1 h, com o Dono de responsável.</summary>
    private async Task<Equipe> EquipeComumComSlaUrgenteAsync()
    {
        var equipe = await Equipe.CriarAsync(api);
        var dono = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("usuarioId").GetGuid();
        var cliente = await Cenarios.ClienteAsync(equipe);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", new
        {
            clienteId = cliente, tipo = "BANCO_HORAS", inicio = "2026-09-01", franquiaMinutos = 600, sla = Cenarios.Sla(60, 60),
        });
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, titulo = "Servidor caiu", prioridade = "URGENTE", responsavelId = dono,
        });
        return equipe;
    }

    private async Task<string?> PapelNaDemonstracaoAsync(Equipe pessoa, Guid demonstracao)
    {
        var usuario = (await EuDoLoginAsync(pessoa.EmailDono)).GetProperty("usuarioId").GetGuid();
        await using var db = api.DaEquipe(demonstracao);
        var vinculo = await db.Vinculos.SingleOrDefaultAsync(v => v.UsuarioId == usuario);
        return vinculo is null ? null : (await db.Papeis.SingleAsync(p => p.Id == vinculo.PapelId)).Nome;
    }

    /// <summary>Os clientes das empresas (empresa, id, nome e se está ativo), para conferir que nada mudou nelas.</summary>
    private async Task<List<(Guid Empresa, Guid Id, string Nome, bool Ativo)>> ClientesAsync(params Guid[] empresas)
    {
        var clientes = new List<(Guid Empresa, Guid Id, string Nome, bool Ativo)>();
        foreach (var empresa in empresas)
        {
            await using var db = api.DaEquipe(empresa);
            var linhas = await db.Clientes.Select(c => new { c.Id, c.Nome, c.Ativo }).ToListAsync();
            clientes.AddRange(linhas.Select(c => (empresa, c.Id, c.Nome, c.Ativo)));
        }

        return clientes;
    }

    /// <summary>Entra com a senha e devolve o <c>/auth/eu</c> da sessão.</summary>
    private async Task<JsonElement> EuDoLoginAsync(string email)
    {
        using var http = api.NovoHttp();
        using var login = await http.PostAsJsonAsync(new Uri("/api/v1/auth/login", UriKind.Relative), new { email, senha = Equipe.Senha });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        using var eu = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Get, "/api/v1/auth/eu", token);
        eu.StatusCode.Should().Be(HttpStatusCode.OK);
        return await eu.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Todas as páginas da lista de empresas da área: a coleção compartilha o banco.</summary>
    private async Task<List<JsonElement>> EmpresasDaAreaAsync(string token)
    {
        var itens = new List<JsonElement>();
        for (var pagina = 1; ; pagina++)
        {
            var (status, corpo) = await AreaAsync(HttpMethod.Get, $"/api/v1/nebrys/empresas?pagina={pagina}&tamanhoPagina=200", token);
            status.Should().Be(HttpStatusCode.OK);
            var desta = corpo.GetProperty("itens").EnumerateArray().ToList();
            itens.AddRange(desta);
            if (desta.Count == 0 || itens.Count >= corpo.GetProperty("total").GetInt32())
                return itens;
        }
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> AreaAsync(
        HttpMethod metodo, string rota, string token, object? corpo = null)
    {
        using var resposta = await Demonstracoes.EnviarAsync(api, metodo, rota, token, corpo);
        var texto = await resposta.Content.ReadAsStringAsync();
        return (resposta.StatusCode, texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone());
    }

    private async Task TickAsync()
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/tick", UriKind.Relative));
        requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
        using var resposta = await http.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task DiarioAsync()
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/diario", UriKind.Relative));
        requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
        using var resposta = await http.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
    }
}
