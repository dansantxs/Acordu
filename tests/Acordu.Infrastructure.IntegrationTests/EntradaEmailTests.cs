using System.Security.Cryptography;
using System.Text;
using Acordu.Application.Common;
using Acordu.Application.EntradaEmail;
using Acordu.Domain.Entities;
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

    [Fact]
    public async Task LerAsync_CorpoVazio_Nulo() // plano-2, menor 6: ilegível não vira erro
    {
        await using var corpo = new MemoryStream([]);

        (await LeitorMime.LerAsync(corpo, 1024)).Should().BeNull();
    }
}
