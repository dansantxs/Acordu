using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Operacao;
using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F15, F15g (F15-14, F15-15, F15-98; RNF-SEG-07, RNF-SEG-03; design D14 e D15; contrato §7, §12 e §12a): os
/// anexos do e-mail que abre a solicitação, entregue pela rota de entrada como o Worker entrega, passam pela política do
/// portal um por um; os válidos viram a primeira mensagem do contato no canal E-mail, e os recusados, a nota do sistema
/// (só da equipe) e a lista no aviso de recebida.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class AnexosPorEmailTests(ApiFixture api)
{
    private const string Fronteira = "fronteira-do-teste";
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    private static readonly byte[] Pdf = "%PDF-1.7\n1 0 obj\n"u8.ToArray();
    private static readonly byte[] Executavel = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    /// <summary>Empresa com o recebimento ligado, um cliente e um contato (sem conta no portal) com e-mail próprio.</summary>
    private async Task<Cenario> CenarioAsync()
    {
        var equipe = await Equipe.CriarAsync(api);
        await EntradaPorEmail.LigarAsync(equipe);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var email = $"carlos-{Guid.NewGuid():N}@bomgrao.com.br";
        await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos", new { nome = "Carlos Prado", email });
        return new Cenario(equipe, cliente, email, await EntradaPorEmail.EnderecoAsync(api, equipe.TenantId));
    }

    /// <summary>
    /// Um e-mail <c>multipart/mixed</c> com o corpo em texto e as <paramref name="partes"/> na ordem, como um programa de
    /// e-mail monta: cada parte em base64 ou, a grande, em binário (para caber no limite do e-mail inteiro).
    /// </summary>
    private static byte[] ComAnexos(string de, params Parte[] partes)
    {
        using var mime = new MemoryStream();
        Escrever(mime,
            $"From: Carlos Prado <{de}>\r\n"
            + "To: atendimento@estudiotraco.com.br\r\n"
            + "Subject: Forno parou de novo\r\n"
            + $"Message-ID: {EntradaPorEmail.NovoMessageId()}\r\n"
            + "Date: Tue, 06 Oct 2026 10:00:00 -0300\r\n"
            + "MIME-Version: 1.0\r\n"
            + $"Content-Type: multipart/mixed; boundary=\"{Fronteira}\"\r\n"
            + "\r\n"
            + $"--{Fronteira}\r\n"
            + "Content-Type: text/plain; charset=utf-8\r\n"
            + "\r\n"
            + "Seguem as fotos do forno.\r\n");
        foreach (var parte in partes)
        {
            Escrever(mime, $"--{Fronteira}\r\nContent-Type: {parte.Tipo}\r\n");
            if (parte.Disposicao is { } disposicao)
                Escrever(mime, $"Content-Disposition: {disposicao}\r\n");
            if (parte.ContentId is { } contentId)
                Escrever(mime, $"Content-ID: <{contentId}>\r\n");
            if (parte.Binario)
            {
                Escrever(mime, "Content-Transfer-Encoding: binary\r\n\r\n");
                mime.Write(parte.Conteudo);
                Escrever(mime, "\r\n");
            }
            else
            {
                Escrever(mime, "Content-Transfer-Encoding: base64\r\n\r\n"
                    + Convert.ToBase64String(parte.Conteudo, Base64FormattingOptions.InsertLineBreaks) + "\r\n");
            }
        }

        Escrever(mime, $"--{Fronteira}--\r\n");
        return mime.ToArray();
    }

    private static void Escrever(MemoryStream mime, string texto) => mime.Write(Encoding.UTF8.GetBytes(texto));

    private static Parte Anexo(string nome, byte[] conteudo, string tipo = "application/octet-stream") =>
        new($"{tipo}; name=\"{nome}\"", $"attachment; filename=\"{nome}\"", null, conteudo);

    private async Task EntregarAsync(Cenario c, byte[] mime, string? remetente = null) =>
        await EntradaPorEmail.EntregarAsync(api, c.Endereco, mime, remetente ?? c.Email);

    private static async Task<Guid> UnicaAsync(Equipe equipe)
    {
        var lista = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes");
        lista.GetProperty("total").GetInt32().Should().Be(1, "a solicitação é criada mesmo com anexos recusados");
        return Cenarios.Id(lista.GetProperty("itens")[0]);
    }

    private static async Task<List<JsonElement>> InteracoesAsync(Equipe equipe, Guid solicitacao) =>
        [.. (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/interacoes"))
            .EnumerateArray()];

    [Fact] // F15-14 (pergunta 4: a imagem embutida conta como anexo)
    public async Task Entregar_AnexosDentroDaPolitica_PrimeiraMensagemExternaDoContatoSemTextoNoCanalEmail()
    {
        var c = await CenarioAsync();
        var mime = ComAnexos(c.Email,
            new Parte("image/png", "inline", "foto1@bomgrao", Png),
            Anexo("orcamento.pdf", Pdf, "application/pdf"));

        await EntregarAsync(c, mime);

        var solicitacao = await UnicaAsync(c.Equipe);
        var mensagem = (await InteracoesAsync(c.Equipe, solicitacao)).Should().ContainSingle("nada recusado, nenhuma nota").Subject;
        mensagem.GetProperty("autorEhCliente").GetBoolean().Should().BeTrue();
        mensagem.GetProperty("autorSistema").GetBoolean().Should().BeFalse();
        mensagem.GetProperty("autor").GetProperty("nome").GetString().Should().Be("Carlos Prado");
        mensagem.GetProperty("visibilidade").GetString().Should().Be("EXTERNA");
        mensagem.GetProperty("canal").GetString().Should().Be("EMAIL");
        mensagem.GetProperty("texto").ValueKind.Should().Be(JsonValueKind.Null, "os anexos vêm sem texto, como no portal");
        mensagem.GetProperty("anexos").EnumerateArray()
            .Select(a => (a.GetProperty("nomeArquivo").GetString(), a.GetProperty("tipoConteudo").GetString()))
            .Should().Equal(("anexo-1.png", "image/png"), ("orcamento.pdf", "application/pdf"));
        await using var db = api.DaEquipe(c.Equipe.TenantId);
        var anexos = (await db.Interacoes.AsNoTracking().SingleAsync()).Anexos;
        anexos.Should().OnlyContain(a => a.ChaveArmazenamento.StartsWith($"{c.Equipe.TenantId}/{solicitacao}/", StringComparison.Ordinal));
        api.Arquivos.Conteudo(anexos.Single(a => a.NomeArquivo == "orcamento.pdf").ChaveArmazenamento).Should().Equal(Pdf);
        (await db.EmailsRecebidos.AsNoTracking().SingleAsync()).Recusados.Should().BeNull();
        api.Emails.Para(c.Email).Should().ContainSingle(m => m.Categoria == "aviso-recebida")
            .Which.Texto.Should().NotContain("não entraram");
    }

    [Fact] // F15-15
    public async Task Entregar_ParteForaDaPolitica_ValidosEntramNotaDoSistemaEAvisoComOsRecusados()
    {
        var c = await CenarioAsync();
        var grande = new byte[(20 * 1024 * 1024) + 1];
        Png.CopyTo(grande, 0);
        var mime = ComAnexos(c.Email,
        [
            Anexo("programa.exe", Executavel),
            new Parte("image/png; name=\"enorme.png\"", "attachment; filename=\"enorme.png\"", null, grande, Binario: true),
            .. Enumerable.Range(1, 6).Select(n => Anexo($"foto-{n}.png", Png, "image/png")),
        ]);

        await EntregarAsync(c, mime);

        var solicitacao = await UnicaAsync(c.Equipe);
        var linha = await InteracoesAsync(c.Equipe, solicitacao);
        linha.Should().HaveCount(2);
        var doContato = linha.Should().ContainSingle(i => i.GetProperty("autorEhCliente").GetBoolean()).Subject;
        doContato.GetProperty("anexos").EnumerateArray().Select(a => a.GetProperty("nomeArquivo").GetString())
            .Should().Equal("foto-1.png", "foto-2.png", "foto-3.png", "foto-4.png", "foto-5.png");
        var nota = linha.Should().ContainSingle(i => i.GetProperty("autorSistema").GetBoolean()).Subject;
        nota.GetProperty("visibilidade").GetString().Should().Be("INTERNA");
        nota.GetProperty("canal").GetString().Should().Be("EMAIL");
        nota.GetProperty("autorEhCliente").GetBoolean().Should().BeFalse();
        nota.GetProperty("autor").GetProperty("id").GetGuid().Should().Be(AutoresDoSistema.EntradaEmail);
        nota.GetProperty("autor").GetProperty("nome").GetString().Should().Be("Acordu (e-mail recebido)");
        nota.GetProperty("texto").GetString().Should().Be(
            "Anexos do e-mail que não entraram na solicitação:\n"
            + "- programa.exe: tipo de arquivo não permitido\n"
            + "- enorme.png: maior que 20 MB\n"
            + "- foto-6.png: além do limite de 5 anexos por e-mail");
        var aviso = api.Emails.Para(c.Email).Should().ContainSingle(m => m.Categoria == "aviso-recebida").Subject;
        aviso.Texto.Should().Contain("Estes arquivos do seu e-mail não entraram na solicitação:")
            .And.Contain("- programa.exe (tipo de arquivo não permitido)")
            .And.Contain("- enorme.png (maior que 20 MB)")
            .And.Contain("- foto-6.png (além do limite de 5 anexos por e-mail)")
            .And.NotContain("foto-1.png");
        await using var db = api.DaEquipe(c.Equipe.TenantId);
        var registro = await db.EmailsRecebidos.AsNoTracking().SingleAsync();
        registro.SolicitacaoId.Should().Be(solicitacao);
        registro.Recusados!.Itens.Select(r => (r.NomeArquivo, r.Motivo)).Should().Equal(
            ("programa.exe", MotivoRecusaAnexo.TipoNaoPermitido),
            ("enorme.png", MotivoRecusaAnexo.GrandeDemais),
            ("foto-6.png", MotivoRecusaAnexo.AlemDoLimite));
        api.Arquivos.ComPrefixo($"{c.Equipe.TenantId}/{solicitacao}/").Should().Be(5, "só os válidos sobem ao storage");
    }

    [Fact] // F15-15, design D14: mensagem anexada e parte assinada são "tipo não permitido", mesmo em texto
    public async Task Entregar_MensagemAnexadaEAssinatura_RecusadasComoTipoNaoPermitido()
    {
        var c = await CenarioAsync();
        var mime = ComAnexos(c.Email,
            new Parte("message/rfc822", "attachment; filename=\"pedido.eml\"", null, "From: a@b.com.br\r\n\r\noi\r\n"u8.ToArray()),
            Anexo("assinatura.asc", "-----BEGIN PGP SIGNATURE-----\r\nabc\r\n"u8.ToArray(), "application/pgp-signature"));

        await EntregarAsync(c, mime);

        var solicitacao = await UnicaAsync(c.Equipe);
        var nota = (await InteracoesAsync(c.Equipe, solicitacao)).Should().ContainSingle().Subject;
        nota.GetProperty("autorSistema").GetBoolean().Should().BeTrue();
        nota.GetProperty("texto").GetString().Should()
            .Contain("- pedido.eml: tipo de arquivo não permitido")
            .And.Contain("- assinatura.asc: tipo de arquivo não permitido");
    }

    [Fact] // F15-98: nem no portal com conta, nem em aviso
    public async Task Portal_ContatoComContaAbreASolicitacao_NotaDoSistemaNuncaAparece()
    {
        var c = await CenarioAsync();
        var comConta = await ContatosDoPortal.CriarAsync(api, c.Equipe, c.Cliente, nome: "Marina Souza");
        var mime = ComAnexos(comConta.Email, Anexo("foto.png", Png, "image/png"), Anexo("programa.exe", Executavel));

        await EntregarAsync(c, mime, comConta.Email);

        var solicitacao = await UnicaAsync(c.Equipe);
        (await InteracoesAsync(c.Equipe, solicitacao)).Should().Contain(i => i.GetProperty("autorSistema").GetBoolean(),
            "a equipe vê a nota");
        var noPortal = await c.Equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/portal/solicitacoes/{solicitacao}/interacoes", token: comConta.Token);
        NaoTemANota(noPortal);
        noPortal.GetArrayLength().Should().Be(1, "só a mensagem dos anexos dele");
        await c.Equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/interacoes",
            new { texto = "Recebemos as fotos.", visibilidade = "EXTERNA" });
        var avisos = api.Emails.Para(comConta.Email).Where(m => m.Categoria.StartsWith("aviso-", StringComparison.Ordinal)).ToList();
        avisos.Select(m => m.Categoria).Should().BeEquivalentTo(["aviso-recebida", "aviso-mensagem"], "a nota não gera aviso");
        avisos.Should().OnlyContain(m => !m.Texto.Contains("Anexos do e-mail que não entraram", StringComparison.Ordinal));
        avisos.Single(m => m.Categoria == "aviso-recebida").Texto.Should().Contain("- programa.exe (tipo de arquivo não permitido)",
            "ao contato, só a lista dos arquivos que não entraram");
    }

    [Fact] // F15-98: nem pela sessão do link do aviso de recebida
    public async Task Portal_ContatoPeloLinkDoAviso_NotaDoSistemaNuncaAparece()
    {
        var c = await CenarioAsync();

        await EntregarAsync(c, ComAnexos(c.Email, Anexo("programa.exe", Executavel)));

        var solicitacao = await UnicaAsync(c.Equipe);
        var acesso = await TrocarAsync(api.Emails.TokenDe(c.Email, "aviso-recebida"));
        var noPortal = await c.Equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/portal/solicitacoes/{solicitacao}/interacoes", token: acesso);
        NaoTemANota(noPortal);
        noPortal.GetArrayLength().Should().Be(0, "todos os anexos foram recusados: nenhuma mensagem do contato");
    }

    [Fact] // F15-97 com anexos: a repetição do mesmo e-mail não duplica anexos, nota nem aviso
    public async Task Entregar_MesmoEmailComAnexosDuasVezes_NadaDuplica()
    {
        var c = await CenarioAsync();
        var mime = ComAnexos(c.Email, Anexo("foto.png", Png, "image/png"), Anexo("programa.exe", Executavel));

        await EntregarAsync(c, mime);
        await EntregarAsync(c, mime);

        var solicitacao = await UnicaAsync(c.Equipe);
        var linha = await InteracoesAsync(c.Equipe, solicitacao);
        linha.Should().HaveCount(2, "a mensagem dos anexos e uma nota só");
        linha.Single(i => i.GetProperty("autorEhCliente").GetBoolean()).GetProperty("anexos").GetArrayLength().Should().Be(1);
        api.Arquivos.ComPrefixo($"{c.Equipe.TenantId}/{solicitacao}/").Should().Be(1);
        api.Emails.Para(c.Email).Count(m => m.Categoria == "aviso-recebida").Should().Be(1);
        await using var db = api.DaEquipe(c.Equipe.TenantId);
        (await db.EmailsRecebidos.CountAsync()).Should().Be(1);
    }

    [Fact] // F15-98: a nota não chega ao sino nem ao feed da equipe
    public async Task Entregar_ParteRecusada_NotaDoSistemaNaoGeraSinoNemAtividade()
    {
        var c = await CenarioAsync();

        await EntregarAsync(c, ComAnexos(c.Email, Anexo("programa-secreto.exe", Executavel)));

        await UnicaAsync(c.Equipe);
        var sino = (await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes")).ToString();
        var feed = (await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/atividades")).ToString();
        foreach (var texto in new[] { sino, feed })
        {
            texto.Should().NotContain("programa-secreto").And.NotContain("não entraram")
                .And.NotContain(AutoresDoSistema.EntradaEmail.ToString());
        }
    }

    [Fact] // isolamento (RNF-SEG-01): outra empresa não vê a solicitação nem a nota; outro cliente do portal também não
    public async Task Entregar_ParteRecusada_NotaInvisivelParaOutraEmpresaEOutroCliente()
    {
        var c = await CenarioAsync();
        await EntregarAsync(c, ComAnexos(c.Email, Anexo("programa.exe", Executavel)));
        var solicitacao = await UnicaAsync(c.Equipe);
        var outra = await Equipe.CriarAsync(api);
        var outroCliente = await Cenarios.ClienteAsync(c.Equipe, "Oficina do Vizinho");
        var vizinho = await ContatosDoPortal.CriarAsync(api, c.Equipe, outroCliente, nome: "Vizinho");

        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/interacoes");
        await c.Equipe.EsperarAsync(
            HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/portal/solicitacoes/{solicitacao}/interacoes", token: vizinho.Token);
        (await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes"))
            .GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact] // o log da entrada não leva nome de arquivo nem endereço (segurança §7)
    public async Task Entregar_AnexosComNomes_LogSemNomeDeArquivoNemEndereco()
    {
        var c = await CenarioAsync();
        var logs = new LogsCapturados(LogLevel.Information);
        await using var app = await api.NovaApiAsync(
            new Dictionary<string, string?> { ["Logs:Formato"] = "json" },
            servicos: s => s.AddSingleton<ILoggerProvider>(logs));
        logs.Formatador = app.Services.GetServices<ConsoleFormatter>().OfType<FormatadorLogJson>().Single();
        using var http = app.GetTestClient();
        var mime = ComAnexos(c.Email, Anexo("contrato-confidencial.png", Png, "image/png"), Anexo("virus-do-joao.exe", Executavel));
        using var conteudo = new ByteArrayContent(mime);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri(EntradaPorEmail.Rota, UriKind.Relative)) { Content = conteudo };
        pedido.Headers.TryAddWithoutValidation("X-Email-Token", ApiFixture.TokenEntrada);
        pedido.Headers.TryAddWithoutValidation("X-Email-Destinatario", c.Endereco);
        pedido.Headers.TryAddWithoutValidation("X-Email-Remetente", c.Email);
        pedido.Headers.TryAddWithoutValidation("X-Email-Filtro", "ok");
        pedido.Headers.TryAddWithoutValidation("X-Email-Autenticidade", EntradaPorEmail.Autenticado);

        using var resposta = await http.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var todas = string.Join('\n', logs.Linhas.Select(l => l.ToString()));
        todas.Should().Contain("Entrada por e-mail: Aberta");
        todas.Should().NotContain("contrato-confidencial").And.NotContain("virus-do-joao")
            .And.NotContain(c.Email).And.NotContain(c.Endereco).And.NotContain("não entraram");
    }

    private static void NaoTemANota(JsonElement noPortal)
    {
        foreach (var i in noPortal.EnumerateArray())
        {
            i.GetProperty("daEquipe").GetBoolean().Should().BeFalse();
            (i.GetProperty("texto").GetString() ?? string.Empty).Should().NotContain("não entraram");
            i.TryGetProperty("autorSistema", out _).Should().BeFalse("o DTO do portal não tem campos internos");
        }
    }

    private async Task<string> TrocarAsync(string token)
    {
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/portal/link", UriKind.Relative), new { token });
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, "corpo: {0}", texto);
        return JsonDocument.Parse(texto).RootElement.GetProperty("accessToken").GetString()!;
    }

    private sealed record Cenario(Equipe Equipe, Guid Cliente, string Email, string Endereco);

    /// <param name="Tipo">O <c>Content-Type</c> inteiro da parte.</param>
    /// <param name="Binario">Sem base64 (a parte grande, para o e-mail inteiro caber no limite da rota).</param>
    private sealed record Parte(string Tipo, string? Disposicao, string? ContentId, byte[] Conteudo, bool Binario = false);
}
