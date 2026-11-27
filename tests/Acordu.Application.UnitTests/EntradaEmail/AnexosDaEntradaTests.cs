using Acordu.Application.Anexos;
using Acordu.Application.EntradaEmail;
using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.UnitTests.EntradaEmail;

/// <summary>
/// Design do F15, D14 (RNF-SEG-07; F15-14, F15-15): os anexos do e-mail pela política do portal, um por um, com o motivo de
/// cada recusado, e os textos da nota do sistema e do aviso de recebida.
/// </summary>
public sealed class AnexosDaEntradaTests
{
    private static readonly PoliticaAnexos Politica = RecebimentoAnexos.Politica;
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    private static readonly byte[] Executavel = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00];

    private static AnexoDeEntrada Anexo(string? nome, byte[] conteudo, string? tipo = "application/octet-stream") =>
        new(nome, tipo, conteudo);

    [Fact]
    public void Separar_SemAnexos_NadaValidoNemRecusado()
    {
        var separacao = AnexosDaEntrada.Separar(null, Politica);

        separacao.Validos.Should().BeEmpty();
        separacao.Recusados.Should().BeNull();
        AnexosDaEntrada.Separar([], Politica).Recusados.Should().BeNull();
    }

    [Fact] // F15-14: o tipo vem do conteúdo, não do que a parte declara
    public void Separar_DentroDaPolitica_TodosValidosNaOrdemComONome()
    {
        var separacao = AnexosDaEntrada.Separar(
            [Anexo("foto.png", Png, "application/octet-stream"), Anexo("nota.txt", "Olá, segue."u8.ToArray(), "text/plain")],
            Politica);

        separacao.Validos.Select(v => (v.NomeArquivo, v.TamanhoBytes)).Should().Equal(("foto.png", Png.Length), ("nota.txt", 12L));
        separacao.Recusados.Should().BeNull();
    }

    [Fact] // F15-14, pergunta 4: a imagem embutida sem nome entra como anexo, com um nome e a extensão do tipo
    public void Separar_ImagemEmbutidaSemNome_EntraComoAnexoN()
    {
        var separacao = AnexosDaEntrada.Separar([Anexo("logo.png", Png, "image/png"), Anexo(null, Png, "image/png")], Politica);

        separacao.Validos.Select(v => v.NomeArquivo).Should().Equal("logo.png", "anexo-2.png");
    }

    [Fact] // F15-15: tipo, tamanho e quantidade, cada um com o seu motivo, na ordem do MIME
    public void Separar_ForaDaPolitica_ValidosAteO5oERecusadosComOMotivo()
    {
        var grande = new byte[Politica.TamanhoMaximoBytes + 1];
        Png.CopyTo(grande, 0);
        IReadOnlyList<AnexoDeEntrada> anexos =
        [
            Anexo("1.png", Png),
            Anexo("programa.exe", Executavel),
            Anexo("2.png", Png),
            Anexo("enorme.png", grande),
            Anexo("3.png", Png),
            Anexo("4.png", Png),
            Anexo("5.png", Png),
            Anexo("6.png", Png),
        ];

        var separacao = AnexosDaEntrada.Separar(anexos, Politica);

        separacao.Validos.Select(v => v.NomeArquivo).Should().Equal("1.png", "2.png", "3.png", "4.png", "5.png");
        separacao.Recusados!.Itens.Should().Equal(
            new AnexoRecusado("programa.exe", MotivoRecusaAnexo.TipoNaoPermitido),
            new AnexoRecusado("enorme.png", MotivoRecusaAnexo.GrandeDemais),
            new AnexoRecusado("6.png", MotivoRecusaAnexo.AlemDoLimite));
    }

    [Fact] // no limite do tamanho ainda entra, como no portal
    public void Separar_NoLimiteDoTamanho_Valido()
    {
        var limite = new byte[Politica.TamanhoMaximoBytes];
        Png.CopyTo(limite, 0);

        AnexosDaEntrada.Separar([Anexo("limite.png", limite)], Politica).Validos.Should().ContainSingle();
    }

    [Fact]
    public void Separar_Vazio_TipoNaoPermitido()
    {
        AnexosDaEntrada.Separar([Anexo("vazio.pdf", [])], Politica).Recusados!.Itens
            .Should().ContainSingle().Which.Motivo.Should().Be(MotivoRecusaAnexo.TipoNaoPermitido);
    }

    [Theory] // design D14: mensagem anexada e partes cifradas ou assinadas nunca entram, mesmo com conteúdo em texto
    [InlineData("message/rfc822")]
    [InlineData("Message/RFC822")]
    [InlineData("multipart/encrypted")]
    [InlineData("application/pgp-signature")]
    [InlineData("application/pgp-encrypted")]
    [InlineData("application/pkcs7-signature")]
    [InlineData("application/x-pkcs7-mime")]
    public void Separar_TipoDeclaradoQueNuncaEntra_TipoNaoPermitido(string tipo)
    {
        var separacao = AnexosDaEntrada.Separar([Anexo("parte.txt", "texto legível"u8.ToArray(), tipo)], Politica);

        separacao.Validos.Should().BeEmpty();
        separacao.Recusados!.Itens.Should().ContainSingle().Which.Motivo.Should().Be(MotivoRecusaAnexo.TipoNaoPermitido);
    }

    [Fact] // o recusado não ocupa vaga: o 5º válido entra mesmo depois de recusados
    public void Separar_RecusadosAntes_NaoContamNoLimite()
    {
        IReadOnlyList<AnexoDeEntrada> anexos =
        [
            Anexo("a.exe", Executavel), Anexo("b.exe", Executavel),
            .. Enumerable.Range(1, 5).Select(n => Anexo($"{n}.png", Png)),
        ];

        var separacao = AnexosDaEntrada.Separar(anexos, Politica);

        separacao.Validos.Should().HaveCount(5);
        separacao.Recusados!.Itens.Should().OnlyContain(r => r.Motivo == MotivoRecusaAnexo.TipoNaoPermitido);
    }

    [Theory] // o nome nunca carrega caminho nem caractere de controle (Anexo.Criar recusaria e o e-mail inteiro falharia)
    [InlineData("C:\\Users\\ana\\foto.png", "foto.png")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("  relatorio\t.pdf  ", "relatorio.pdf")]
    [InlineData("pasta/", "anexo-3")]
    [InlineData("..", "anexo-3")]
    [InlineData(null, "anexo-3")]
    public void NomeDoArquivo_ComCaminhoOuVazio_SoONomeOuAnexoN(string? nome, string esperado)
    {
        AnexosDaEntrada.NomeDoArquivo(nome, 3, "application/octet-stream").Should().Be(esperado);
    }

    [Theory]
    [InlineData("image/jpeg", "anexo-1.jpg")]
    [InlineData("image/png; name=x", "anexo-1.png")]
    [InlineData("message/rfc822", "anexo-1.eml")]
    [InlineData(null, "anexo-1")]
    public void NomeDoArquivo_SemNome_ExtensaoDoTipoDeclarado(string? tipo, string esperado)
    {
        AnexosDaEntrada.NomeDoArquivo(null, 1, tipo).Should().Be(esperado);
    }

    [Fact]
    public void NomeDoArquivo_MaiorQue255_CortaMantendoAExtensao()
    {
        var nome = AnexosDaEntrada.NomeDoArquivo(new string('a', 300) + ".pdf", 1, null);

        nome.Should().HaveLength(255).And.EndWith("a.pdf");
    }

    [Fact] // F15-15: a nota do sistema lista cada recusado com o motivo
    public void TextoDaNota_Recusados_ListaComOMotivo()
    {
        var recusados = new AnexosRecusados(
        [
            new AnexoRecusado("programa.exe", MotivoRecusaAnexo.TipoNaoPermitido),
            new AnexoRecusado("video.mp4", MotivoRecusaAnexo.GrandeDemais),
            new AnexoRecusado("6.png", MotivoRecusaAnexo.AlemDoLimite),
        ]);

        AnexosDaEntrada.TextoDaNota(recusados, Politica).Should().Be(
            "Anexos do e-mail que não entraram na solicitação:\n"
            + "- programa.exe: tipo de arquivo não permitido\n"
            + "- video.mp4: maior que 20 MB\n"
            + "- 6.png: além do limite de 5 anexos por e-mail");
    }

    [Fact] // F15-15: o aviso diz ao contato quais arquivos não entraram
    public void TextoDoAviso_Recusados_ListaParaOContato()
    {
        var recusados = new AnexosRecusados([new AnexoRecusado("programa.exe", MotivoRecusaAnexo.TipoNaoPermitido)]);

        AnexosDaEntrada.TextoDoAviso(recusados, Politica).Should().Be(
            "Estes arquivos do seu e-mail não entraram na solicitação:\n"
            + "- programa.exe (tipo de arquivo não permitido)\n"
            + "Se precisar deles, envie em outro formato ou por um link.");
    }

    [Fact] // milhares de partes não estouram o texto da interação
    public void TextoDaNota_MaisQue20_ListaOs20EContaORestante()
    {
        var recusados = new AnexosRecusados(
            Enumerable.Range(1, 23).Select(n => new AnexoRecusado($"{n}.exe", MotivoRecusaAnexo.TipoNaoPermitido)));

        var texto = AnexosDaEntrada.TextoDaNota(recusados, Politica);

        texto.Should().Contain("- 20.exe").And.NotContain("- 21.exe").And.EndWith("- e mais 3 arquivos");
    }

    private static AnexosRecusados Recusados(int quantos) =>
        new(Enumerable.Range(1, quantos).Select(n => new AnexoRecusado($"{n}.exe", MotivoRecusaAnexo.TipoNaoPermitido)));

    [Fact] // F15-15: o aviso ao contato tem o mesmo teto de 20 linhas e a contagem do resto
    public void TextoDoAviso_MaisQue20_ListaOs20EContaORestante()
    {
        var texto = AnexosDaEntrada.TextoDoAviso(Recusados(23), Politica);

        texto.Should().Contain("- 20.exe (tipo de arquivo não permitido)").And.NotContain("- 21.exe")
            .And.Contain("- e mais 3 arquivos")
            .And.EndWith("Se precisar deles, envie em outro formato ou por um link.");
    }

    [Fact] // no teto exato, não há "e mais"
    public void TextoDaNotaEDoAviso_Exatamente20_SemContagemDoRestante()
    {
        AnexosDaEntrada.TextoDaNota(Recusados(20), Politica).Should().Contain("- 20.exe").And.NotContain("e mais");
        AnexosDaEntrada.TextoDoAviso(Recusados(20), Politica).Should().Contain("- 20.exe").And.NotContain("e mais");
    }

    [Fact] // um a mais no singular
    public void TextoDaNota_21_RestanteNoSingular()
    {
        AnexosDaEntrada.TextoDaNota(Recusados(21), Politica).Should().EndWith("- e mais 1 arquivo");
    }

    [Fact] // a nota cabe no texto da interação mesmo com nomes de 255 caracteres e milhares de recusados
    public void TextoDaNota_MilharesComNomesLongos_CabeNoLimiteDoTexto()
    {
        var recusados = new AnexosRecusados(Enumerable.Range(1, 3000)
            .Select(_ => new AnexoRecusado(new string('n', 255), MotivoRecusaAnexo.AlemDoLimite)));

        AnexosDaEntrada.TextoDaNota(recusados, Politica).Length.Should().BeLessThanOrEqualTo(LimitesTexto.TextoInteracao);
        AnexosDaEntrada.TextoDoAviso(recusados, Politica).Should().Contain("- e mais 2980 arquivos");
    }

    [Fact] // a detecção é pelo conteúdo: a extensão de imagem não salva um executável
    public void Separar_ExecutavelComNomeDeImagem_TipoNaoPermitido()
    {
        var separacao = AnexosDaEntrada.Separar([Anexo("foto.png", Executavel, "image/png")], Politica);

        separacao.Validos.Should().BeEmpty();
        separacao.Recusados!.Itens.Should().ContainSingle()
            .Which.Should().Be(new AnexoRecusado("foto.png", MotivoRecusaAnexo.TipoNaoPermitido));
    }

    [Fact] // F15-14: exatamente 5 válidos entram, sem nenhum recusado
    public void Separar_ExatamenteCinco_TodosValidosSemRecusados()
    {
        var separacao = AnexosDaEntrada.Separar([.. Enumerable.Range(1, 5).Select(n => Anexo($"{n}.png", Png))], Politica);

        separacao.Validos.Should().HaveCount(5);
        separacao.Recusados.Should().BeNull();
    }
}
