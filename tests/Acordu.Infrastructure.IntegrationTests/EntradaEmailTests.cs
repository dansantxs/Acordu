using System.Security.Cryptography;
using System.Text;
using Acordu.Application.Common;
using Acordu.Application.EntradaEmail;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using Acordu.Infrastructure.Email.Entrada;
using Acordu.Infrastructure.Persistence.EntradaEmail;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>
/// Spec do F15, F15d (F15-05, F15-19, F15-22, F15-89; design D6, D12 e D14; ADR 0016, itens 3 e 8): a consulta global do
/// código de entrada (ponto revisado), o registro de entrada por tenant com a chave única e a leitura do MIME.
/// </summary>
[Collection(ColecaoBanco.Nome)]
public sealed class EntradaEmailTests(BancoFixture banco)
{
    private static byte[] Mime(string? messageId, string from = "Ana <ana@cliente.com.br>") => Encoding.UTF8.GetBytes(
        $"From: {from}\r\n"
        + "To: k7m2qd9w@entrada.acordu.app.br\r\n"
        + "Subject: Forno parou\r\n"
        + (messageId is null ? string.Empty : $"Message-ID: {messageId}\r\n")
        + "MIME-Version: 1.0\r\n"
        + "Content-Type: text/plain; charset=utf-8\r\n"
        + "\r\n"
        + "O forno parou de novo.\r\n");

    /// <summary>Empresa gravada no contexto dela, com o recebimento como pedido e, se preciso, a situação por SQL.</summary>
    private async Task<Tenant> EmpresaAsync(bool ligado = true, string? situacao = null)
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio Traço", Dados.Agora, Guid.NewGuid(), TimeSpan.FromDays(30));
        if (ligado)
            tenant.LigarRecebimentoEmail();
        await using var db = banco.DaEquipe(tenant.Id);
        db.Add(tenant);
        await db.SaveChangesAsync();
        if (situacao is not null)
            await db.Database.ExecuteSqlAsync($"UPDATE tenants SET situacao = {situacao} WHERE id = {tenant.Id}");
        return tenant;
    }

    private async Task<EmpresaDeEntrada?> PorCodigoAsync(string codigo)
    {
        // Sem tenant no contexto, como a rota de entrada: só a consulta revisada enxerga as empresas.
        await using var db = banco.Contexto(Quem.Sistema());
        return await new EmpresasDeEntrada(db).PorCodigoAsync(codigo);
    }

    [Fact]
    public async Task PorCodigoAsync_EmpresaLigada_IdESituacaoSemTenantNoContexto()
    {
        var tenant = await EmpresaAsync();

        var empresa = await PorCodigoAsync(tenant.CodigoEntrada!);

        empresa.Should().Be(new EmpresaDeEntrada(tenant.Id, SomenteLeitura: false, RecebimentoLigado: true));
    }

    [Fact]
    public async Task PorCodigoAsync_EmpresaDesligada_RecebimentoDesligado() // F15-05
    {
        var tenant = await EmpresaAsync(ligado: false);

        (await PorCodigoAsync(tenant.CodigoEntrada!))!.RecebimentoLigado.Should().BeFalse();
    }

    [Theory]
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task PorCodigoAsync_EmpresaSoLeitura_SomenteLeitura(string situacao) // F15-19
    {
        var tenant = await EmpresaAsync(situacao: situacao);

        (await PorCodigoAsync(tenant.CodigoEntrada!))!.SomenteLeitura.Should().BeTrue();
    }

    [Theory]
    [InlineData("k7m2qd9w")] // no formato, de ninguém (nunca existiu, ou expurgada)
    [InlineData("k7m2qd")]
    [InlineData("K7M2QD9W")]
    [InlineData("' or 1=1")]
    public async Task PorCodigoAsync_CodigoDeNinguem_Nulo(string codigo) // F15-22
    {
        await EmpresaAsync();

        (await PorCodigoAsync(codigo)).Should().BeNull();
    }

    [Fact]
    public async Task PorCodigoAsync_CodigoDeUmaEmpresa_NuncaOutra() // ADR 0016, item 3
    {
        var a = await EmpresaAsync();
        var b = await EmpresaAsync();

        (await PorCodigoAsync(a.CodigoEntrada!))!.TenantId.Should().Be(a.Id);
        (await PorCodigoAsync(b.CodigoEntrada!))!.TenantId.Should().Be(b.Id);
    }

    [Fact]
    public async Task EmailRecebido_GravadoERelidoNoTenant()
    {
        var tenant = await EmpresaAsync();
        var registro = Dados.EmailRecebido(tenant.Id);
        await using (var db = banco.DaEquipe(tenant.Id))
        {
            db.Add(registro);
            await db.SaveChangesAsync();
        }

        await using var leitura = banco.DaEquipe(tenant.Id);
        var lido = await leitura.EmailsRecebidos.AsNoTracking().SingleAsync();
        lido.ChaveEntrada.Should().Be(registro.ChaveEntrada);
        lido.Resultado.Should().Be(Domain.Enums.ResultadoEntradaEmail.Ignorado);
        lido.RecebidoEm.Should().Be(Dados.Agora);
        (await new EmailsRecebidos(leitura).ExisteAsync(registro.ChaveEntrada)).Should().BeTrue();
    }

    [Fact]
    public async Task EmailRecebido_MesmaChaveNaMesmaEmpresa_IndiceUnicoReconhecidoComoRepeticao() // design D6
    {
        var tenant = await EmpresaAsync();
        var chave = Dados.EmailRecebido(tenant.Id).ChaveEntrada;
        await using (var db = banco.DaEquipe(tenant.Id))
        {
            db.Add(Dados.EmailRecebido(tenant.Id, chave));
            await db.SaveChangesAsync();
        }

        await using var corrida = banco.DaEquipe(tenant.Id);
        corrida.Add(Dados.EmailRecebido(tenant.Id, chave));
        var gravar = () => corrida.SaveChangesAsync();

        var excecao = await gravar.Should().ThrowAsync<DbUpdateException>();
        EmailsRecebidos.ChaveRepetida(excecao.Which).Should().BeTrue("a corrida de duas entregas iguais é repetição");
    }

    [Fact]
    public async Task EmailRecebido_MesmaChaveEmOutraEmpresa_GravaENaoSeVe() // F15-21 (base): o registro é por tenant
    {
        var a = await EmpresaAsync();
        var b = await EmpresaAsync();
        var chave = Dados.EmailRecebido(a.Id).ChaveEntrada;
        await using (var db = banco.DaEquipe(a.Id))
        {
            db.Add(Dados.EmailRecebido(a.Id, chave));
            await db.SaveChangesAsync();
        }

        await using (var db = banco.DaEquipe(b.Id))
        {
            (await new EmailsRecebidos(db).ExisteAsync(chave)).Should().BeFalse("o registro da empresa A não existe na B");
            db.Add(Dados.EmailRecebido(b.Id, chave));
            await db.SaveChangesAsync();
        }

        await using var leitura = banco.DaEquipe(b.Id);
        (await leitura.EmailsRecebidos.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task EmailRecebido_EmpresaSoLeitura_GuardaRecusa(string situacao) // F15-19, ADR 0015
    {
        var tenant = await EmpresaAsync(situacao: situacao);
        await using var db = banco.DaEquipe(tenant.Id);
        db.Add(Dados.EmailRecebido(tenant.Id));

        var gravar = () => db.SaveChangesAsync();

        await gravar.Should().ThrowAsync<EmpresaSomenteLeituraException>();
    }

    [Fact]
    public async Task LerAsync_EmailComMessageId_IdentificacaoEHashDoMime()
    {
        var mime = Mime("<abc@mail.cliente.com.br>");
        await using var corpo = new MemoryStream(mime);

        var mensagem = await LeitorMime.LerAsync(corpo, mime.Length);

        mensagem!.MessageId.Should().Be("abc@mail.cliente.com.br");
        mensagem.Remetentes.Should().Equal("ana@cliente.com.br");
        mensagem.HashDoConteudo.Should().Be(Convert.ToHexStringLower(SHA256.HashData(mime)));
    }

    [Fact]
    public async Task LerAsync_SemMessageIdComVariosRemetentes_MessageIdNuloERemetentesComoVieram()
    {
        var mime = Mime(null, "Ana <ana@cliente.com.br>, bruno@cliente.com.br");
        await using var corpo = new MemoryStream(mime);

        var mensagem = await LeitorMime.LerAsync(corpo, mime.Length);

        mensagem!.MessageId.Should().BeNull();
        mensagem.Remetentes.Should().Equal("ana@cliente.com.br", "bruno@cliente.com.br");
    }

    [Fact]
    public async Task LerAsync_UmByteAcimaDoMaximo_CorpoGrandeDemais() // F15-89
    {
        var mime = Mime("<abc@mail.cliente.com.br>");
        await using var corpo = new MemoryStream(mime);

        var ler = () => LeitorMime.LerAsync(corpo, mime.Length - 1);

        await ler.Should().ThrowAsync<CorpoGrandeDemaisException>();
    }

    [Fact]
    public async Task LerAsync_RespostaAutomaticaEmMassa_SinaisDoD12() // F15-85: os cabeçalhos que o descarte usa
    {
        var mime = Encoding.UTF8.GetBytes(
            "From: Ana <ana@cliente.com.br>\r\n"
            + "To: k7m2qd9w@entrada.acordu.app.br\r\n"
            + "Subject: Ausente\r\n"
            + "Message-ID: <ferias@mail.cliente.com.br>\r\n"
            + "Auto-Submitted: auto-replied\r\n"
            + "Precedence: bulk\r\n"
            + "List-Unsubscribe: <mailto:sair@cliente.com.br>\r\n"
            + "X-Autoreply: yes\r\n"
            + "MIME-Version: 1.0\r\n"
            + "Content-Type: multipart/report; report-type=delivery-status; boundary=\"b\"\r\n"
            + "\r\n"
            + "--b\r\nContent-Type: text/plain\r\n\r\nEstou de férias.\r\n--b--\r\n");
        await using var corpo = new MemoryStream(mime);

        var mensagem = await LeitorMime.LerAsync(corpo, mime.Length);

        mensagem!.Sinais.Should().Be(new SinaisDeAutomatico(
            AutoSubmitted: "auto-replied", Precedence: "bulk", ListaDeDistribuicao: true, RespostaAutomatica: true, TipoDoCorpo: "multipart/report"));
    }

    [Fact]
    public async Task LerAsync_EmailComum_SemSinais()
    {
        var mime = Mime("<abc@mail.cliente.com.br>");
        await using var corpo = new MemoryStream(mime);

        var mensagem = await LeitorMime.LerAsync(corpo, mime.Length);

        mensagem!.Sinais.Should().Be(new SinaisDeAutomatico(TipoDoCorpo: "text/plain"));
        DescarteDeEntrada.Automatico(mensagem).Should().BeFalse();
    }

    [Fact] // F15-11, F15-13: o assunto decodificado e o texto do corpo
    public async Task LerAsync_EmailComum_AssuntoETexto()
    {
        var mime = Mime("<abc@mail.cliente.com.br>");
        await using var corpo = new MemoryStream(mime);

        var mensagem = await LeitorMime.LerAsync(corpo, mime.Length);

        mensagem!.Assunto.Should().Be("Forno parou");
        mensagem.Texto.Should().StartWith("O forno parou de novo.");
        mensagem.Autenticidade.Should().BeNull("o veredito vem do Worker, não do MIME");
    }

    [Fact] // F15-13: texto e HTML juntos, a parte em texto vale
    public async Task LerAsync_TextoEHtml_ParteEmTexto()
    {
        var mime = Encoding.UTF8.GetBytes(
            "From: Ana <ana@cliente.com.br>\r\n"
            + "Subject: =?utf-8?Q?Forno_n=C3=A3o_liga?=\r\n"
            + "MIME-Version: 1.0\r\n"
            + "Content-Type: multipart/alternative; boundary=\"b\"\r\n"
            + "\r\n"
            + "--b\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nO forno não liga.\r\n"
            + "--b\r\nContent-Type: text/html; charset=utf-8\r\n\r\n<p>O <b>forno</b> não liga (HTML).</p>\r\n--b--\r\n");
        await using var corpo = new MemoryStream(mime);

        var mensagem = await LeitorMime.LerAsync(corpo, mime.Length);

        mensagem!.Assunto.Should().Be("Forno não liga", "o assunto codificado (RFC 2047) chega decodificado");
        mensagem.Texto.Should().Contain("O forno não liga.").And.NotContain("HTML");
    }

    [Fact] // F15-13 e design D14: só HTML vira texto, sem marcação nem script
    public async Task LerAsync_SoHtml_TextoSemMarcacaoNemScript()
    {
        var mime = Encoding.UTF8.GetBytes(
            "From: Ana <ana@cliente.com.br>\r\n"
            + "Subject: Forno\r\n"
            + "MIME-Version: 1.0\r\n"
            + "Content-Type: text/html; charset=utf-8\r\n"
            + "\r\n"
            + "<html><head><style>p { color: red; }</style><script>alert('x')</script></head>"
            + "<body><p>O <b>forno</b> parou &amp; esfriou.</p><p>Segunda linha</p></body></html>\r\n");
        await using var corpo = new MemoryStream(mime);

        var mensagem = await LeitorMime.LerAsync(corpo, mime.Length);

        mensagem!.Texto.Should().Contain("O forno parou & esfriou.").And.Contain("Segunda linha");
        mensagem.Texto.Should().NotContain("<").And.NotContain("alert").And.NotContain("color");
    }

    [Fact] // F15-12, F15-13: sem assunto nem corpo
    public async Task LerAsync_SemAssuntoNemCorpo_Nulos()
    {
        var mime = Encoding.UTF8.GetBytes("From: Ana <ana@cliente.com.br>\r\nMessage-ID: <x@cliente.com.br>\r\n\r\n");
        await using var corpo = new MemoryStream(mime);

        var mensagem = await LeitorMime.LerAsync(corpo, mime.Length);

        mensagem!.Assunto.Should().BeNull();
        ConteudoDaAbertura.Descricao(mensagem.Texto).Should().BeNull();
    }

    [Fact] // F15-10, F15-21, F15-24, F15-25: os contatos com o e-mail, só da empresa do contexto
    public async Task ContatosDeEntrada_PorEmail_SoDaEmpresaComAtivoEInativo()
    {
        var a = await EmpresaAsync();
        var b = await EmpresaAsync();
        var ativo = Cliente.Criar(Guid.NewGuid(), a.Id, "Padaria Bom Grão", null, null);
        var contato = ativo.AdicionarContato("Ana", "ana@cliente.com.br", null, null);
        var outro = Cliente.Criar(Guid.NewGuid(), a.Id, "Mercado Central", null, null);
        var inativo = outro.AdicionarContato("Ana", "ana@cliente.com.br", null, null);
        outro.InativarContato(inativo.Id);
        outro.AdicionarContato("Beto", "beto@cliente.com.br", null, null);
        var deB = Cliente.Criar(Guid.NewGuid(), b.Id, "Padaria de B", null, null);
        deB.AdicionarContato("Ana", "ana@cliente.com.br", null, null);
        await using (var db = banco.DaEquipe(a.Id))
        {
            db.AddRange(ativo, outro);
            await db.SaveChangesAsync();
        }

        await using (var db = banco.DaEquipe(b.Id))
        {
            db.Add(deB);
            await db.SaveChangesAsync();
        }

        await using var leitura = banco.DaEquipe(a.Id);
        var contatos = await new ContatosDeEntrada(leitura).PorEmailAsync("ana@cliente.com.br");

        var esperados = new[]
        {
            new ContatoDeEntrada(ativo.Id, contato.Id, null, ClienteAtivo: true, ContatoAtivo: true),
            new ContatoDeEntrada(outro.Id, inativo.Id, null, ClienteAtivo: true, ContatoAtivo: false),
        };
        contatos.Should().BeEquivalentTo(esperados, "o contato da empresa B com o mesmo e-mail não aparece em A (isolamento entre tenants)");
        IdentificacaoDoRemetente.Escolher(contatos)!.ContatoId.Should().Be(contato.Id);
    }

    [Fact]
    public async Task LerAsync_CorpoVazio_Nulo() // plano-2, menor 6: ilegível não vira erro
    {
        await using var corpo = new MemoryStream([]);

        (await LeitorMime.LerAsync(corpo, 1024)).Should().BeNull();
    }

    private static readonly byte[] CabecalhoPng = [0x89, 0x50, 0x4E, 0x47];

    private static string NovaChave() => Convert.ToHexStringLower(SHA256.HashData(Guid.NewGuid().ToByteArray()));

    [Fact] // F15-15 (design D6): a abertura guarda a solicitação e os recusados em jsonb, e o aviso os lê por ela
    public async Task EmailRecebido_ComSolicitacaoERecusados_GravadoERelidoPelaSolicitacao()
    {
        var tenant = await EmpresaAsync();
        var solicitacao = Guid.NewGuid();
        var recusados = new AnexosRecusados(
        [
            new AnexoRecusado("programa.exe", MotivoRecusaAnexo.TipoNaoPermitido),
            new AnexoRecusado("vídeo \"grande\".mp4", MotivoRecusaAnexo.GrandeDemais),
            new AnexoRecusado("6.png", MotivoRecusaAnexo.AlemDoLimite),
        ]);
        await using (var db = banco.DaEquipe(tenant.Id))
        {
            db.Add(EmailRecebido.Registrar(
                Guid.NewGuid(), tenant.Id, NovaChave(), null, ResultadoEntradaEmail.Solicitacao, Dados.Agora, solicitacao, recusados));
            db.Add(EmailRecebido.Registrar(
                Guid.NewGuid(), tenant.Id, NovaChave(), null, ResultadoEntradaEmail.Solicitacao, Dados.Agora, Guid.NewGuid()));
            await db.SaveChangesAsync();
        }

        await using var leitura = banco.DaEquipe(tenant.Id);
        var registros = new EmailsRecebidos(leitura);
        (await registros.RecusadosDaAberturaAsync(solicitacao)).Should().Be(recusados, "o nome e o motivo, na ordem");
        (await leitura.EmailsRecebidos.AsNoTracking().SingleAsync(e => e.Recusados == null)).SolicitacaoId.Should().NotBeNull();
        (await registros.RecusadosDaAberturaAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact] // isolamento entre tenants (RNF-SEG-01): os recusados da empresa A não aparecem na B
    public async Task RecusadosDaAberturaAsync_OutraEmpresa_Nulo()
    {
        var a = await EmpresaAsync();
        var b = await EmpresaAsync();
        var solicitacao = Guid.NewGuid();
        await using (var db = banco.DaEquipe(a.Id))
        {
            db.Add(EmailRecebido.Registrar(
                Guid.NewGuid(), a.Id, NovaChave(), null, ResultadoEntradaEmail.Solicitacao, Dados.Agora, solicitacao,
                new AnexosRecusados([new AnexoRecusado("programa.exe", MotivoRecusaAnexo.TipoNaoPermitido)])));
            await db.SaveChangesAsync();
        }

        await using var leitura = banco.DaEquipe(b.Id);
        (await new EmailsRecebidos(leitura).RecusadosDaAberturaAsync(solicitacao)).Should().BeNull();
    }

    private static byte[] ComPartes(string corpo) => Encoding.UTF8.GetBytes(
        "From: Ana <ana@cliente.com.br>\r\n"
        + "To: k7m2qd9w@entrada.acordu.app.br\r\n"
        + "Subject: Forno parou\r\n"
        + "MIME-Version: 1.0\r\n"
        + corpo);

    [Fact] // F15-14 (design D14, pergunta 4): anexos e imagem embutida, decodificados, na ordem; o corpo fica de fora
    public async Task LerAsync_AnexosEImagemEmbutida_PartesNaOrdemSemOCorpo()
    {
        var mime = ComPartes(
            "Content-Type: multipart/mixed; boundary=\"m\"\r\n\r\n"
            + "--m\r\nContent-Type: multipart/related; boundary=\"r\"\r\n\r\n"
            + "--r\r\nContent-Type: multipart/alternative; boundary=\"a\"\r\n\r\n"
            + "--a\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nVeja a foto.\r\n"
            + "--a\r\nContent-Type: text/html; charset=utf-8\r\n\r\n<p>Veja <img src=\"cid:f1\"></p>\r\n"
            + "--a--\r\n"
            + "--r\r\nContent-Type: image/png\r\nContent-ID: <f1>\r\nContent-Transfer-Encoding: base64\r\n\r\n"
            + Convert.ToBase64String(CabecalhoPng) + "\r\n"
            + "--r--\r\n"
            + "--m\r\nContent-Type: application/pdf\r\nContent-Disposition: attachment; filename*=utf-8''or%C3%A7amento.pdf\r\n"
            + "Content-Transfer-Encoding: base64\r\n\r\n" + Convert.ToBase64String("%PDF-1.7"u8) + "\r\n"
            + "--m\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Disposition: attachment; filename=\"nota.txt\"\r\n\r\n"
            + "linha\r\n"
            + "--m--\r\n");
        await using var corpo = new MemoryStream(mime);

        var mensagem = await LeitorMime.LerAsync(corpo, mime.Length);

        mensagem!.Texto.Should().StartWith("Veja a foto.");
        var anexos = mensagem.Anexos!;
        anexos.Select(a => a.NomeArquivo).Should().Equal(null, "orçamento.pdf", "nota.txt");
        anexos.Select(a => a.TipoDeclarado).Should().Equal("image/png", "application/pdf", "text/plain");
        anexos[0].Inicio.ToArray().Should().Equal(CabecalhoPng);
        anexos[1].TamanhoBytes.Should().Be(8, "o base64 já desfeito");
    }

    [Fact] // design D14: as partes do e-mail cifrado vêm com o tipo dele, e a mensagem anexada com o seu
    public async Task LerAsync_CifradoEMensagemAnexada_TipoQueNuncaEntra()
    {
        var mime = ComPartes(
            "Content-Type: multipart/mixed; boundary=\"m\"\r\n\r\n"
            + "--m\r\nContent-Type: text/plain\r\n\r\noi\r\n"
            + "--m\r\nContent-Type: multipart/encrypted; protocol=\"application/pgp-encrypted\"; boundary=\"c\"\r\n\r\n"
            + "--c\r\nContent-Type: application/pgp-encrypted\r\n\r\nVersion: 1\r\n"
            + "--c\r\nContent-Type: application/octet-stream; name=\"encrypted.asc\"\r\n\r\n-----BEGIN PGP MESSAGE-----\r\n"
            + "--c--\r\n"
            + "--m\r\nContent-Type: message/rfc822\r\nContent-Disposition: attachment; filename=\"pedido.eml\"\r\n\r\n"
            + "From: b@c.com.br\r\nSubject: antigo\r\n\r\ntexto\r\n"
            + "--m--\r\n");
        await using var corpo = new MemoryStream(mime);

        var mensagem = await LeitorMime.LerAsync(corpo, mime.Length);

        mensagem!.Anexos!.Select(a => (a.NomeArquivo, a.TipoDeclarado)).Should().Equal(
            (null, "multipart/encrypted"), ("encrypted.asc", "multipart/encrypted"), ("pedido.eml", "message/rfc822"));
    }

    [Fact]
    public async Task LerAsync_SoTexto_SemAnexos()
    {
        var mime = Mime("<abc@mail.cliente.com.br>");
        await using var corpo = new MemoryStream(mime);

        (await LeitorMime.LerAsync(corpo, mime.Length))!.Anexos.Should().BeEmpty();
    }
}
