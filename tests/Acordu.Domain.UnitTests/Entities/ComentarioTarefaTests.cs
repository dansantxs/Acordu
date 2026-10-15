using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;
using static Acordu.Domain.UnitTests.Calendarios;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class ComentarioTarefaTests
{
    private const long VinteMb = 20L * 1024 * 1024;
    private static readonly Guid Tarefa = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid Autor = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly PoliticaAnexos Politica = new(VinteMb, ["image/png"], 2);

    private static NovoAnexo Png(string nome = "arte.png", long tamanho = 1024) =>
        new(nome, "image/png", tamanho, $"tenant/{Tarefa}/{Guid.NewGuid()}");

    private static ComentarioTarefa Registrar(string? texto = "Ajustei a margem.", IReadOnlyList<NovoAnexo>? anexos = null) =>
        ComentarioTarefa.Registrar(Guid.NewGuid(), Calendarios.Tenant, Tarefa, Autor, texto, anexos ?? [], Politica, Brasilia(6, 11));

    [Fact]
    public void Registrar_GuardaTextoAutorEInstante()
    {
        var comentario = Registrar("  Ajustei a margem.  ");

        comentario.Texto.Should().Be("Ajustei a margem.");
        comentario.TarefaId.Should().Be(Tarefa);
        comentario.AutorId.Should().Be(Autor);
        comentario.RegistradoEm.Should().Be(Brasilia(6, 11));
    }

    [Fact]
    public void Registrar_SoComAnexo_Aceita()
    {
        var comentario = Registrar(null, [Png()]);

        comentario.Texto.Should().BeNull();
        comentario.Anexos.Should().HaveCount(1);
        comentario.Anexos[0].TipoConteudo.Should().Be("image/png");
    }

    [Fact]
    public void Registrar_SemTextoNemAnexo_LancaExcecao()
    {
        var acao = () => Registrar("   ");

        acao.Should().Throw<ArgumentException>().WithParameterName("texto");
    }

    [Fact]
    public void Registrar_AnexosDemais_LancaAnexoInvalido()
    {
        var acao = () => Registrar("x", [Png("a.png"), Png("b.png"), Png("c.png")]);

        acao.Should().Throw<AnexoInvalidoException>();
    }

    [Fact]
    public void Registrar_TipoNaoPermitido_LancaAnexoInvalido()
    {
        var acao = () => Registrar("x", [new NovoAnexo("a.exe", "application/x-msdownload", 10, "chave")]);

        acao.Should().Throw<AnexoInvalidoException>();
    }

    [Fact]
    public void Registrar_ComMencao_EmitePessoasMencionadas()
    {
        var ana = Guid.NewGuid();

        var comentario = Registrar($"@{{{ana}}} conferi a arte");

        comentario.Eventos.Should().HaveCount(1);
        var mencao = (PessoasMencionadas)comentario.Eventos[0];
        mencao.Alvo.Should().Be(AlvoColaboracao.Tarefa);
        mencao.AlvoId.Should().Be(Tarefa);
        mencao.Mencionados.Should().Equal(ana);
    }

    [Fact]
    public void Registrar_SemMencao_SemEvento()
    {
        Registrar().Eventos.Should().BeEmpty();
    }
}
