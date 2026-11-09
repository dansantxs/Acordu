using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;

namespace Acordu.Api.FunctionalTests;

/// <summary>RF-SOL-06, RF-POR-04/05, RNF-SEG-07 e segurança §7: anexos nas interações, tipo pelo conteúdo e download assinado.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class AnexosTests(ApiFixture api)
{
    private const string Erros = "https://acordu.app.br/erros/";

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nconteúdo do relatório");
    private static readonly byte[] Exe = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00];

    private Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        string rota, string token, IEnumerable<(string Nome, byte[] Conteudo)> arquivos, params (string Campo, string Valor)[] campos) =>
        EnviarAsync(HttpMethod.Post, "anexos", rota, token, arquivos, campos);

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        HttpMethod metodo, string campoArquivos, string rota, string token, IEnumerable<(string Nome, byte[] Conteudo)> arquivos,
        params (string Campo, string Valor)[] campos)
    {
        using var formulario = new MultipartFormDataContent();
        foreach (var (campo, valor) in campos)
            formulario.Add(new StringContent(valor), campo);
        foreach (var (nome, conteudo) in arquivos)
        {
            var parte = new ByteArrayContent(conteudo);
            // O tipo declarado é ignorado: vale o detectado pelo conteúdo.
            parte.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            formulario.Add(parte, campoArquivos, nome);
        }

        using var http = api.NovoHttp();
        using var pedido = new HttpRequestMessage(metodo, new Uri(rota, UriKind.Relative)) { Content = formulario };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(pedido);
        var texto = await resposta.Content.ReadAsStringAsync();
        return (resposta.StatusCode, texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone());
    }

    private async Task<(HttpStatusCode Status, Uri? Destino)> BaixarAsync(string rota, string token)
    {
        using var http = api.NovoHttp();
        using var pedido = new HttpRequestMessage(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(pedido);
        if (resposta.StatusCode != HttpStatusCode.OK) return (resposta.StatusCode, null);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return (resposta.StatusCode, new Uri(corpo.GetProperty("url").GetString()!));
    }

    private async Task<(Equipe Equipe, Guid Cliente, Guid Solicitacao)> CenarioAsync()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        return (equipe, cliente, await Cenarios.SolicitacaoAsync(equipe, cliente));
    }

    [Fact]
    public async Task Comentario_SolicitacaoEncerrada_SoNotaInternaSemAnexo() // F11-06, F11-07
    {
        var (equipe, _, solicitacao) = await CenarioAsync();
        var rota = $"/api/v1/solicitacoes/{solicitacao}";
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{rota}/cancelamento", new { motivo = "Cliente desistiu" });

        var (comAnexo, problema) = await EnviarAsync(
            $"{rota}/interacoes", equipe.Dono, [("tela.png", Png)], ("texto", "Print"), ("visibilidade", "INTERNA"));
        comAnexo.Should().Be(HttpStatusCode.Conflict);
        problema.GetProperty("type").GetString().Should().Be(Erros + "solicitacao-encerrada");
        api.Arquivos.ComPrefixo($"{equipe.TenantId}/").Should().Be(0, "o arquivo nem chega ao armazenamento");
        await equipe.EsperarAsync(HttpStatusCode.Conflict, HttpMethod.Post, $"{rota}/interacoes",
            new { texto = "Mais alguma coisa?", visibilidade = "EXTERNA" });

        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"{rota}/interacoes",
            new { texto = "O cliente ligou agradecendo." });
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, rota)).GetProperty("status").GetString().Should().Be("CANCELADA");
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{rota}/interacoes")).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Comentario_ComAnexos_TipoPeloConteudo_EDownloadAssinado()
    {
        var (equipe, _, solicitacao) = await CenarioAsync();

        var (status, interacao) = await EnviarAsync(
            $"/api/v1/solicitacoes/{solicitacao}/interacoes", equipe.Dono,
            [("tela.png", Png), ("relatório final.pdf", Pdf)], ("texto", "Segue o print"), ("visibilidade", "EXTERNA"));

        status.Should().Be(HttpStatusCode.Created, "corpo: {0}", interacao);
        interacao.GetProperty("texto").GetString().Should().Be("Segue o print");
        interacao.GetProperty("visibilidade").GetString().Should().Be("EXTERNA");
        var anexos = interacao.GetProperty("anexos");
        anexos.GetArrayLength().Should().Be(2);
        // Ordem pelo nome do arquivo.
        anexos[0].GetProperty("nomeArquivo").GetString().Should().Be("relatório final.pdf");
        anexos[0].GetProperty("tipoConteudo").GetString().Should().Be("application/pdf");
        anexos[0].GetProperty("tamanhoBytes").GetInt64().Should().Be(Pdf.Length);
        anexos[1].GetProperty("tipoConteudo").GetString().Should().Be("image/png");

        var anexo = anexos[0].GetProperty("id").GetGuid();
        var (baixar, destino) = await BaixarAsync($"/api/v1/anexos/{anexo}/download", equipe.Dono);
        baixar.Should().Be(HttpStatusCode.OK);
        var chave = $"{equipe.TenantId}/{solicitacao}/{anexo}";
        destino!.AbsoluteUri.Should().StartWith(ArmazenamentoDeTeste.Base + chave).And.Contain("validade=5");
        api.Arquivos.Conteudo(chave).Should().Equal(Pdf);
        var linha = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/interacoes");
        linha[0].GetProperty("anexos").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task TipoNaoPermitido_422_NadaFicaNoStorage()
    {
        var (equipe, _, solicitacao) = await CenarioAsync();

        var (status, erro) = await EnviarAsync(
            $"/api/v1/solicitacoes/{solicitacao}/interacoes", equipe.Dono, [("ok.png", Png), ("fatura.pdf", Exe)], ("texto", "x"));

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        erro.GetProperty("type").GetString().Should().Be(Erros + "anexo-invalido");
        api.Arquivos.ComPrefixo($"{equipe.TenantId}/").Should().Be(0);
    }

    [Fact]
    public async Task AcimaDe20MB_413()
    {
        var (equipe, _, solicitacao) = await CenarioAsync();
        var grande = new byte[(20 * 1024 * 1024) + 1];
        Pdf.CopyTo(grande, 0);

        var (status, erro) = await EnviarAsync($"/api/v1/solicitacoes/{solicitacao}/interacoes", equipe.Dono, [("grande.pdf", grande)]);

        status.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        erro.GetProperty("type").GetString().Should().Be(Erros + "anexo-grande-demais");
    }

    [Fact]
    public async Task Portal_SoAnexosExternos_EContatoRespondeComAnexo()
    {
        var (equipe, cliente, solicitacao) = await CenarioAsync();
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        var (_, externa) = await EnviarAsync(
            $"/api/v1/solicitacoes/{solicitacao}/interacoes", equipe.Dono, [("arte.png", Png)], ("visibilidade", "EXTERNA"));
        var (_, interna) = await EnviarAsync(
            $"/api/v1/solicitacoes/{solicitacao}/interacoes", equipe.Dono, [("custos.pdf", Pdf)], ("visibilidade", "INTERNA"));
        var anexoExterno = externa.GetProperty("anexos")[0].GetProperty("id").GetGuid();
        var anexoInterno = interna.GetProperty("anexos")[0].GetProperty("id").GetGuid();

        var linha = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/portal/solicitacoes/{solicitacao}/interacoes", token: contato.Token);
        var externoBaixado = await BaixarAsync($"/api/v1/portal/anexos/{anexoExterno}/download", contato.Token);
        var internoBaixado = await BaixarAsync($"/api/v1/portal/anexos/{anexoInterno}/download", contato.Token);
        var (respondeu, resposta) = await EnviarAsync(
            $"/api/v1/portal/solicitacoes/{solicitacao}/interacoes", contato.Token, [("comprovante.pdf", Pdf)], ("texto", "Segue"));

        linha.GetArrayLength().Should().Be(1);
        linha[0].GetProperty("anexos")[0].GetProperty("nomeArquivo").GetString().Should().Be("arte.png");
        externoBaixado.Status.Should().Be(HttpStatusCode.OK);
        internoBaixado.Status.Should().Be(HttpStatusCode.NotFound);
        respondeu.Should().Be(HttpStatusCode.Created, "corpo: {0}", resposta);
        resposta.GetProperty("anexos")[0].GetProperty("tipoConteudo").GetString().Should().Be("application/pdf");
    }

    [Fact]
    public async Task Download_LeitorBaixa_OutroTenantEOutroClienteNao()
    {
        var (equipe, _, solicitacao) = await CenarioAsync();
        var (_, interacao) = await EnviarAsync(
            $"/api/v1/solicitacoes/{solicitacao}/interacoes", equipe.Dono, [("arte.png", Png)], ("visibilidade", "EXTERNA"));
        var anexo = interacao.GetProperty("anexos")[0].GetProperty("id").GetGuid();
        var leitor = await equipe.ComPapelAsync(Papel.NomeLeitor);
        var outra = await Equipe.CriarAsync(api);
        var outroCliente = await Cenarios.ClienteAsync(equipe, "Contábil Silva");
        var contatoDeOutro = await ContatosDoPortal.CriarAsync(api, equipe, outroCliente);

        (await BaixarAsync($"/api/v1/anexos/{anexo}/download", leitor)).Status.Should().Be(HttpStatusCode.OK);
        (await BaixarAsync($"/api/v1/anexos/{anexo}/download", outra.Dono)).Status.Should().Be(HttpStatusCode.NotFound);
        (await BaixarAsync($"/api/v1/portal/anexos/{anexo}/download", contatoDeOutro.Token)).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Portal_AbreSolicitacaoComAnexos_ViramAPrimeiraInteracao()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);

        var (status, aberta) = await EnviarAsync(
            "/api/v1/portal/solicitacoes", contato.Token, [("erro.png", Png), ("nota.pdf", Pdf)],
            ("titulo", "Site fora do ar"), ("descricao", "Desde ontem"), ("prioridade", "ALTA"));

        status.Should().Be(HttpStatusCode.Created, "corpo: {0}", aberta);
        var id = aberta.GetProperty("id").GetGuid();
        aberta.GetProperty("titulo").GetString().Should().Be("Site fora do ar");
        var linha = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{id}/interacoes");
        linha.GetArrayLength().Should().Be(1);
        linha[0].GetProperty("autorEhCliente").GetBoolean().Should().BeTrue();
        linha[0].GetProperty("visibilidade").GetString().Should().Be("EXTERNA");
        linha[0].GetProperty("anexos").GetArrayLength().Should().Be(2);
        var anexo = linha[0].GetProperty("anexos")[0].GetProperty("id").GetGuid();
        api.Arquivos.Existe($"{equipe.TenantId}/{id}/{anexo}").Should().BeTrue();
    }

    [Fact]
    public async Task Portal_AberturaComAnexoInvalido_NaoCriaASolicitacao()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);

        var (status, _) = await EnviarAsync("/api/v1/portal/solicitacoes", contato.Token, [("x.pdf", Exe)], ("titulo", "Com vírus"));

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        var lista = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes?clienteId={cliente}");
        lista.GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Logotipo_EnviaTrocaRemove_EApareceNoPortal()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        const string Rota = "/api/v1/configuracoes/portal/logo";

        var (status, primeiro) = await EnviarAsync(HttpMethod.Put, "logo", Rota, equipe.Dono, [("marca.png", Png)]);
        status.Should().Be(HttpStatusCode.OK, "corpo: {0}", primeiro);
        var url = new Uri(primeiro.GetProperty("logoUrl").GetString()!);
        url.AbsoluteUri.Should().StartWith($"{ArmazenamentoDeTeste.Base}{equipe.TenantId}/portal/logo-").And.Contain(".png").And.Contain("validade=60");
        var chave = url.AbsolutePath.TrimStart('/');
        api.Arquivos.Existe(chave).Should().BeTrue();

        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/portal/eu", token: contato.Token);
        eu.GetProperty("prestador").GetProperty("logoUrl").GetString().Should().Be(url.AbsoluteUri);

        var (_, segundo) = await EnviarAsync(HttpMethod.Put, "logo", Rota, equipe.Dono, [("marca.jpg", [0xFF, 0xD8, 0xFF, 0xE0, 1, 2])]);
        segundo.GetProperty("logoUrl").GetString().Should().Contain(".jpg");
        api.Arquivos.Existe(chave).Should().BeFalse("o anterior é removido depois da troca");

        var removido = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Delete, Rota);
        removido.GetProperty("logoUrl").ValueKind.Should().Be(JsonValueKind.Null);
        api.Arquivos.ComPrefixo($"{equipe.TenantId}/portal/").Should().Be(0);
    }

    [Fact]
    public async Task Logotipo_TipoTamanhoEPermissao()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        const string Rota = "/api/v1/configuracoes/portal/logo";
        var grande = new byte[(512 * 1024) + 1];
        Png.CopyTo(grande, 0);

        var (pdf, erroPdf) = await EnviarAsync(HttpMethod.Put, "logo", Rota, equipe.Dono, [("marca.pdf", Pdf)]);
        var (tamanho, _) = await EnviarAsync(HttpMethod.Put, "logo", Rota, equipe.Dono, [("marca.png", grande)]);
        var (semPermissao, _) = await EnviarAsync(HttpMethod.Put, "logo", Rota, membro, [("marca.png", Png)]);

        pdf.Should().Be(HttpStatusCode.BadRequest);
        erroPdf.GetProperty("errors").TryGetProperty("logo", out _).Should().BeTrue();
        tamanho.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        semPermissao.Should().Be(HttpStatusCode.Forbidden);
    }
}
