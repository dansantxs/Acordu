using System.Net;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §4.1–4.2: clientes e contatos, com permissão, isolamento e If-Match.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class ClientesTests(ApiFixture api)
{
    private const string Base = "https://acordu.app.br/erros/";

    private static readonly string[] EtiquetasCondominio = ["Condomínio"];

    private static object Padaria(string nome = "Padaria Bom Grão") =>
        new { nome, documento = "12.345.678/0001-95", observacoes = "Entrega às terças" };

    [Fact]
    public async Task Criar_DevolveODetalheComVersao()
    {
        var equipe = await Equipe.CriarAsync(api);

        var cliente = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", Padaria());

        cliente.GetProperty("nome").GetString().Should().Be("Padaria Bom Grão");
        cliente.GetProperty("documento").GetString().Should().Be("12345678000195");
        cliente.GetProperty("ativo").GetBoolean().Should().BeTrue();
        cliente.GetProperty("versao").GetString().Should().NotBeNullOrEmpty();
        cliente.GetProperty("contatos").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Criar_DocumentoInvalido_400NoCampo()
    {
        var equipe = await Equipe.CriarAsync(api);

        var erro = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/clientes", new { nome = "X", documento = "123" });

        erro.GetProperty("errors").TryGetProperty("documento", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Listar_BuscaPorNomeOuDocumento_EFiltroDeAtivo()
    {
        var equipe = await Equipe.CriarAsync(api);
        var padaria = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", Padaria());
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", new { nome = "Oficina do Zé" });
        var inativo = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", new { nome = "Padaria Fechada" });
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/clientes/{inativo.GetProperty("id").GetGuid()}");

        var porNome = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/clientes?busca=padaria&ativo=true");
        var porDocumento = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/clientes?busca=345.678");
        var todos = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/clientes?tamanhoPagina=2");

        porNome.GetProperty("total").GetInt32().Should().Be(1);
        porNome.GetProperty("itens")[0].GetProperty("id").GetGuid().Should().Be(padaria.GetProperty("id").GetGuid());
        porDocumento.GetProperty("total").GetInt32().Should().Be(1);
        todos.GetProperty("total").GetInt32().Should().Be(3);
        todos.GetProperty("itens").GetArrayLength().Should().Be(2);
        todos.GetProperty("tamanhoPagina").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Editar_ComIfMatch_EVersaoVelha409()
    {
        var equipe = await Equipe.CriarAsync(api);
        var criado = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", Padaria());
        var id = criado.GetProperty("id").GetGuid();
        var versao = criado.GetProperty("versao").GetString();

        var editado = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/clientes/{id}", new { nome = "Padaria Bom Grão Ltda" }, ifMatch: versao);
        var conflito = await equipe.EsperarAsync(
            HttpStatusCode.Conflict, HttpMethod.Patch, $"/api/v1/clientes/{id}", new { nome = "Outro nome" }, ifMatch: versao);

        editado.GetProperty("nome").GetString().Should().Be("Padaria Bom Grão Ltda");
        editado.GetProperty("documento").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null, "o PATCH recebe o formulário inteiro");
        editado.GetProperty("versao").GetString().Should().NotBe(versao);
        conflito.GetProperty("type").GetString().Should().Be(Base + "conflito-versao");
    }

    [Fact]
    public async Task Contatos_AdicionarEditarInativar_EEmailRepetido422()
    {
        var equipe = await Equipe.CriarAsync(api);
        var id = (await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", Padaria())).GetProperty("id").GetGuid();

        var contato = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{id}/contatos",
            new { nome = "Marina", email = "Marina@Padaria.com", cargo = "Gerente" });
        var contatoId = contato.GetProperty("id").GetGuid();
        var repetido = await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, $"/api/v1/clientes/{id}/contatos",
            new { nome = "Outra Marina", email = "marina@padaria.com" });
        var editado = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/clientes/{id}/contatos/{contatoId}",
            new { nome = "Marina Souza", email = "marina@padaria.com", telefone = "14 99999-0000" });
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/clientes/{id}/contatos/{contatoId}");
        var detalhe = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/clientes/{id}");

        contato.GetProperty("email").GetString().Should().Be("marina@padaria.com");
        contato.GetProperty("temConta").GetBoolean().Should().BeFalse();
        repetido.GetProperty("type").GetString().Should().Be(Base + "validacao");
        editado.GetProperty("nome").GetString().Should().Be("Marina Souza");
        editado.GetProperty("cargo").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        detalhe.GetProperty("contatos")[0].GetProperty("ativo").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ContatoInexistente_404()
    {
        var equipe = await Equipe.CriarAsync(api);
        var id = (await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", Padaria())).GetProperty("id").GetGuid();

        var erro = await equipe.EsperarAsync(
            HttpStatusCode.NotFound, HttpMethod.Patch, $"/api/v1/clientes/{id}/contatos/{Guid.NewGuid()}",
            new { nome = "X", email = "x@x.com" });

        erro.GetProperty("type").GetString().Should().Be(Base + "nao-encontrado");
    }

    [Fact]
    public async Task OutroTenant_404EmLeituraEEscrita()
    {
        var equipe = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        var id = (await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", Padaria())).GetProperty("id").GetGuid();

        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/clientes/{id}");
        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Patch, $"/api/v1/clientes/{id}", Padaria("Invasão"));
        var lista = await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/clientes");

        lista.GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Leitor_LeMasNaoEdita()
    {
        var equipe = await Equipe.CriarAsync(api);
        var leitor = await equipe.ComPapelAsync(Papel.NomeLeitor);
        var id = (await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", Padaria())).GetProperty("id").GetGuid();

        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/clientes/{id}", token: leitor);
        var erro = await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Post, "/api/v1/clientes", Padaria(), token: leitor);

        erro.GetProperty("type").GetString().Should().Be(Base + "sem-permissao");
    }

    private static object Completo(Guid? responsavelId, params string[] etiquetas) => new
    {
        nome = "Padaria Bom Grão",
        documento = "12.345.678/0001-95",
        observacoes = (string?)null,
        razaoSocial = "Bom Grão Panificadora Ltda",
        inscricaoEstadual = "isento",
        email = "Contato@BomGrao.com",
        telefone = "(18) 3333-0000",
        endereco = new
        {
            cep = "19700-000",
            logradouro = "Rua das Flores",
            numero = "120",
            complemento = (string?)null,
            bairro = "Centro",
            cidade = "Bastos",
            uf = "sp",
        },
        responsavelId,
        etiquetas,
    };

    private static async Task<Guid> EuAsync(Equipe equipe)
    {
        using var resposta = await equipe.GetAsync("/api/v1/auth/eu");
        return (await Equipe.JsonAsync(resposta)).GetProperty("usuarioId").GetGuid();
    }

    [Fact]
    public async Task CadastroAmpliado_GuardaNormalizadoEDevolveNoDetalhe()
    {
        var equipe = await Equipe.CriarAsync(api);
        var dono = await EuAsync(equipe);

        var cliente = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", Completo(dono, " Varejo ", "varejo", "Centro"));

        cliente.GetProperty("razaoSocial").GetString().Should().Be("Bom Grão Panificadora Ltda");
        cliente.GetProperty("inscricaoEstadual").GetString().Should().Be("ISENTO");
        cliente.GetProperty("email").GetString().Should().Be("contato@bomgrao.com");
        cliente.GetProperty("telefone").GetString().Should().Be("(18) 3333-0000");
        var endereco = cliente.GetProperty("endereco");
        endereco.GetProperty("cep").GetString().Should().Be("19700000");
        endereco.GetProperty("uf").GetString().Should().Be("SP");
        endereco.GetProperty("complemento").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        cliente.GetProperty("responsavel").GetProperty("id").GetGuid().Should().Be(dono);
        cliente.GetProperty("responsavel").GetProperty("nome").GetString().Should().NotBeNullOrEmpty();
        cliente.GetProperty("etiquetas").EnumerateArray().Select(e => e.GetString()).Should().Equal("Varejo", "Centro");
    }

    [Fact]
    public async Task CadastroAmpliado_PatchSemOsCamposApaga()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", Completo(await EuAsync(equipe), "Varejo"));
        var id = cliente.GetProperty("id").GetGuid();

        var editado = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/clientes/{id}", new { nome = "Padaria Bom Grão" },
            ifMatch: cliente.GetProperty("versao").GetString());

        editado.GetProperty("razaoSocial").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        editado.GetProperty("endereco").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        editado.GetProperty("responsavel").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        editado.GetProperty("etiquetas").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("cep", "1970", "endereco.cep")]
    [InlineData("uf", "XX", "endereco.uf")]
    public async Task EnderecoInvalido_400NoCampoAninhado(string campo, string valor, string erroEsperado)
    {
        var equipe = await Equipe.CriarAsync(api);
        var endereco = new Dictionary<string, object?>
        {
            ["cep"] = "19700-000", ["logradouro"] = "Rua A", ["cidade"] = "Bastos", ["uf"] = "SP",
        };
        endereco[campo] = valor;

        var erro = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/clientes", new { nome = "X", endereco });

        erro.GetProperty("errors").TryGetProperty(erroEsperado, out _).Should().BeTrue();
    }

    [Fact]
    public async Task ResponsavelForaDaEquipe_400NoCampo()
    {
        var equipe = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);

        var erro = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/clientes", Completo(await EuAsync(outra)));

        erro.GetProperty("errors").TryGetProperty("responsavelId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Listar_FiltraPorEtiquetaEResponsavel_EEtiquetasEmUso()
    {
        var equipe = await Equipe.CriarAsync(api);
        var dono = await EuAsync(equipe);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", Completo(dono, "Varejo", "Centro"));
        await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", new { nome = "Condomínio Azul", etiquetas = EtiquetasCondominio });

        var varejo = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/clientes?etiqueta=Varejo");
        var doDono = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/clientes?responsavelId={dono}");
        var etiquetas = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/clientes/etiquetas");

        varejo.GetProperty("total").GetInt32().Should().Be(1);
        varejo.GetProperty("itens")[0].GetProperty("etiquetas").GetArrayLength().Should().Be(2);
        doDono.GetProperty("total").GetInt32().Should().Be(1);
        doDono.GetProperty("itens")[0].GetProperty("responsavel").GetProperty("id").GetGuid().Should().Be(dono);
        etiquetas.EnumerateArray().Select(e => e.GetString()).Should().Equal("Centro", "Condomínio", "Varejo");
    }
}
