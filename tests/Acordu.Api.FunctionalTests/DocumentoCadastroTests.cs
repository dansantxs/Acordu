using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, documento no cadastro (F14g; RF-AUT-09; design D8): CPF ou CNPJ obrigatório e válido, um teste por
/// documento guardado só como HMAC, fora dos tenants, e a liberação para novo teste usada uma vez.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class DocumentoCadastroTests(ApiFixture api)
{
    private const string Senha = "senha-bem-comprida";

    [Theory] // F14-56
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Registro_SemDocumento_400NoCampoENadaCriado(string? documento)
    {
        var email = NovoEmail();

        var (status, corpo) = await RegistrarAsync(email, documento);

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo).Should().Be("Informe o CPF ou CNPJ.");
        await NadaCriadoAsync(email);
    }

    [Fact] // F14-56
    public async Task Registro_SemOCampoDocumentoNoCorpo_400NoCampo()
    {
        var email = NovoEmail();
        using var http = api.NovoHttp();

        using var resposta = await http.PostAsJsonAsync(
            new Uri("/api/v1/auth/registro", UriKind.Relative), new { nome = "Ana", email, senha = Senha, nomeEmpresa = "Estúdio" });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Erro(await resposta.Content.ReadFromJsonAsync<JsonElement>()).Should().Be("Informe o CPF ou CNPJ.");
        await NadaCriadoAsync(email);
    }

    [Theory] // F14-56
    [InlineData("cpf")]
    [InlineData("cnpj")]
    [InlineData("alfanumerico")]
    [InlineData("curto")]
    [InlineData("letra-no-cpf")]
    public async Task Registro_DocumentoInvalido_400NoCampoENadaCriado(string tipo)
    {
        var documento = tipo switch
        {
            "cpf" => DocumentosDeTeste.ComDigitoErrado(DocumentosDeTeste.NovoCpf()),
            "cnpj" => DocumentosDeTeste.ComDigitoErrado(DocumentosDeTeste.Novo()),
            "alfanumerico" => DocumentosDeTeste.ComDigitoErrado(DocumentosDeTeste.NovoAlfanumerico()),
            "curto" => "1234567890",
            _ => "1234567890A",
        };
        var email = NovoEmail();

        var (status, corpo) = await RegistrarAsync(email, documento);

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo).Should().Be("CPF ou CNPJ inválido.");
        await NadaCriadoAsync(email);
    }

    [Fact] // F14-56 (o caminho feliz guarda o documento normalizado no Tenant e só o HMAC no registro global)
    public async Task Registro_DocumentoValido_GuardaNormalizadoNaEmpresaESoOHashNoRegistro()
    {
        var cpf = DocumentosDeTeste.NovoCpf();
        var email = NovoEmail();

        var (status, _) = await RegistrarAsync(email, DocumentosDeTeste.ComPontuacaoCpf(cpf));

        status.Should().Be(HttpStatusCode.Created);
        await using var db = Banco();
        var usuario = await db.Users.SingleAsync(u => u.Email == email);
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.CriadoPorUsuarioId == usuario.Id);
        tenant.Documento.Should().Be(cpf);
        var registro = await db.Set<DocumentoTeste>().SingleAsync(d => d.Hash == DocumentosDeTeste.Hash(cpf));
        registro.PrimeiroTesteEm.Should().Be(tenant.CriadoEm);
        registro.LiberadoEm.Should().BeNull();
        registro.LiberacaoUsadaEm.Should().BeNull();
        var semDocumentoEmClaro = await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM documentos_teste WHERE hash = {cpf} OR hash ILIKE {"%" + cpf + "%"}")
            .SingleAsync();
        semDocumentoEmClaro.Should().Be(0, "o registro global nunca guarda o documento em claro (D8)");
    }

    [Fact] // F14-57
    public async Task Registro_DocumentoDeOutraEmpresaExistente_400ComOCanalDaNebrysENadaCriado()
    {
        var cnpj = DocumentosDeTeste.Novo();
        (await RegistrarAsync(NovoEmail(), cnpj)).Status.Should().Be(HttpStatusCode.Created);
        var email = NovoEmail();

        var (status, corpo) = await RegistrarAsync(email, cnpj);

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo).Should().Be(
            "Este CPF ou CNPJ já teve um período de teste no Acordu. Para testar de novo, fale com a Nebrys: contato@acordu.app.br.");
        await NadaCriadoAsync(email);
        await using var db = Banco();
        (await db.Set<DocumentoTeste>().CountAsync(d => d.Hash == DocumentosDeTeste.Hash(cnpj))).Should().Be(1);
    }

    [Fact] // F14-57
    public async Task Registro_DocumentoDeEmpresaJaExpurgada_RecusadoPeloRegistroQueFicou()
    {
        var cnpj = DocumentosDeTeste.Novo();
        (await RegistrarAsync(NovoEmail(), cnpj)).Status.Should().Be(HttpStatusCode.Created);
        // A empresa saiu (expurgo do F14i); o registro do documento não tem referência a ela e fica.
        await using (var db = Banco())
        {
            await db.Database.ExecuteSqlAsync($"UPDATE tenants SET documento = NULL WHERE documento = {cnpj}");
            (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Documento == cnpj)).Should().BeFalse();
        }

        var email = NovoEmail();
        var (status, corpo) = await RegistrarAsync(email, cnpj);

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo).Should().Contain("fale com a Nebrys: contato@acordu.app.br");
        await NadaCriadoAsync(email);
    }

    [Fact] // F14-57
    public async Task Registro_DoisCadastrosAoMesmoTempoComOMesmoDocumento_UmPassaEOOutroRecebe400()
    {
        var cnpj = DocumentosDeTeste.Novo();

        var resultados = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RegistrarAsync(NovoEmail(), cnpj)));

        resultados.Count(r => r.Status == HttpStatusCode.Created).Should().Be(1);
        resultados.Where(r => r.Status != HttpStatusCode.Created).Should().AllSatisfy(r =>
        {
            r.Status.Should().Be(HttpStatusCode.BadRequest);
            Erro(r.Corpo).Should().Contain("já teve um período de teste");
        });
        await using var db = Banco();
        (await db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Documento == cnpj)).Should().Be(1);
    }

    [Theory] // F14-58
    [InlineData("cpf-pontuado")]
    [InlineData("cnpj-pontuado")]
    [InlineData("alfanumerico-minusculas")]
    [InlineData("alfanumerico-pontuado-minusculas")]
    [InlineData("espacos")]
    public async Task Registro_MesmoDocumentoEscritoDeOutroJeito_RecusadoComoOMesmo(string forma)
    {
        var (primeiro, segundo) = forma switch
        {
            "cpf-pontuado" => DoCpf(),
            "cnpj-pontuado" => DoCnpj(DocumentosDeTeste.Novo()),
            "alfanumerico-minusculas" => Alfanumerico(c => c.ToLowerInvariant()),
            "alfanumerico-pontuado-minusculas" => Alfanumerico(c => DocumentosDeTeste.ComPontuacaoCnpj(c).ToLowerInvariant()),
            _ => Espacos(),
        };
        (await RegistrarAsync(NovoEmail(), primeiro)).Status.Should().Be(HttpStatusCode.Created);
        var email = NovoEmail();

        var (status, corpo) = await RegistrarAsync(email, segundo);

        status.Should().Be(HttpStatusCode.BadRequest);
        Erro(corpo).Should().Contain("já teve um período de teste");
        await NadaCriadoAsync(email);

        static (string, string) DoCpf()
        {
            var cpf = DocumentosDeTeste.NovoCpf();
            return (cpf, DocumentosDeTeste.ComPontuacaoCpf(cpf));
        }

        static (string, string) DoCnpj(string cnpj) => (DocumentosDeTeste.ComPontuacaoCnpj(cnpj), cnpj);

        static (string, string) Alfanumerico(Func<string, string> escrever)
        {
            var cnpj = DocumentosDeTeste.NovoAlfanumerico();
            return (cnpj, escrever(cnpj));
        }

        static (string, string) Espacos()
        {
            var cnpj = DocumentosDeTeste.Novo();
            return (cnpj, $"  {cnpj[..2]} {cnpj[2..8]} {cnpj[8..]} ");
        }
    }

    [Fact] // F14-59
    public async Task Registro_DocumentoComLiberacaoNaoUsada_201ELiberacaoFicaUsada()
    {
        var cnpj = DocumentosDeTeste.NovoAlfanumerico();
        (await RegistrarAsync(NovoEmail(), cnpj)).Status.Should().Be(HttpStatusCode.Created);
        var liberadoEm = await LiberarAsync(cnpj);
        var email = NovoEmail();

        var (status, _) = await RegistrarAsync(email, DocumentosDeTeste.ComPontuacaoCnpj(cnpj).ToLowerInvariant());

        status.Should().Be(HttpStatusCode.Created);
        await using var db = Banco();
        var registro = await db.Set<DocumentoTeste>().AsNoTracking().SingleAsync(d => d.Hash == DocumentosDeTeste.Hash(cnpj));
        registro.LiberadoEm.Should().Be(liberadoEm);
        registro.LiberacaoUsadaEm.Should().NotBeNull();
        registro.LiberacaoDisponivel.Should().BeFalse();
        var usuario = await db.Users.SingleAsync(u => u.Email == email);
        (await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.CriadoPorUsuarioId == usuario.Id)).Documento.Should().Be(cnpj);
    }

    [Fact] // F14-59
    public async Task Registro_LiberacaoJaUsada_TerceiroCadastroRecusado()
    {
        var cpf = DocumentosDeTeste.NovoCpf();
        (await RegistrarAsync(NovoEmail(), cpf)).Status.Should().Be(HttpStatusCode.Created);
        await LiberarAsync(cpf);
        (await RegistrarAsync(NovoEmail(), cpf)).Status.Should().Be(HttpStatusCode.Created);
        var email = NovoEmail();

        var (status, corpo) = await RegistrarAsync(email, cpf);

        status.Should().Be(HttpStatusCode.BadRequest, "a liberação vale para um cadastro só");
        Erro(corpo).Should().Contain("fale com a Nebrys");
        await NadaCriadoAsync(email);
    }

    [Fact] // F14-59
    public async Task Registro_DoisCadastrosAoMesmoTempoComAMesmaLiberacao_SoUmPassa()
    {
        var cnpj = DocumentosDeTeste.Novo();
        (await RegistrarAsync(NovoEmail(), cnpj)).Status.Should().Be(HttpStatusCode.Created);
        await LiberarAsync(cnpj);

        var resultados = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RegistrarAsync(NovoEmail(), cnpj)));

        resultados.Count(r => r.Status == HttpStatusCode.Created).Should().Be(1);
        resultados.Where(r => r.Status != HttpStatusCode.Created)
            .Should().AllSatisfy(r => r.Status.Should().Be(HttpStatusCode.BadRequest));
        await using var db = Banco();
        (await db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Documento == cnpj)).Should().Be(2);
    }

    [Fact] // F14-59
    public async Task Registro_LiberacaoComRecusaPorOutroCampo_LiberacaoContinuaDisponivel()
    {
        var cnpj = DocumentosDeTeste.Novo();
        var emailEmUso = NovoEmail();
        (await RegistrarAsync(emailEmUso, cnpj)).Status.Should().Be(HttpStatusCode.Created);
        await LiberarAsync(cnpj);

        (await RegistrarAsync(emailEmUso, cnpj)).Status.Should().Be(HttpStatusCode.BadRequest);

        await using var db = Banco();
        var registro = await db.Set<DocumentoTeste>().AsNoTracking().SingleAsync(d => d.Hash == DocumentosDeTeste.Hash(cnpj));
        registro.LiberacaoDisponivel.Should().BeTrue("o cadastro recusado não usa a liberação");
    }

    [Theory] // F14-56, F14-57 (nada criado: nem empresa, nem registro do documento, nem usuário)
    [InlineData("invalido")]
    [InlineData("repetido")]
    public async Task Registro_Recusado_NaoMudaNenhumaTabela(string motivo)
    {
        var existente = DocumentosDeTeste.Novo();
        (await RegistrarAsync(NovoEmail(), existente)).Status.Should().Be(HttpStatusCode.Created);
        var documento = motivo == "repetido" ? existente : DocumentosDeTeste.ComDigitoErrado(DocumentosDeTeste.Novo());
        var antes = await ContagensAsync();

        var (status, _) = await RegistrarAsync(NovoEmail(), documento);

        status.Should().Be(HttpStatusCode.BadRequest);
        (await ContagensAsync()).Should().Be(antes);
    }

    [Theory] // D8: a API não sobe sem a chave do HMAC ou com chave curta
    [InlineData(null)]
    [InlineData("")]
    [InlineData("curta-demais")]
    public async Task Api_SemChaveHmacOuComChaveCurta_NaoSobe(string? chave)
    {
        var acao = () => api.NovaApiAsync(new Dictionary<string, string?> { ["Documentos:ChaveHmac"] = chave });

        (await acao.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*Documentos__ChaveHmac*");
    }

    private async Task<(int Usuarios, int Empresas, int Documentos, int Sessoes)> ContagensAsync()
    {
        await using var db = Banco();
        return (
            await db.Users.CountAsync(),
            await db.Tenants.IgnoreQueryFilters().CountAsync(),
            await db.Set<DocumentoTeste>().CountAsync(),
            await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM refresh_tokens").SingleAsync());
    }

    /// <summary>A liberação é gravada direto no banco: a ação da área da Nebrys chega no F14l.</summary>
    private async Task<DateTimeOffset> LiberarAsync(string documentoNormalizado)
    {
        var agora = new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
        await using var db = Banco();
        var linhas = await db.Database.ExecuteSqlAsync(
            $"UPDATE documentos_teste SET liberado_em = {agora}, liberacao_usada_em = NULL WHERE hash = {DocumentosDeTeste.Hash(documentoNormalizado)}");
        linhas.Should().Be(1);
        return agora;
    }

    private async Task NadaCriadoAsync(string email)
    {
        await using var db = Banco();
        (await db.Users.AnyAsync(u => u.Email == email)).Should().BeFalse("o cadastro recusado não cria nada");
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> RegistrarAsync(string email, string? documento)
    {
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(
            new Uri("/api/v1/auth/registro", UriKind.Relative),
            new { nome = "Ana Souza", email, senha = Senha, nomeEmpresa = "Estúdio Traço", documento, versaoTermos = Apoio.TextosAceitos.Termos, versaoPrivacidade = Apoio.TextosAceitos.Privacidade });
        return (resposta.StatusCode, await resposta.Content.ReadFromJsonAsync<JsonElement>());
    }

    /// <summary>Contexto de um tenant qualquer: as tabelas lidas aqui são globais (Identity, registro do documento) ou lidas sem o filtro.</summary>
    private Infrastructure.Persistence.AcorduDbContext Banco() => api.DaEquipe(Guid.NewGuid());

    private static string Erro(JsonElement corpo) =>
        corpo.GetProperty("errors").GetProperty("documento")[0].GetString()!;

    private static string NovoEmail() => $"documento-{Guid.NewGuid():N}@exemplo.com";
}
