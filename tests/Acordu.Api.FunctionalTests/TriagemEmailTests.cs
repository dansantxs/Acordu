using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Operacao;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F15, F15h (F15-20, F15-24, F15-25, F15-26, F15-70, F15-103; RF-SOL-15; design D6, D10, D11 e D14; ADR 0016,
/// item 4; contrato §18): o e-mail de remetente não identificado, entregue pela rota de entrada como o Worker entrega, vai à
/// triagem da empresa com o motivo, o conteúdo e os anexos dentro da política, uma vez só por entrega, e nada é respondido.
/// A lista da triagem pela API é do F15k: aqui o item é lido do banco, no contexto da empresa (plano-2, menor 4).
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class TriagemEmailTests(ApiFixture api)
{
    private const string Fronteira = "fronteira-da-triagem";
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    private static readonly byte[] Executavel = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    /// <summary>Empresa com o recebimento ligado, um cliente e um contato com e-mail próprio.</summary>
    private async Task<Cenario> CenarioAsync()
    {
        var equipe = await Equipe.CriarAsync(api);
        await EntradaPorEmail.LigarAsync(equipe);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var email = $"carlos-{Guid.NewGuid():N}@bomgrao.com.br";
        var contato = await ContatoAsync(equipe, cliente, email);
        return new Cenario(equipe, cliente, contato, email, await EntradaPorEmail.EnderecoAsync(api, equipe.TenantId));
    }

    private static async Task<Guid> ContatoAsync(Equipe equipe, Guid cliente, string email) =>
        Cenarios.Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos", new { nome = "Carlos Prado", email }));

    private static byte[] Simples(string de, string messageId) =>
        EntradaPorEmail.Mime(de, "Orçamento do forno", messageId, "Quanto custa a troca da resistência?");

    /// <summary>Um e-mail <c>multipart/mixed</c> com o corpo em texto, uma imagem e um executável (que nunca entra).</summary>
    private static byte[] ComAnexos(string de, string messageId) =>
        Encoding.UTF8.GetBytes(
            $"From: {de}\r\n"
            + "To: atendimento@estudiotraco.com.br\r\n"
            + "Subject: =?utf-8?Q?Or=C3=A7amento_do_forno?=\r\n"
            + $"Message-ID: {messageId}\r\n"
            + "Date: Tue, 06 Oct 2026 10:00:00 -0300\r\n"
            + "MIME-Version: 1.0\r\n"
            + $"Content-Type: multipart/mixed; boundary=\"{Fronteira}\"\r\n\r\n"
            + $"--{Fronteira}\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n"
            + "Quanto custa a troca da resistência?\r\n"
            + Parte("foto-do-forno.png", "image/png", Png)
            + Parte("programa.exe", "application/octet-stream", Executavel)
            + $"--{Fronteira}--\r\n");

    private static string Parte(string nome, string tipo, byte[] conteudo) =>
        $"--{Fronteira}\r\nContent-Type: {tipo}; name=\"{nome}\"\r\nContent-Disposition: attachment; filename=\"{nome}\"\r\n"
        + $"Content-Transfer-Encoding: base64\r\n\r\n{Convert.ToBase64String(conteudo)}\r\n";

    private async Task<List<ItemTriagemEmail>> ItensAsync(Guid tenantId)
    {
        await using var db = api.DaEquipe(tenantId);
        return await db.ItensTriagemEmail.AsNoTracking().ToListAsync();
    }

    /// <summary>O que a entrada pode ter gravado na empresa: registro, solicitação, mensagem e item da triagem.</summary>
    private async Task<(int Registros, int Solicitacoes, int Mensagens, int Triagem)> GravadoAsync(Guid tenantId)
    {
        await using var db = api.DaEquipe(tenantId);
        return (await db.EmailsRecebidos.CountAsync(), await db.Solicitacoes.CountAsync(), await db.Interacoes.CountAsync(),
            await db.ItensTriagemEmail.CountAsync());
    }

    [Fact] // F15-70
    public async Task Entregar_RemetenteNaoIdentificado_ItemComRemetenteAssuntoDataTextoAnexosEMotivoSemResponder()
    {
        var c = await CenarioAsync();
        var estranho = $"pedro-{Guid.NewGuid():N}@outro.com.br";
        var avisosAoDono = api.Emails.Para(c.Equipe.EmailDono).Count;

        await EntradaPorEmail.EntregarAsync(
            api, c.Endereco, ComAnexos($"=?utf-8?Q?Pedro_Ara=C3=BAjo?= <{estranho.ToUpperInvariant()}>", EntradaPorEmail.NovoMessageId()), estranho);

        var item = (await ItensAsync(c.Equipe.TenantId)).Should().ContainSingle().Subject;
        item.RemetenteEmail.Should().Be(estranho, "o endereço normalizado como o dos contatos");
        item.RemetenteNome.Should().Be("Pedro Araújo");
        item.Assunto.Should().Be("Orçamento do forno");
        item.Texto.Should().Be("Quanto custa a troca da resistência?");
        item.RecebidoEm.Should().BeCloseTo(api.Relogio.Agora, TimeSpan.FromMilliseconds(1), "o horário da recepção, não o Date (D14)");
        item.Motivo.Should().Be(MotivoTriagem.Desconhecido);
        var anexo = item.Anexos.Should().ContainSingle("o executável fica de fora").Subject;
        anexo.NomeArquivo.Should().Be("foto-do-forno.png");
        anexo.TipoConteudo.Should().Be("image/png");
        anexo.ChaveArmazenamento.Should().StartWith($"{c.Equipe.TenantId}/triagem/{item.Id}/", "a chave da triagem (segurança §7)");
        api.Arquivos.Conteudo(anexo.ChaveArmazenamento).Should().Equal(Png);
        item.Recusados!.Itens.Should().ContainSingle()
            .Which.Should().Be(new AnexoRecusado("programa.exe", MotivoRecusaAnexo.TipoNaoPermitido));
        await using var db = api.DaEquipe(c.Equipe.TenantId);
        var registro = await db.EmailsRecebidos.AsNoTracking().SingleAsync();
        registro.Resultado.Should().Be(ResultadoEntradaEmail.Triagem);
        registro.ItemTriagemId.Should().Be(item.Id);
        (await GravadoAsync(c.Equipe.TenantId)).Should().Be((1, 0, 0, 1), "nem solicitação, nem mensagem");
        api.Emails.Para(estranho).Should().BeEmpty("nada é respondido ao remetente");
        api.Emails.Para(c.Equipe.EmailDono).Should().HaveCount(avisosAoDono, "a chegada à triagem não manda e-mail");
    }

    [Fact] // F15-70 (e a regra de log da convenção §5, que o F15-109 fecha no F15n)
    public async Task Entregar_RemetenteNaoIdentificado_LogSoComODesfecho()
    {
        var c = await CenarioAsync();
        var estranho = $"pedro-{Guid.NewGuid():N}@outro.com.br";
        var logs = new LogsCapturados(LogLevel.Information);
        await using var app = await api.NovaApiAsync(
            new Dictionary<string, string?> { ["Logs:Formato"] = "json" },
            servicos: s => s.AddSingleton<ILoggerProvider>(logs));
        logs.Formatador = app.Services.GetServices<ConsoleFormatter>().OfType<FormatadorLogJson>().Single();
        using var http = app.GetTestClient();
        using var conteudo = new ByteArrayContent(ComAnexos($"Pedro <{estranho}>", EntradaPorEmail.NovoMessageId()));
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri(EntradaPorEmail.Rota, UriKind.Relative)) { Content = conteudo };
        pedido.Headers.TryAddWithoutValidation("X-Email-Token", ApiFixture.TokenEntrada);
        pedido.Headers.TryAddWithoutValidation("X-Email-Destinatario", c.Endereco);
        pedido.Headers.TryAddWithoutValidation("X-Email-Remetente", estranho);
        pedido.Headers.TryAddWithoutValidation("X-Email-Filtro", "ok");
        pedido.Headers.TryAddWithoutValidation("X-Email-Autenticidade", EntradaPorEmail.Autenticado);

        using var resposta = await http.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await resposta.Content.ReadAsStringAsync()).Should().BeEmpty("a rota não diz que foi à triagem");
        var todas = string.Join('\n', logs.Linhas.Select(l => l.ToString()));
        todas.Should().Contain("Entrada por e-mail: Triagem.");
        todas.Should().NotContain(estranho).And.NotContain(c.Endereco).And.NotContain("Orçamento")
            .And.NotContain("foto-do-forno").And.NotContain("programa.exe").And.NotContain("resistência");
    }

    [Fact] // F15-20 (isolamento entre tenants)
    public async Task Entregar_ContatoDeOutraEmpresa_TriagemDaEmpresaDoEnderecoENadaNaOutra()
    {
        var a = await CenarioAsync();
        var b = await CenarioAsync();

        await EntradaPorEmail.EntregarAsync(
            api, a.Endereco, Simples($"Carlos Prado <{b.Email}>", EntradaPorEmail.NovoMessageId()), b.Email);

        var item = (await ItensAsync(a.Equipe.TenantId)).Should().ContainSingle().Subject;
        item.Motivo.Should().Be(MotivoTriagem.Desconhecido, "em A, ninguém tem aquele e-mail");
        item.RemetenteEmail.Should().Be(b.Email);
        (await GravadoAsync(a.Equipe.TenantId)).Should().Be((1, 0, 0, 1));
        (await GravadoAsync(b.Equipe.TenantId)).Should().Be((0, 0, 0, 0), "nada é criado em B");
        api.Emails.Para(b.Email).Should().BeEmpty();
    }

    [Fact] // F15-24 (pergunta 5)
    public async Task Entregar_ContatoEmDoisClientes_TriagemMaisDeUmClienteComOsClientesPossiveis()
    {
        var c = await CenarioAsync();
        var outroCliente = await Cenarios.ClienteAsync(c.Equipe, "Mercado Central");
        await ContatoAsync(c.Equipe, outroCliente, c.Email);

        await EntradaPorEmail.EntregarAsync(api, c.Endereco, Simples($"Carlos Prado <{c.Email}>", EntradaPorEmail.NovoMessageId()), c.Email);

        var item = (await ItensAsync(c.Equipe.TenantId)).Should().ContainSingle().Subject;
        item.Motivo.Should().Be(MotivoTriagem.MaisDeUmCliente);
        item.ClientesPossiveis.Should().BeEquivalentTo(new[] { c.Cliente, outroCliente });
        (await GravadoAsync(c.Equipe.TenantId)).Solicitacoes.Should().Be(0, "nenhuma solicitação é criada sozinha");
        api.Emails.Para(c.Email).Should().BeEmpty();
    }

    [Theory] // F15-25
    [InlineData("contato-inativo")]
    [InlineData("cliente-inativo")]
    public async Task Entregar_ContatoOuClienteInativo_TriagemContatoInativoSemSolicitacao(string caso)
    {
        var c = await CenarioAsync();
        var rota = caso == "contato-inativo" ? $"/api/v1/clientes/{c.Cliente}/contatos/{c.Contato}" : $"/api/v1/clientes/{c.Cliente}";
        await c.Equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, rota);

        await EntradaPorEmail.EntregarAsync(api, c.Endereco, Simples($"Carlos Prado <{c.Email}>", EntradaPorEmail.NovoMessageId()), c.Email);

        var item = (await ItensAsync(c.Equipe.TenantId)).Should().ContainSingle().Subject;
        item.Motivo.Should().Be(MotivoTriagem.ContatoInativo);
        item.ClientesPossiveis.Should().BeEmpty();
        (await GravadoAsync(c.Equipe.TenantId)).Should().Be((1, 0, 0, 1));
        api.Emails.Para(c.Email).Should().BeEmpty();
    }

    [Theory] // F15-26 (pergunta 6), com o resultado da conferência simulado pelo veredito do Worker (D11)
    [InlineData("fail")]
    [InlineData("none")]
    [InlineData(null)]
    public async Task Entregar_ContatoComDominioNaoConfirmado_TriagemRemetenteNaoConfirmadoSemSolicitacaoNemMensagem(string? autenticidade)
    {
        var c = await CenarioAsync();

        await EntradaPorEmail.EntregarAsync(
            api, c.Endereco, Simples($"Carlos Prado <{c.Email}>", EntradaPorEmail.NovoMessageId()), c.Email, autenticidade);

        var item = (await ItensAsync(c.Equipe.TenantId)).Should().ContainSingle().Subject;
        item.Motivo.Should().Be(MotivoTriagem.RemetenteNaoConfirmado);
        item.RemetenteEmail.Should().Be(c.Email);
        (await GravadoAsync(c.Equipe.TenantId)).Should().Be((1, 0, 0, 1), "nenhuma solicitação nem mensagem");
        await using var db = api.DaEquipe(c.Equipe.TenantId);
        (await db.ConversasPorEmail.CountAsync()).Should().Be(0);
        api.Emails.Para(c.Email).Should().BeEmpty("nada é respondido a um remetente que pode ser forjado");
    }

    [Fact] // F15-103
    public async Task Entregar_MesmoEmailNaoIdentificadoDuasVezes_UmItemSo()
    {
        var c = await CenarioAsync();
        var estranho = $"pedro-{Guid.NewGuid():N}@outro.com.br";
        var mime = ComAnexos($"Pedro <{estranho}>", EntradaPorEmail.NovoMessageId());

        await EntradaPorEmail.EntregarAsync(api, c.Endereco, mime, estranho);
        await EntradaPorEmail.EntregarAsync(api, c.Endereco, mime, estranho);

        (await GravadoAsync(c.Equipe.TenantId)).Should().Be((1, 0, 0, 1));
        api.Arquivos.ComPrefixo($"{c.Equipe.TenantId}/triagem/").Should().Be(1, "o anexo válido sobe uma vez só");
    }

    [Fact] // F15-103: entregas iguais ao mesmo tempo; a corrida cai no índice do registro e os anexos de quem perdeu saem
    public async Task Entregar_MesmoEmailNaoIdentificadoEmParalelo_UmItemSoESemArquivoOrfao()
    {
        var c = await CenarioAsync();
        var estranho = $"pedro-{Guid.NewGuid():N}@outro.com.br";
        var mime = ComAnexos($"Pedro <{estranho}>", EntradaPorEmail.NovoMessageId());

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => EntradaPorEmail.EntregarAsync(api, c.Endereco, mime, estranho)));

        (await GravadoAsync(c.Equipe.TenantId)).Should().Be((1, 0, 0, 1));
        var item = (await ItensAsync(c.Equipe.TenantId)).Single();
        api.Arquivos.ComPrefixo($"{c.Equipe.TenantId}/triagem/").Should().Be(1, "só o do item que ficou");
        api.Arquivos.ComPrefixo($"{c.Equipe.TenantId}/triagem/{item.Id}/").Should().Be(1);
    }

    [Fact] // F15-103: o mesmo e-mail mandado a duas empresas é uma entrega em cada (a chave leva o destino, D6)
    public async Task Entregar_MesmoEmailNaoIdentificadoParaDuasEmpresas_UmItemEmCada()
    {
        var a = await CenarioAsync();
        var b = await CenarioAsync();
        var estranho = $"pedro-{Guid.NewGuid():N}@outro.com.br";
        var mime = Simples($"Pedro <{estranho}>", EntradaPorEmail.NovoMessageId());

        await EntradaPorEmail.EntregarAsync(api, a.Endereco, mime, estranho);
        await EntradaPorEmail.EntregarAsync(api, b.Endereco, mime, estranho);

        (await ItensAsync(a.Equipe.TenantId)).Should().ContainSingle().Which.TenantId.Should().Be(a.Equipe.TenantId);
        (await ItensAsync(b.Equipe.TenantId)).Should().ContainSingle().Which.TenantId.Should().Be(b.Equipe.TenantId);
    }

    [Fact] // F15-70 (D10): From com vários endereços, mesmo com o de um contato entre eles, é DESCONHECIDO e sem remetente
    public async Task Entregar_FromComVariosEnderecos_TriagemDesconhecidoSemRemetenteENome()
    {
        var c = await CenarioAsync();

        await EntradaPorEmail.EntregarAsync(
            api, c.Endereco, Simples($"Carlos Prado <{c.Email}>, Pedro <pedro-{Guid.NewGuid():N}@outro.com.br>", EntradaPorEmail.NovoMessageId()), c.Email);

        var item = (await ItensAsync(c.Equipe.TenantId)).Should().ContainSingle().Subject;
        item.Motivo.Should().Be(MotivoTriagem.Desconhecido);
        item.RemetenteEmail.Should().BeNull("não há um endereço só");
        item.RemetenteNome.Should().BeNull();
        item.Assunto.Should().Be("Orçamento do forno");
        (await GravadoAsync(c.Equipe.TenantId)).Should().Be((1, 0, 0, 1));
        api.Emails.Para(c.Email).Should().BeEmpty();
    }

    [Theory] // F15-70 (D10): From inválido ou vazio vai à triagem como DESCONHECIDO, sem remetente; a rota segue em 202
    [InlineData("sem-arroba")]
    [InlineData("")]
    public async Task Entregar_FromInvalidoOuVazio_TriagemDesconhecidoSemRemetente(string de)
    {
        var c = await CenarioAsync();

        await EntradaPorEmail.EntregarAsync(api, c.Endereco, Simples(de, EntradaPorEmail.NovoMessageId()), c.Email);

        var item = (await ItensAsync(c.Equipe.TenantId)).Should().ContainSingle().Subject;
        item.Motivo.Should().Be(MotivoTriagem.Desconhecido);
        item.RemetenteEmail.Should().BeNull();
        item.Texto.Should().Be("Quanto custa a troca da resistência?");
        (await GravadoAsync(c.Equipe.TenantId)).Should().Be((1, 0, 0, 1));
    }

    [Fact] // D12: o automático de quem não é conhecido é descartado antes da triagem (nada de item nem de arquivo)
    public async Task Entregar_AutomaticoDeNaoIdentificado_DescartadoSemItemNaTriagem()
    {
        var c = await CenarioAsync();
        var estranho = $"pedro-{Guid.NewGuid():N}@outro.com.br";
        var mime = EntradaPorEmail.Mime(
            $"Pedro <{estranho}>", "Resposta automática", EntradaPorEmail.NovoMessageId(), cabecalhos: "Auto-Submitted: auto-replied\r\n");

        await EntradaPorEmail.EntregarAsync(api, c.Endereco, mime, estranho);

        await using var db = api.DaEquipe(c.Equipe.TenantId);
        var registro = await db.EmailsRecebidos.AsNoTracking().SingleAsync();
        registro.Resultado.Should().Be(ResultadoEntradaEmail.Ignorado);
        registro.ItemTriagemId.Should().BeNull();
        (await GravadoAsync(c.Equipe.TenantId)).Should().Be((1, 0, 0, 0));
    }

    [Fact] // F15-103 (D6): sem Message-ID a chave sai do MIME inteiro; a mesma entrega duas vezes ainda é um item só
    public async Task Entregar_MesmoEmailNaoIdentificadoSemMessageIdDuasVezes_UmItemSo()
    {
        var c = await CenarioAsync();
        var estranho = $"pedro-{Guid.NewGuid():N}@outro.com.br";
        var mime = EntradaPorEmail.Mime($"Pedro <{estranho}>", "Orçamento", null, $"Corpo único {Guid.NewGuid():N}");

        await EntradaPorEmail.EntregarAsync(api, c.Endereco, mime, estranho);
        await EntradaPorEmail.EntregarAsync(api, c.Endereco, mime, estranho);

        (await GravadoAsync(c.Equipe.TenantId)).Should().Be((1, 0, 0, 1));
    }

    [Fact] // F15-26 e F15-20: o forjado de quem nem é contato é só DESCONHECIDO (o "não confirmado" é do From de um contato)
    public async Task Entregar_DesconhecidoComAutenticidadeFalhando_TriagemDesconhecido()
    {
        var c = await CenarioAsync();
        var estranho = $"pedro-{Guid.NewGuid():N}@outro.com.br";

        await EntradaPorEmail.EntregarAsync(api, c.Endereco, Simples($"Pedro <{estranho}>", EntradaPorEmail.NovoMessageId()), estranho, "fail");

        (await ItensAsync(c.Equipe.TenantId)).Should().ContainSingle().Which.Motivo.Should().Be(MotivoTriagem.Desconhecido);
    }

    [Fact] // F15-24 e F15-26: dois clientes mas sem a autenticidade vale o "não confirmado", sem clientes possíveis
    public async Task Entregar_ContatoEmDoisClientesSemAutenticidade_NaoConfirmadoSemClientesPossiveis()
    {
        var c = await CenarioAsync();
        var outroCliente = await Cenarios.ClienteAsync(c.Equipe, "Mercado Central");
        await ContatoAsync(c.Equipe, outroCliente, c.Email);

        await EntradaPorEmail.EntregarAsync(api, c.Endereco, Simples($"Carlos <{c.Email}>", EntradaPorEmail.NovoMessageId()), c.Email, "none");

        var item = (await ItensAsync(c.Equipe.TenantId)).Should().ContainSingle().Subject;
        item.Motivo.Should().Be(MotivoTriagem.RemetenteNaoConfirmado);
        item.ClientesPossiveis.Should().BeEmpty();
    }

    private sealed record Cenario(Equipe Equipe, Guid Cliente, Guid Contato, string Email, string Endereco);
}
