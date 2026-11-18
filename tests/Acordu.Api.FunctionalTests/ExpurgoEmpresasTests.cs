using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence;
using Acordu.Infrastructure.Persistence.Cadastros;
using Acordu.Infrastructure.Seguranca;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14i (F14-29 a F14-38, F14-104, F14-112; design D6): o passo <c>expurgo-empresas</c> do diário apaga a
/// empresa VENCIDA ou CANCELADA há o prazo (30 dias) ou mais, com tudo dela e as contas que só tinham presença nela; o
/// registro do documento fica. O expurgo do cadastro abandonado (RF-AUT-07) passa a apagar o registro do documento.
/// As situações são gravadas direto no banco (as ações da área da Nebrys são do F14l), como nos testes do F14a.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class ExpurgoEmpresasTests(ApiFixture api)
{
    private static readonly TimeSpan Prazo = TimeSpan.FromDays(30);
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    [Theory] // F14-29
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task Diario_SoLeituraHaOPrazo_ApagaEmpresaArquivosEContasSoDela(string situacao)
    {
        var empresa = await EmpresaComTudoAsync();
        await SoLeituraAsync(empresa.Equipe.TenantId, situacao, api.Relogio.Agora - Prazo);

        var resultado = await DiarioAsync();

        // ">= 1": a coleção compartilha o banco, e empresas de outros testes também chegam ao prazo.
        resultado.GetProperty("empresasExpurgadas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        resultado.GetProperty("falhas").GetInt32().Should().Be(0);
        (await LinhasDaEmpresaAsync(empresa.Equipe.TenantId)).Should().BeEmpty("nenhuma tabela com TenantId guarda linha da empresa");
        await using var db = api.DaEquipe(empresa.Equipe.TenantId);
        (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == empresa.Equipe.TenantId)).Should().BeFalse();
        api.Arquivos.ComPrefixo($"{empresa.Equipe.TenantId}/").Should().Be(0, "anexos e logotipo saem do storage");
        foreach (var conta in new[] { empresa.Dono, empresa.Contato })
        {
            (await db.Users.AnyAsync(u => u.Id == conta)).Should().BeFalse("a conta só tinha presença nesta empresa");
            (await db.Set<RefreshToken>().AnyAsync(r => r.UsuarioId == conta)).Should().BeFalse();
            (await db.Set<TokenConta>().AnyAsync(t => t.UsuarioId == conta)).Should().BeFalse();
        }
    }

    [Theory] // F14-30
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task Diario_SoLeituraComMenosQueOPrazo_NadaEApagado(string situacao)
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        await SoLeituraAsync(equipe.TenantId, situacao, api.Relogio.Agora - Prazo + TimeSpan.FromMinutes(1));
        var antes = await LinhasDaEmpresaAsync(equipe.TenantId);

        await DiarioAsync();

        (await LinhasDaEmpresaAsync(equipe.TenantId)).Should().BeEquivalentTo(antes);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/clientes/{cliente}");
    }

    [Theory] // F14-31
    [InlineData("TESTE")]
    [InlineData("ATIVA")]
    public async Task Diario_EmTesteOuAtivaDeQualquerIdade_NuncaEApagada(string situacao)
    {
        var equipe = await Equipe.CriarAsync(api);
        var agora = api.Relogio.Agora;
        // Velha de anos e, na ATIVA, até com datas de só leitura antigas (ex.: uma reativada): a situação é o que vale. A
        // demonstração é ATIVA (design D1, a1).
        await using (var db = api.DaEquipe(equipe.TenantId))
        {
            await db.Database.ExecuteSqlAsync($"""
                UPDATE tenants SET situacao = {situacao}, criado_em = {agora.AddYears(-3)},
                    fim_do_teste = {(situacao == "TESTE" ? agora.AddDays(10) : null)},
                    somente_leitura_desde = {(situacao == "ATIVA" ? agora.AddYears(-2) : null)}
                WHERE id = {equipe.TenantId}
                """);
        }

        await DiarioAsync();

        await using (var db = api.DaEquipe(equipe.TenantId))
            (await db.Tenants.AnyAsync()).Should().BeTrue();
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu");
    }

    [Fact] // F14-32: vínculo e sessão em outra empresa
    public async Task Diario_PessoaComVinculoEmOutraEmpresa_FicaComAContaEOVinculoDeLa()
    {
        var expurgavel = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        var tokenNaOutra = await outra.ComPapelAsync(Papel.NomeMembro);
        var pessoa = await UsuarioAsync(outra, tokenNaOutra);
        await VincularAsync(expurgavel.TenantId, pessoa);
        await SoLeituraAsync(expurgavel.TenantId, "CANCELADA", api.Relogio.Agora - Prazo);

        await DiarioAsync();

        await using (var db = api.DaEquipe(outra.TenantId))
        {
            (await db.Users.AnyAsync(u => u.Id == pessoa)).Should().BeTrue();
            (await db.Vinculos.AnyAsync(v => v.UsuarioId == pessoa)).Should().BeTrue("o vínculo na outra empresa fica");
            (await db.Vinculos.IgnoreQueryFilters().AnyAsync(v => v.UsuarioId == pessoa && v.TenantId == expurgavel.TenantId))
                .Should().BeFalse("o desta empresa sai");
        }

        await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: tokenNaOutra);
    }

    [Fact] // F14-32: contato com conta em outra empresa
    public async Task Diario_PessoaQueEContatoEmOutraEmpresa_FicaComAContaEOAcessoDeLa()
    {
        var expurgavel = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        var contato = await ContatosDoPortal.CriarAsync(api, outra, await Cenarios.ClienteAsync(outra));
        Guid pessoa;
        await using (var db = api.DaEquipe(outra.TenantId))
            pessoa = (await db.Users.SingleAsync(u => u.Email == contato.Email)).Id;
        await VincularAsync(expurgavel.TenantId, pessoa);
        await SoLeituraAsync(expurgavel.TenantId, "VENCIDA", api.Relogio.Agora - Prazo);

        await DiarioAsync();

        await using (var db = api.DaEquipe(outra.TenantId))
            (await db.Users.AnyAsync(u => u.Id == pessoa)).Should().BeTrue();
        await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/portal/eu", token: contato.Token);
    }

    [Fact] // F14-32: sessão (refresh token) em outra empresa, sem vínculo lá
    public async Task Diario_PessoaComSessaoEmOutraEmpresa_FicaComAConta()
    {
        var expurgavel = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        var pessoa = await UsuarioAsync(expurgavel, expurgavel.Dono);
        await using (var db = api.DaEquipe(outra.TenantId))
        {
            db.Add(new RefreshToken(pessoa, outra.TenantId, Guid.NewGuid().ToString("N"), api.Relogio.Agora, api.Relogio.Agora.AddDays(5)));
            await db.SaveChangesAsync();
        }

        await SoLeituraAsync(expurgavel.TenantId, "CANCELADA", api.Relogio.Agora - Prazo);

        await DiarioAsync();

        await using var verificacao = api.DaEquipe(outra.TenantId);
        (await verificacao.Users.AnyAsync(u => u.Id == pessoa)).Should().BeTrue("a conta tem sessão em outra empresa");
        (await verificacao.Set<RefreshToken>().AnyAsync(r => r.UsuarioId == pessoa && r.TenantId == outra.TenantId)).Should().BeTrue();
        (await verificacao.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == expurgavel.TenantId)).Should().BeFalse();
    }

    [Fact] // borda: só leitura sem a data de início não tem de onde contar o prazo
    public async Task Diario_SoLeituraSemDataDeInicio_NaoEApagada()
    {
        var equipe = await Equipe.CriarAsync(api);
        await using (var db = api.DaEquipe(equipe.TenantId))
            await db.Database.ExecuteSqlAsync($"UPDATE tenants SET situacao = 'VENCIDA', somente_leitura_desde = NULL WHERE id = {equipe.TenantId}");

        await DiarioAsync();

        await using var verificacao = api.DaEquipe(equipe.TenantId);
        (await verificacao.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == equipe.TenantId)).Should().BeTrue();
    }

    [Fact] // F14-33
    public async Task Refresh_DepoisDoExpurgo_Retorna401()
    {
        var equipe = await Equipe.CriarAsync(api);
        await SoLeituraAsync(equipe.TenantId, "CANCELADA", api.Relogio.Agora - Prazo);

        await DiarioAsync();

        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/auth/refresh", UriKind.Relative));
        requisicao.Headers.Add("Cookie", $"acordu_refresh={equipe.RefreshDono}");
        using var resposta = await http.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // F14-33
    public async Task Eu_ComAccessTokenDaEmpresaExpurgada_Retorna401()
    {
        var equipe = await Equipe.CriarAsync(api);
        await SoLeituraAsync(equipe.TenantId, "VENCIDA", api.Relogio.Agora - Prazo);

        await DiarioAsync();

        using var resposta = await equipe.GetAsync("/api/v1/auth/eu");
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // F14-34
    public async Task Diario_ComExpurgo_NaoTocaOutrasEmpresas()
    {
        var expurgavel = await EmpresaComTudoAsync();
        var outra = await EmpresaComTudoAsync();
        await SoLeituraAsync(expurgavel.Equipe.TenantId, "CANCELADA", api.Relogio.Agora - Prazo);
        var antes = await LinhasDaEmpresaAsync(outra.Equipe.TenantId);
        var arquivosAntes = api.Arquivos.ComPrefixo($"{outra.Equipe.TenantId}/");
        antes.Should().ContainKey("auditoria");

        await DiarioAsync();

        await using (var db = api.DaEquipe(expurgavel.Equipe.TenantId))
            (await db.Tenants.AnyAsync()).Should().BeFalse();
        (await LinhasDaEmpresaAsync(outra.Equipe.TenantId)).Should().BeEquivalentTo(antes, "dados, auditoria e rastros da outra ficam");
        api.Arquivos.ComPrefixo($"{outra.Equipe.TenantId}/").Should().Be(arquivosAntes).And.BeGreaterThan(0);
        await using (var db = api.DaEquipe(outra.Equipe.TenantId))
        {
            (await db.Users.AnyAsync(u => u.Id == outra.Dono)).Should().BeTrue();
            (await db.Users.AnyAsync(u => u.Id == outra.Contato)).Should().BeTrue();
        }

        await outra.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{outra.Solicitacao}");
    }

    [Fact] // F14-35
    public async Task Diario_RodadoDeNovo_NaoApagaMais()
    {
        var equipe = await Equipe.CriarAsync(api);
        await SoLeituraAsync(equipe.TenantId, "CANCELADA", api.Relogio.Agora - Prazo);
        (await DiarioAsync()).GetProperty("empresasExpurgadas").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        var segundo = await DiarioAsync();

        segundo.GetProperty("empresasExpurgadas").GetInt32().Should().Be(0);
        segundo.GetProperty("falhas").GetInt32().Should().Be(0);
    }

    [Fact] // F14-36
    public async Task Diario_ExpurgoDeUmaEmpresaFalha_ContaEmFalhasESegueComAsDemaisEOsOutrosPassos()
    {
        var quebrada = await Equipe.CriarAsync(api);
        var normal = await Equipe.CriarAsync(api);
        await SoLeituraAsync(quebrada.TenantId, "CANCELADA", api.Relogio.Agora - Prazo);
        await SoLeituraAsync(normal.TenantId, "CANCELADA", api.Relogio.Agora - Prazo);
        // Falha forçada só na empresa "quebrada": apagar os papéis dela dispara um erro no banco.
        await using (var db = api.DaEquipe(quebrada.TenantId))
        {
            await db.Database.ExecuteSqlRawAsync(string.Format(CultureInfo.InvariantCulture, """
                CREATE OR REPLACE FUNCTION teste_f14_falha() RETURNS trigger AS $$ BEGIN RAISE EXCEPTION 'falha forçada'; END $$ LANGUAGE plpgsql;
                CREATE TRIGGER teste_f14_falha BEFORE DELETE ON papeis FOR EACH ROW WHEN (OLD.tenant_id = '{0}') EXECUTE FUNCTION teste_f14_falha();
                """, quebrada.TenantId));
        }

        JsonElement resultado;
        try
        {
            resultado = await DiarioAsync();
        }
        finally
        {
            await using var db = api.DaEquipe(quebrada.TenantId);
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER teste_f14_falha ON papeis; DROP FUNCTION teste_f14_falha();");
        }

        resultado.GetProperty("falhas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        resultado.GetProperty("empresasExpurgadas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        resultado.TryGetProperty("marcasOperacaoRemovidas", out _).Should().BeTrue("os passos seguintes rodaram");
        await using (var db = api.DaEquipe(quebrada.TenantId))
        {
            (await db.Tenants.AnyAsync()).Should().BeTrue("a transação da empresa que falhou foi desfeita");
            (await db.Papeis.CountAsync()).Should().Be(4);
        }

        await using (var db = api.DaEquipe(normal.TenantId))
            (await db.Tenants.AnyAsync()).Should().BeFalse("a falha de uma não para as demais");
    }

    [Fact] // F14-37
    public async Task Diario_EmpresaComDocumento_FicaSoORegistroDoDocumento()
    {
        var documento = DocumentosDeTeste.Novo();
        var (token, _) = await RegistrarAsync(documento);
        var tenantId = await TenantDoTokenAsync(token);
        await SoLeituraAsync(tenantId, "VENCIDA", api.Relogio.Agora - Prazo);

        await DiarioAsync();

        await using (var db = api.DaEquipe(tenantId))
        {
            (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenantId)).Should().BeFalse();
            var registro = await db.Set<DocumentoTeste>().AsNoTracking().SingleAsync(d => d.Hash == DocumentosDeTeste.Hash(documento));
            registro.LiberadoEm.Should().BeNull();
            (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Documento == documento)).Should().BeFalse("o documento em claro saiu com a empresa");
        }

        var (status, corpo) = await RegistrarCorpoAsync(documento);
        status.Should().Be(HttpStatusCode.BadRequest, "o documento já teve teste, mesmo com a empresa expurgada");
        corpo.GetProperty("errors").GetProperty("documento")[0].GetString().Should().Contain("fale com a Nebrys");
    }

    [Fact] // F14-38
    public async Task Diario_EmpresaComSugestao_ASugestaoFicaSemEmpresaNemPessoa()
    {
        var equipe = await Equipe.CriarAsync(api);
        var id = (await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/sugestoes",
            new { texto = "Sugestão de quem deixou o teste vencer." })).GetProperty("id").GetGuid();
        await SoLeituraAsync(equipe.TenantId, "VENCIDA", api.Relogio.Agora - Prazo);

        await DiarioAsync();

        await using var db = api.DaEquipe(Guid.NewGuid());
        var restantes = await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM sugestoes WHERE id = {id} AND empresa_id IS NULL AND pessoa_id IS NULL")
            .SingleAsync();
        restantes.Should().Be(1, "a sugestão é da Nebrys: fica, sem a empresa nem a pessoa");
    }

    [Fact] // F14-112
    public async Task Diario_EmpresaCanceladaPelaMigrationDoF14_EApagadaComAsContasSoDela()
    {
        var empresa = await EmpresaComTudoAsync();
        // Como uma empresa anterior ao F14: a coluna nova vazia e o mesmo preenchimento da migration (F14-08).
        await using (var db = api.DaEquipe(empresa.Equipe.TenantId))
        {
            await db.Database.ExecuteSqlAsync($"UPDATE tenants SET situacao = '', somente_leitura_desde = NULL WHERE id = {empresa.Equipe.TenantId}");
            await db.Database.ExecuteSqlRawAsync(ScriptsDeDados.CancelarEmpresasAnterioresAoF14);
        }

        var resultado = await DiarioAsync();

        resultado.GetProperty("empresasExpurgadas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        (await LinhasDaEmpresaAsync(empresa.Equipe.TenantId)).Should().BeEmpty();
        api.Arquivos.ComPrefixo($"{empresa.Equipe.TenantId}/").Should().Be(0);
        await using (var db = api.DaEquipe(empresa.Equipe.TenantId))
        {
            (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == empresa.Equipe.TenantId)).Should().BeFalse();
            (await db.Users.AnyAsync(u => u.Id == empresa.Dono || u.Id == empresa.Contato)).Should().BeFalse();
        }
    }

    [Fact] // F14-104
    public async Task Registro_DocumentoDeCadastroAbandonadoExpurgado_201()
    {
        var documento = DocumentosDeTeste.Novo();
        var (token, _) = await RegistrarAsync(documento);
        var tenantId = await TenantDoTokenAsync(token);

        (await DiarioAsync(TimeSpan.FromDays(8))).GetProperty("cadastrosRemovidos").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        await using (var db = api.DaEquipe(tenantId))
        {
            (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenantId)).Should().BeFalse();
            (await db.Set<DocumentoTeste>().AnyAsync(d => d.Hash == DocumentosDeTeste.Hash(documento))).Should().BeFalse();
        }

        (await RegistrarCorpoAsync(documento)).Status.Should().Be(HttpStatusCode.Created);
    }

    [Fact] // F14-104, decisão do Daniel (18/11): "só apagar a linha"; a liberação usada pelo abandonado não volta
    public async Task Registro_LiberacaoUsadaPorCadastroAbandonadoExpurgado_LiberacaoNaoVoltaE400()
    {
        var documento = DocumentosDeTeste.Novo();
        (await RegistrarCorpoAsync(documento)).Status.Should().Be(HttpStatusCode.Created);
        var liberadoEm = api.Relogio.Agora;
        await using (var db = api.DaEquipe(Guid.NewGuid()))
        {
            (await db.Database.ExecuteSqlAsync(
                $"UPDATE documentos_teste SET liberado_em = {liberadoEm}, liberacao_usada_em = NULL WHERE hash = {DocumentosDeTeste.Hash(documento)}"))
                .Should().Be(1);
        }

        // Um segundo cadastro (abandonado) usa a liberação; o primeiro confirma o e-mail e segue (não é abandonado).
        var inicio = api.Relogio.Agora;
        api.Relogio.Agora = inicio + TimeSpan.FromMinutes(5);
        try
        {
            (await RegistrarCorpoAsync(documento)).Status.Should().Be(HttpStatusCode.Created);
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }

        await using (var db = api.DaEquipe(Guid.NewGuid()))
            await db.Database.ExecuteSqlAsync($"UPDATE tenants SET envio_liberado_em = {inicio} WHERE documento = {documento} AND criado_em = {inicio}");

        await DiarioAsync(TimeSpan.FromDays(8));

        await using (var db = api.DaEquipe(Guid.NewGuid()))
        {
            (await db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Documento == documento)).Should().Be(1, "só o abandonado saiu");
            var registro = await db.Set<DocumentoTeste>().AsNoTracking().SingleAsync(d => d.Hash == DocumentosDeTeste.Hash(documento));
            registro.PrimeiroTesteEm.Should().Be(inicio, "a linha é do primeiro cadastro, que existe: fica");
            registro.LiberacaoDisponivel.Should().BeFalse("a liberação usada pelo abandonado não volta");
        }

        api.Relogio.Agora = inicio + TimeSpan.FromDays(9);
        try
        {
            (await RegistrarCorpoAsync(documento)).Status.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }
    }

    [Fact] // F14-104: o expurgo acha a linha do cadastro pela igualdade PrimeiroTesteEm == Tenant.CriadoEm
    public async Task Registro_DocumentoNovo_RegistroEEmpresaComOMesmoInstante()
    {
        // Relógio que anda a cada leitura: se o cadastro ler o instante duas vezes (uma para o registro, outra para a
        // empresa), os dois ficam diferentes e o expurgo do cadastro abandonado deixaria de achar a linha.
        var relogio = new RelogioQueAnda(api.Relogio.Agora, TimeSpan.FromMilliseconds(1));
        await using var app = await api.NovaApiAsync(
            new Dictionary<string, string?>(), servicos: s => s.AddSingleton<TimeProvider>(relogio));
        var documento = DocumentosDeTeste.Novo();
        var email = $"instante-{Guid.NewGuid():N}@exemplo.com";
        using (var http = app.GetTestClient())
        {
            using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), new
            {
                nome = "Ana Souza", email, senha = Equipe.Senha, nomeEmpresa = "Estúdio Traço", documento,
            });
            resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        await using var db = api.DaEquipe(Guid.NewGuid());
        var usuario = await db.Users.SingleAsync(u => u.Email == email);
        var tenant = await db.Tenants.IgnoreQueryFilters().AsNoTracking().SingleAsync(t => t.CriadoPorUsuarioId == usuario.Id);
        var registro = await db.Set<DocumentoTeste>().AsNoTracking().SingleAsync(d => d.Hash == DocumentosDeTeste.Hash(documento));
        registro.PrimeiroTesteEm.Should().Be(tenant.CriadoEm);
    }

    [Fact] // F14-104 (borda): a empresa que cabe nos dois expurgos no mesmo diário sai como cadastro abandonado
    public async Task Diario_EmpresaAbandonadaESoLeituraHaOPrazo_SaiPeloExpurgoDeCadastrosEODocumentoVolta()
    {
        var documento = DocumentosDeTeste.Novo();
        var (token, _) = await RegistrarAsync(documento);
        var tenantId = await TenantDoTokenAsync(token);
        var adiante = TimeSpan.FromDays(8);
        await SoLeituraAsync(tenantId, "CANCELADA", api.Relogio.Agora + adiante - Prazo);

        var resultado = await DiarioAsync(adiante);

        resultado.GetProperty("cadastrosRemovidos").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        await using (var db = api.DaEquipe(tenantId))
        {
            (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenantId)).Should().BeFalse();
            (await db.Set<DocumentoTeste>().AnyAsync(d => d.Hash == DocumentosDeTeste.Hash(documento)))
                .Should().BeFalse("saiu pelo expurgo de cadastros, que apaga a linha do documento");
        }

        (await RegistrarCorpoAsync(documento)).Status.Should().Be(HttpStatusCode.Created);
    }

    [Fact] // F14-32: criadora de outra empresa, sem vínculo nem sessão lá
    public async Task Diario_ContaCriadoraDeOutraEmpresa_NaoEApagada()
    {
        var expurgavel = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        var pessoa = await UsuarioAsync(expurgavel, expurgavel.Dono);
        await using (var db = api.DaEquipe(outra.TenantId))
            await db.Database.ExecuteSqlAsync($"UPDATE tenants SET criado_por_usuario_id = {pessoa} WHERE id = {outra.TenantId}");
        await SoLeituraAsync(expurgavel.TenantId, "VENCIDA", api.Relogio.Agora - Prazo);

        await DiarioAsync();

        await using var verificacao = api.DaEquipe(outra.TenantId);
        (await verificacao.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == expurgavel.TenantId)).Should().BeFalse();
        (await verificacao.Users.AnyAsync(u => u.Id == pessoa)).Should().BeTrue("ela criou outra empresa");
        (await verificacao.Vinculos.IgnoreQueryFilters().AnyAsync(v => v.UsuarioId == pessoa)).Should().BeFalse();
        (await verificacao.Set<RefreshToken>().AnyAsync(r => r.UsuarioId == pessoa)).Should().BeFalse();
    }

    [Fact] // F14-29/F14-32 (borda): conta presente só em duas empresas expurgáveis na mesma rodada
    public async Task Diario_ContaEmDuasEmpresasExpurgaveis_EApagadaAoSairDaSegunda()
    {
        var primeira = await Equipe.CriarAsync(api);
        var segunda = await Equipe.CriarAsync(api);
        var pessoa = await UsuarioAsync(primeira, primeira.Dono);
        await VincularAsync(segunda.TenantId, pessoa);
        await SoLeituraAsync(primeira.TenantId, "CANCELADA", api.Relogio.Agora - Prazo);
        await SoLeituraAsync(segunda.TenantId, "VENCIDA", api.Relogio.Agora - Prazo);

        await DiarioAsync();

        await using var db = api.DaEquipe(Guid.NewGuid());
        (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == primeira.TenantId || t.Id == segunda.TenantId)).Should().BeFalse();
        (await db.Users.AnyAsync(u => u.Id == pessoa)).Should().BeFalse("ao sair da segunda, não havia presença em outra empresa");
        (await db.Set<RefreshToken>().AnyAsync(r => r.UsuarioId == pessoa)).Should().BeFalse();
    }

    /// <summary>
    /// Empresa com cliente, contato com acesso ao portal (conta só nela), solicitação com anexo, tarefa com comentário com
    /// anexo, convite de equipe pendente, logotipo e sugestão: um pouco de cada tabela e arquivos no storage.
    /// </summary>
    private async Task<EmpresaComDados> EmpresaComTudoAsync()
    {
        var equipe = await Equipe.CriarAsync(api);
        var dono = await UsuarioAsync(equipe, equipe.Dono);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);
        (await MultipartAsync($"/api/v1/solicitacoes/{solicitacao}/interacoes", equipe.Dono, ("texto", "Segue o print"), ("visibilidade", "EXTERNA")))
            .Should().Be(HttpStatusCode.Created);
        var tarefa = await Cenarios.TarefaAsync(equipe, solicitacao);
        (await MultipartAsync($"/api/v1/tarefas/{tarefa}/comentarios", equipe.Dono, ("texto", "Print da tarefa")))
            .Should().Be(HttpStatusCode.Created);
        Guid membro;
        await using (var db = api.DaEquipe(equipe.TenantId))
            membro = (await db.Papeis.ToListAsync()).Single(p => p.Nome == Papel.NomeMembro).Id;
        await equipe.EsperarAsync(HttpStatusCode.Accepted, HttpMethod.Post, "/api/v1/convites",
            new { email = $"convidada-{Guid.NewGuid():N}@estudio.com", papelId = membro });
        var logo = $"{equipe.TenantId}/portal/logo-teste.png";
        using (var conteudo = new MemoryStream(Png))
            await api.Arquivos.EnviarAsync(logo, conteudo, Png.Length, "image/png");
        await using (var db = api.DaEquipe(equipe.TenantId))
            await db.Database.ExecuteSqlAsync($"UPDATE tenants SET logo_chave = {logo} WHERE id = {equipe.TenantId}");
        Guid contatoUsuario;
        await using (var db = api.DaEquipe(equipe.TenantId))
            contatoUsuario = (await db.Users.SingleAsync(u => u.Email == contato.Email)).Id;
        api.Arquivos.ComPrefixo($"{equipe.TenantId}/").Should().Be(3, "dois anexos e o logotipo");
        return new EmpresaComDados(equipe, dono, contatoUsuario, solicitacao);
    }

    /// <summary>Linhas da empresa em cada tabela com <c>tenant_id</c> do modelo (só as que têm alguma).</summary>
    private async Task<Dictionary<string, int>> LinhasDaEmpresaAsync(Guid tenantId)
    {
        await using var db = api.DaEquipe(tenantId);
        var tabelas = ApagadorDeEmpresa.TiposComTenant(db)
            .Select(t => db.Model.FindEntityType(t)!.GetTableName()!)
            .Distinct()
            .ToList();
        var linhas = new Dictionary<string, int>();
        foreach (var tabela in tabelas)
        {
#pragma warning disable EF1002 // O nome da tabela vem do modelo do EF, não de entrada.
            var quantidade = await db.Database
                .SqlQueryRaw<int>($"SELECT count(*)::int AS \"Value\" FROM \"{tabela}\" WHERE tenant_id = {{0}}", tenantId)
                .SingleAsync();
#pragma warning restore EF1002
            if (quantidade > 0)
                linhas[tabela] = quantidade;
        }

        return linhas;
    }

    private async Task SoLeituraAsync(Guid tenantId, string situacao, DateTimeOffset desde)
    {
        await using var db = api.DaEquipe(tenantId);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE tenants SET situacao = {situacao}, somente_leitura_desde = {desde}, cancelada_em = {(situacao == "CANCELADA" ? desde : null)} WHERE id = {tenantId}");
    }

    private async Task VincularAsync(Guid tenantId, Guid usuario)
    {
        await using var db = api.DaEquipe(tenantId);
        var papel = (await db.Papeis.ToListAsync()).Single(p => p.Nome == Papel.NomeMembro);
        db.Add(Vinculo.Criar(Guid.NewGuid(), usuario, papel));
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> UsuarioAsync(Equipe equipe, string token) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: token)).GetProperty("usuarioId").GetGuid();

    private async Task<Guid> TenantDoTokenAsync(string token)
    {
        using var http = api.NovoHttp();
        using var eu = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/auth/eu", UriKind.Relative));
        eu.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(eu);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tenant").GetProperty("id").GetGuid();
    }

    private async Task<(string Token, string Email)> RegistrarAsync(string documento)
    {
        var email = $"expurgo-{Guid.NewGuid():N}@exemplo.com";
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), new
        {
            nome = "Ana Souza", email, senha = Equipe.Senha, nomeEmpresa = "Estúdio Traço", documento,
        });
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        return ((await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!, email);
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> RegistrarCorpoAsync(string documento)
    {
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), new
        {
            nome = "Ana Souza", email = $"expurgo-{Guid.NewGuid():N}@exemplo.com", senha = Equipe.Senha, nomeEmpresa = "Estúdio Traço", documento,
        });
        var texto = await resposta.Content.ReadAsStringAsync();
        return (resposta.StatusCode, texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone());
    }

    private async Task<HttpStatusCode> MultipartAsync(string rota, string token, params (string Campo, string Valor)[] campos)
    {
        using var formulario = new MultipartFormDataContent();
        foreach (var (campo, valor) in campos)
            formulario.Add(new StringContent(valor), campo);
        var parte = new ByteArrayContent(Png);
        parte.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        formulario.Add(parte, "anexos", "tela.png");
        using var http = api.NovoHttp();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri(rota, UriKind.Relative)) { Content = formulario };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(pedido);
        return resposta.StatusCode;
    }

    /// <summary>Roda o diário com o relógio adiantado (ou no instante atual) e volta o relógio.</summary>
    private async Task<JsonElement> DiarioAsync(TimeSpan adiante = default)
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

    private sealed record EmpresaComDados(Equipe Equipe, Guid Dono, Guid Contato, Guid Solicitacao);
}

/// <summary>Relógio que avança um passo a cada leitura (para flagrar dois instantes onde deveria haver um).</summary>
internal sealed class RelogioQueAnda(DateTimeOffset inicio, TimeSpan passo) : TimeProvider
{
    private long _ticks = inicio.UtcTicks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Add(ref _ticks, passo.Ticks), TimeSpan.Zero);
}
