using System.Text.Json;
using System.Text.RegularExpressions;
using Acordu.Application.Exemplos;
using Acordu.Domain.Enums;

namespace Acordu.Application.UnitTests.Exemplos;

/// <summary>
/// O conjunto único de dados fictícios (design do F14, D13; F14-92): o arquivo embutido lê inteiro, as referências por
/// chave fecham, e nada nele é de pessoa, empresa ou documento real.
/// </summary>
public sealed partial class DadosFicticiosTests
{
    private static readonly DadosFicticios Dados = DadosFicticios.Arquivo;

    [Fact]
    public void Arquivo_Embutido_TrazOQueOsExemplosEAManualUsam()
    {
        Dados.Versao.Should().Be(1);
        Dados.Pessoas.Should().ContainSingle(p => p.Papel == "Dono");
        Dados.Pessoas.Should().OnlyContain(p => p.Papel == "Dono" || p.Papel == "Membro");
        Dados.Clientes.Should().NotBeEmpty();
        Dados.Solicitacoes.Should().NotBeEmpty();
        Dados.Tarefas.Should().NotBeEmpty();
        Dados.Apontamentos.Should().NotBeEmpty();
        Dados.Clientes.Should().OnlyContain(c => c.Contato != null && c.Contrato != null, "cada cliente vem com contato e contrato");
    }

    [Fact]
    public void Arquivo_Chaves_SemRepeticao()
    {
        Unicas(Dados.Pessoas.Select(p => p.Chave));
        Unicas(Dados.Categorias.Select(c => c.Chave));
        Unicas(Dados.Clientes.Select(c => c.Chave));
        Unicas(Dados.Solicitacoes.Select(s => s.Chave));
        Unicas(Dados.Projetos.Select(p => p.Chave));
        Unicas(Dados.Tarefas.Select(t => t.Chave));
    }

    [Fact]
    public void Arquivo_Referencias_ApontamParaChavesQueExistem()
    {
        var pessoas = Dados.Pessoas.Select(p => p.Chave).ToHashSet(StringComparer.Ordinal);
        var clientes = Dados.Clientes.Select(c => c.Chave).ToHashSet(StringComparer.Ordinal);
        var categorias = Dados.Categorias.Select(c => c.Chave).ToHashSet(StringComparer.Ordinal);
        var solicitacoes = Dados.Solicitacoes.Select(s => s.Chave).ToHashSet(StringComparer.Ordinal);
        var projetos = Dados.Projetos.Select(p => p.Chave).ToHashSet(StringComparer.Ordinal);
        var tarefas = Dados.Tarefas.Select(t => t.Chave).ToHashSet(StringComparer.Ordinal);

        Dados.Clientes.Should().OnlyContain(c => c.Responsavel == null || pessoas.Contains(c.Responsavel));
        Dados.Solicitacoes.Should().OnlyContain(s => clientes.Contains(s.Cliente)
            && (s.Responsavel == null || pessoas.Contains(s.Responsavel))
            && (s.Categoria == null || categorias.Contains(s.Categoria)));
        Dados.Solicitacoes.SelectMany(s => s.Passos ?? []).Should().OnlyContain(p => p.Por == null || pessoas.Contains(p.Por));
        Dados.Projetos.Should().OnlyContain(p => clientes.Contains(p.Cliente) && p.Inicio <= p.Prazo);
        Dados.Tarefas.Should().OnlyContain(t => (t.Responsavel == null || pessoas.Contains(t.Responsavel))
            && (t.Solicitacao != null ? solicitacoes.Contains(t.Solicitacao)
                : t.Projeto != null ? projetos.Contains(t.Projeto)
                : t.Cliente != null && clientes.Contains(t.Cliente)));
        Dados.Tarefas.Should().OnlyContain(
            t => new[] { t.Solicitacao, t.Projeto, t.Cliente }.Count(o => o != null) == 1, "a tarefa tem uma origem só");
        Dados.Apontamentos.Should().OnlyContain(a => pessoas.Contains(a.Por) && tarefas.Contains(a.Tarefa));
        Dados.Recorrencias.Should().OnlyContain(r => clientes.Contains(r.Cliente) && (r.Responsavel == null || pessoas.Contains(r.Responsavel)));
        (Dados.Cronometro is not { } cronometro || (pessoas.Contains(cronometro.Por) && tarefas.Contains(cronometro.Tarefa)))
            .Should().BeTrue("o cronômetro aponta para pessoa e tarefa dos dados");
        (Dados.Portal is not { } portal || clientes.Contains(portal.Cliente)).Should().BeTrue("o portal é de um cliente dos dados");
    }

    [Fact]
    public void Arquivo_Passos_SoAsAcoesConhecidasComOQueCadaUmaPede()
    {
        var passos = Dados.Solicitacoes.SelectMany(s => s.Passos ?? []).ToList();

        passos.Should().OnlyContain(p => p.Acao == PassoFicticio.Inicio || p.Acao == PassoFicticio.Interacao
            || p.Acao == PassoFicticio.AguardandoCliente || p.Acao == PassoFicticio.Resolucao);
        passos.Where(p => p.Acao == PassoFicticio.Interacao)
            .Should().OnlyContain(p => p.Visibilidade != null && !string.IsNullOrWhiteSpace(p.Texto));
        passos.Where(p => p.Acao == PassoFicticio.Resolucao).Should().OnlyContain(p => p.Solucao != null);
    }

    [Fact]
    public void Arquivo_Mencoes_SoDePessoasDosDados()
    {
        var pessoas = Dados.Pessoas.Select(p => p.Chave).ToHashSet(StringComparer.Ordinal);
        var mencoes = Dados.Solicitacoes.SelectMany(s => s.Passos ?? [])
            .SelectMany(p => Mencao().Matches(p.Texto ?? "").Select(m => m.Groups[1].Value))
            .ToList();

        mencoes.Should().NotBeEmpty().And.OnlyContain(m => pessoas.Contains(m));
    }

    [Fact]
    public void Arquivo_DatasEHoras_DentroDoQueAApiAceita()
    {
        Dados.Apontamentos.Should().OnlyContain(a => a.Dia <= 0, "hora não vai para o futuro");
        Dados.Apontamentos.Should().OnlyContain(a => a.Minutos > 0 && a.Minutos <= 24 * 60);
        Dados.Apontamentos.Should().OnlyContain(
            a => a.Limite == null || a.Limite == ApontamentoFicticio.Semana || a.Limite == ApontamentoFicticio.Mes);
        Dados.Tarefas.Should().OnlyContain(t => t.Status == StatusTarefa.AFazer || t.Status == StatusTarefa.Fazendo || t.Status == StatusTarefa.Concluida);
        Dados.Clientes.Should().OnlyContain(c => c.Contrato.InicioMeses <= 0, "a vigência começa antes das horas apontadas");
    }

    [Fact]
    public void Arquivo_SoDadoFicticio_SemDocumentoEComEmailsQueNaoEntregam()
    {
        var emails = Dados.Pessoas.Select(p => p.Email)
            .Concat(Dados.Clientes.Select(c => c.Contato.Email))
            .Concat(Dados.Clientes.Where(c => c.Email != null).Select(c => c.Email!))
            .ToList();

        // .invalid é reservado (RFC 2606): nenhum e-mail dos dados fictícios chega a alguém.
        emails.Should().OnlyContain(e => e.EndsWith(".invalid", StringComparison.Ordinal));
        using var json = JsonDocument.Parse(BrutoDoArquivo());
        Chaves(json.RootElement).Should().NotContain(
            c => c.Equals("documento", StringComparison.OrdinalIgnoreCase) || c.Equals("cpf", StringComparison.OrdinalIgnoreCase)
                || c.Equals("cnpj", StringComparison.OrdinalIgnoreCase),
            "nenhum CPF nem CNPJ, real ou não");
    }

    [Fact]
    public void Ler_JsonComEnumDesconhecido_Falha()
    {
        using var json = new MemoryStream("""{"versao":1,"clientes":[{"chave":"x","nome":"X","contato":{"nome":"A","email":"a@x.invalid"},"contrato":{"tipo":"VITALICIO","inicioMeses":0}}]}"""u8.ToArray());

        var ler = () => DadosFicticios.Ler(json);

        ler.Should().Throw<JsonException>();
    }

    private static void Unicas(IEnumerable<string> chaves) =>
        chaves.Should().OnlyHaveUniqueItems().And.OnlyContain(c => !string.IsNullOrWhiteSpace(c));

    private static string BrutoDoArquivo()
    {
        using var recurso = typeof(DadosFicticios).Assembly.GetManifestResourceStream(DadosFicticios.Recurso)!;
        using var leitor = new StreamReader(recurso);
        return leitor.ReadToEnd();
    }

    private static IEnumerable<string> Chaves(JsonElement elemento) => elemento.ValueKind switch
    {
        JsonValueKind.Object => elemento.EnumerateObject().SelectMany(p => Chaves(p.Value).Prepend(p.Name)),
        JsonValueKind.Array => elemento.EnumerateArray().SelectMany(Chaves),
        _ => [],
    };

    [GeneratedRegex(@"@\{([a-zA-Z]+)\}")]
    private static partial Regex Mencao();
}
