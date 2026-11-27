using System.Security.Cryptography;
using System.Text;
using Acordu.Application.Common;
using Acordu.Application.EntradaEmail;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using Acordu.Infrastructure.Email.Entrada;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence.Cadastros;
using Acordu.Infrastructure.Persistence.EntradaEmail;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>
/// Spec do F15, F15d (F15-05, F15-19, F15-22, F15-89; design D6, D12 e D14; ADR 0016, itens 3 e 8): a consulta global do
/// código de entrada (ponto revisado), o registro de entrada por tenant com a chave única e a leitura do MIME. F15h
/// (F15-70; design D10): o item da triagem gravado e relido no tenant, só da equipe.
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

    [Fact] // F15i (design D6): a resposta na conversa aponta a solicitação, mas os recusados lidos pelo aviso são só os da abertura
    public async Task RecusadosDaAberturaAsync_SoARespostaNaConversa_Nulo()
    {
        var a = await EmpresaAsync();
        var solicitacao = Guid.NewGuid();
        await using (var db = banco.DaEquipe(a.Id))
        {
            db.Add(EmailRecebido.Registrar(
                Guid.NewGuid(), a.Id, NovaChave(), null, ResultadoEntradaEmail.Mensagem, Dados.Agora, solicitacao));
            await db.SaveChangesAsync();
        }

        await using var leitura = banco.DaEquipe(a.Id);
        (await leitura.EmailsRecebidos.AsNoTracking().SingleAsync()).SolicitacaoId.Should().Be(solicitacao);
        (await new EmailsRecebidos(leitura).RecusadosDaAberturaAsync(solicitacao)).Should().BeNull();
    }

    [Fact] // F15-40 (design D3): o token é procurado só na empresa do contexto; o de outra empresa não é achado
    public async Task ConversasPorEmail_PorToken_SoNaEmpresaDoContexto()
    {
        var a = await EmpresaAsync();
        var b = await EmpresaAsync();
        var cliente = Dados.Cliente(a.Id);
        var solicitacao = Dados.Solicitacao(a.Id, cliente.Id);
        var conversa = Dados.Conversa(a.Id, cliente.Id, solicitacao.Id);
        await using (var db = banco.DaEquipe(a.Id))
        {
            db.AddRange(cliente, solicitacao, conversa);
            await db.SaveChangesAsync();
        }

        await using var emA = banco.DaEquipe(a.Id);
        (await new ConversasPorEmail(emA).PorTokenAsync(conversa.Token))!.SolicitacaoId.Should().Be(solicitacao.Id);
        (await new ConversasPorEmail(emA).PorTokenAsync(ConversaPorEmail.SortearToken())).Should().BeNull();
        await using var emB = banco.DaEquipe(b.Id);
        (await new ConversasPorEmail(emB).PorTokenAsync(conversa.Token)).Should().BeNull("o token de A não vale em B");
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

    [Fact] // F15-70: o nome do remetente vem do From com um endereço só
    public async Task LerAsync_FromComNome_NomeDoRemetenteDecodificado()
    {
        var mime = Mime("<a@b>", "=?utf-8?Q?Jo=C3=A3o_Pr=C3=A1do?= <joao@cliente.com.br>");
        await using var corpo = new MemoryStream(mime);

        var mensagem = await LeitorMime.LerAsync(corpo, mime.Length);

        mensagem!.NomeDoRemetente.Should().Be("João Prádo");
    }

    [Fact] // F15-70: com vários endereços no From, nenhum nome
    public async Task LerAsync_VariosRemetentes_SemNome()
    {
        var mime = Mime(null, "Ana <ana@cliente.com.br>, Bruno <bruno@cliente.com.br>");
        await using var corpo = new MemoryStream(mime);

        (await LeitorMime.LerAsync(corpo, mime.Length))!.NomeDoRemetente.Should().BeNull();
    }

    [Fact] // F15-70, F15-24 (design D10): o item com os anexos, os clientes possíveis e os recusados, relido no tenant
    public async Task ItemTriagem_GravadoERelidoComAnexosClientesERecusados()
    {
        var tenant = await EmpresaAsync();
        var clientes = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var item = Dados.ItemTriagem(tenant.Id, MotivoTriagem.MaisDeUmCliente, clientes);
        await using (var db = banco.DaEquipe(tenant.Id))
        {
            db.Add(item);
            db.Add(EmailRecebido.Registrar(
                Guid.NewGuid(), tenant.Id, NovaChave(), null, ResultadoEntradaEmail.Triagem, Dados.Agora, itemTriagemId: item.Id));
            await db.SaveChangesAsync();
        }

        await using var leitura = banco.DaEquipe(tenant.Id);
        var lido = await leitura.ItensTriagemEmail.AsNoTracking().SingleAsync();
        lido.RemetenteEmail.Should().Be("pedro@outro.com.br");
        lido.RemetenteNome.Should().Be("Pedro");
        lido.Assunto.Should().Be("Orçamento do forno");
        lido.Texto.Should().Be("Quanto custa?");
        lido.Motivo.Should().Be(MotivoTriagem.MaisDeUmCliente);
        lido.ClientesPossiveis.Should().Equal(clientes);
        lido.RecebidoEm.Should().Be(Dados.Agora);
        lido.Anexos.Should().ContainSingle().Which.ChaveArmazenamento.Should().StartWith($"{tenant.Id}/triagem/{item.Id}/");
        lido.Recusados.Should().Be(item.Recusados);
        (await leitura.EmailsRecebidos.AsNoTracking().SingleAsync()).ItemTriagemId.Should().Be(item.Id);
    }

    [Theory]
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task ItemTriagem_EmpresaSoLeitura_GuardaRecusa(string situacao) // ADR 0015: a triagem é dado da empresa
    {
        var tenant = await EmpresaAsync(situacao: situacao);
        await using var db = banco.DaEquipe(tenant.Id);
        db.Add(Dados.ItemTriagem(tenant.Id));

        var gravar = () => db.SaveChangesAsync();

        await gravar.Should().ThrowAsync<EmpresaSomenteLeituraException>();
    }

    [Fact] // ADR 0002: o item não pertence a cliente nenhum; o contexto do contato não o grava
    public async Task ItemTriagem_NoContextoDoContato_GravacaoRecusada()
    {
        var tenant = await EmpresaAsync();
        await using var db = banco.DoCliente(tenant.Id, Guid.NewGuid());
        db.Add(Dados.ItemTriagem(tenant.Id));

        var gravar = () => db.SaveChangesAsync();

        await gravar.Should().ThrowAsync<IsolamentoVioladoException>();
    }

    [Fact] // design §1.2 ("O ApagadorDeEmpresa apaga os anexos da triagem"): o expurgo da empresa leva os arquivos da triagem
    public async Task ApagarParaRestaurar_ComItemNaTriagem_DevolveAChaveDoAnexoEApagaOItem()
    {
        var tenant = await EmpresaAsync();
        var item = Dados.ItemTriagem(tenant.Id);
        await using (var db = banco.DaEquipe(tenant.Id))
        {
            db.Add(item);
            await db.SaveChangesAsync();
        }

        IReadOnlyList<string> chaves;
        await using (var db = banco.DaEquipe(tenant.Id))
            chaves = await ApagadorDeEmpresa.ApagarParaRestaurarAsync(db, tenant, CancellationToken.None);

        chaves.Should().Contain(item.Anexos[0].ChaveArmazenamento);
        await using var leitura = banco.DaEquipe(tenant.Id);
        (await leitura.ItensTriagemEmail.AnyAsync()).Should().BeFalse();
        (await leitura.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM anexos_triagem_email WHERE item_triagem_id = {item.Id}").SingleAsync())
            .Should().Be(0, "os anexos saem em cascata com o item");
    }

    [Fact] // F15-70: From ausente e e-mail sem conteúdo (colunas nulas) e sem anexos nem recusados: gravado e relido
    public async Task ItemTriagem_SemRemetenteNemConteudo_RelidoComNulosEListaVazia()
    {
        var tenant = await EmpresaAsync();
        var id = Guid.NewGuid();
        await using (var db = banco.DaEquipe(tenant.Id))
        {
            db.Add(ItemTriagemEmail.Receber(
                id, tenant.Id, null, null, null, null, MotivoTriagem.Desconhecido, [], [], null,
                new PoliticaAnexos(20L * 1024 * 1024, ["image/png"], 5), Dados.Agora));
            await db.SaveChangesAsync();
        }

        await using var leitura = banco.DaEquipe(tenant.Id);
        var lido = await leitura.ItensTriagemEmail.AsNoTracking().SingleAsync(i => i.Id == id);
        lido.RemetenteEmail.Should().BeNull();
        lido.RemetenteNome.Should().BeNull();
        lido.Assunto.Should().BeNull();
        lido.Texto.Should().BeNull();
        lido.ClientesPossiveis.Should().BeEmpty();
        lido.Anexos.Should().BeEmpty();
        lido.Recusados.Should().BeNull();
    }

    [Fact] // F15-70: nos tetos (e-mail 254, nome 200, assunto 998, texto 10.000) as colunas comportam o que o domínio aceita
    public async Task ItemTriagem_NosTetosDeTamanho_GravadoSemTruncarNemFalhar()
    {
        var tenant = await EmpresaAsync();
        var id = Guid.NewGuid();
        var email = $"{new string('a', 64)}@{new string('b', 60)}.{new string('c', 60)}.{new string('d', 60)}.com.br";
        await using (var db = banco.DaEquipe(tenant.Id))
        {
            db.Add(ItemTriagemEmail.Receber(
                id, tenant.Id, email, new string('n', 200), new string('a', 998), new string('t', 10_000),
                MotivoTriagem.Desconhecido, [], [], null, new PoliticaAnexos(20L * 1024 * 1024, ["image/png"], 5), Dados.Agora));
            await db.SaveChangesAsync();
        }

        await using var leitura = banco.DaEquipe(tenant.Id);
        var lido = await leitura.ItensTriagemEmail.AsNoTracking().SingleAsync(i => i.Id == id);
        lido.RemetenteEmail.Should().Be(email);
        lido.RemetenteNome.Should().HaveLength(200);
        lido.Assunto.Should().HaveLength(998);
        lido.Texto.Should().HaveLength(10_000);
    }

    [Fact] // ADR 0002: o item de uma empresa não aparece na outra, nem pela consulta, nem pelo Id
    public async Task ItemTriagem_DeOutraEmpresa_NaoApareceNemPeloId()
    {
        var a = await EmpresaAsync();
        var b = await EmpresaAsync();
        var item = Dados.ItemTriagem(a.Id);
        await using (var db = banco.DaEquipe(a.Id))
        {
            db.Add(item);
            await db.SaveChangesAsync();
        }

        await using var daOutra = banco.DaEquipe(b.Id);

        (await daOutra.ItensTriagemEmail.AnyAsync()).Should().BeFalse();
        (await daOutra.ItensTriagemEmail.AnyAsync(i => i.Id == item.Id)).Should().BeFalse();
    }

    [Fact] // a restauração/expurgo de uma empresa não leva o item nem o arquivo de outra
    public async Task ApagarParaRestaurar_ItemDeOutraEmpresa_FicaEAChaveNaoSai()
    {
        var apagada = await EmpresaAsync();
        var outra = await EmpresaAsync();
        var itemApagado = Dados.ItemTriagem(apagada.Id);
        var itemDaOutra = Dados.ItemTriagem(outra.Id);
        foreach (var (tenant, item) in new[] { (apagada, itemApagado), (outra, itemDaOutra) })
        {
            await using var db = banco.DaEquipe(tenant.Id);
            db.Add(item);
            await db.SaveChangesAsync();
        }

        IReadOnlyList<string> chaves;
        await using (var db = banco.DaEquipe(apagada.Id))
            chaves = await ApagadorDeEmpresa.ApagarParaRestaurarAsync(db, apagada, CancellationToken.None);

        chaves.Should().Contain(itemApagado.Anexos[0].ChaveArmazenamento).And.NotContain(itemDaOutra.Anexos[0].ChaveArmazenamento);
        await using var leitura = banco.DaEquipe(outra.Id);
        (await leitura.ItensTriagemEmail.SingleAsync()).Id.Should().Be(itemDaOutra.Id);
        (await leitura.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM anexos_triagem_email WHERE item_triagem_id = {itemDaOutra.Id}").SingleAsync())
            .Should().Be(1, "os anexos da outra empresa ficam");
    }
}
