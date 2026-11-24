using System.Text.Json;
using System.Text.Json.Serialization;
using Acordu.Application.Clientes;
using Acordu.Domain.Enums;

namespace Acordu.Application.Exemplos;

/// <summary>
/// O conjunto único de dados fictícios do Acordu (design do F14, D13; convenções §4): <c>demonstracao.json</c>, embutido
/// neste assembly. A API lê daqui os dados de exemplo (RF-EMP-05, F14r) e, no F14s, a empresa de demonstração; o bot do
/// manual (<c>apps/web/ajuda/demonstracao.ts</c>) lê o mesmo arquivo e cria pela API. Tudo é inventado: nenhuma pessoa,
/// empresa ou documento real, e-mails em domínios <c>.invalid</c>, sem CPF nem CNPJ.
/// <para>
/// Pessoas, clientes, solicitações, projetos e tarefas têm uma <c>chave</c> e se referem uns aos outros por ela. Datas
/// são relativas ao dia em que os dados são criados (<see cref="DiasRelativos"/>): <c>dia</c>, <c>inicio</c> e
/// <c>prazo</c> contam dias úteis a partir de hoje; <c>inicioMeses</c>, meses a partir do primeiro dia do mês corrente.
/// Num texto, <c>@{chave}</c> menciona a pessoa da chave.
/// </para>
/// </summary>
public sealed record DadosFicticios(
    int Versao,
    EmpresaFicticia Empresa,
    IReadOnlyList<PessoaFicticia> Pessoas,
    IReadOnlyList<CategoriaFicticia> Categorias,
    IReadOnlyList<ClienteFicticio> Clientes,
    IReadOnlyList<SolicitacaoFicticia> Solicitacoes,
    IReadOnlyList<ProjetoFicticio> Projetos,
    IReadOnlyList<TarefaFicticia> Tarefas,
    IReadOnlyList<ApontamentoFicticio> Apontamentos,
    IReadOnlyList<RecorrenciaFicticia> Recorrencias,
    CronometroFicticio? Cronometro,
    PortalFicticio? Portal)
{
    /// <summary>Nome do recurso embutido (o <c>LogicalName</c> do <c>.csproj</c>).</summary>
    public const string Recurso = "Acordu.Application.Exemplos.demonstracao.json";

    /// <summary>Enums como no contrato da API (<c>BANCO_HORAS</c>, <c>EXTERNA</c>), campos em camelCase.</summary>
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false) },
    };

    private static readonly Lazy<DadosFicticios> DoRecurso = new(() =>
    {
        using var json = AbrirRecurso();
        return Ler(json);
    });

    /// <summary>O arquivo embutido, lido uma vez por processo.</summary>
    public static DadosFicticios Arquivo => DoRecurso.Value;

    /// <summary>Lê o conjunto de um JSON no formato do <c>demonstracao.json</c>.</summary>
    public static DadosFicticios Ler(Stream json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<DadosFicticios>(json, Opcoes)
            ?? throw new InvalidOperationException("Os dados fictícios estão vazios.");
    }

    private static Stream AbrirRecurso() =>
        typeof(DadosFicticios).Assembly.GetManifestResourceStream(Recurso)
        ?? throw new InvalidOperationException($"Recurso {Recurso} ausente do assembly da Application.");
}

public sealed record EmpresaFicticia(string Nome);

/// <param name="Papel">Nome do papel padrão (<c>Dono</c>, <c>Membro</c>).</param>
public sealed record PessoaFicticia(string Chave, string Nome, string Email, string Papel, int CapacidadeSemanalMinutos);

public sealed record CategoriaFicticia(string Chave, string Nome);

/// <summary>Cliente com um contato e um contrato (o SLA do contrato é o sugerido da empresa).</summary>
public sealed record ClienteFicticio(
    string Chave,
    string Nome,
    IReadOnlyList<string>? Etiquetas,
    string? Responsavel,
    string? Email,
    string? RazaoSocial,
    string? Telefone,
    string? Observacoes,
    EnderecoDto? Endereco,
    ContatoFicticio Contato,
    ContratoFicticio Contrato);

public sealed record ContatoFicticio(string Nome, string Email);

/// <param name="InicioMeses">Início da vigência: o primeiro dia do mês corrente mais estes meses (negativo volta).</param>
public sealed record ContratoFicticio(TipoContrato Tipo, int InicioMeses, int? FranquiaMinutos);

/// <summary>
/// Solicitação aberta pela equipe para o contato e o contrato do cliente, seguida dos <see cref="Passos"/> na ordem.
/// </summary>
public sealed record SolicitacaoFicticia(
    string Chave,
    string Cliente,
    string Titulo,
    string? Descricao,
    Prioridade Prioridade,
    string? Responsavel,
    string? Categoria,
    IReadOnlyList<PassoFicticio>? Passos);

/// <param name="Acao">
/// <c>inicio</c>, <c>interacao</c> (com <see cref="Visibilidade"/> e <see cref="Texto"/>), <c>aguardando-cliente</c> ou
/// <c>resolucao</c> (com <see cref="Solucao"/>): o nome da ação na rota da API.
/// </param>
/// <param name="Por">Quem faz (chave da pessoa).</param>
public sealed record PassoFicticio(string Acao, string? Por, Visibilidade? Visibilidade, string? Texto, SolucaoFicticia? Solucao)
{
    public const string Inicio = "inicio";
    public const string Interacao = "interacao";
    public const string AguardandoCliente = "aguardando-cliente";
    public const string Resolucao = "resolucao";
}

public sealed record SolucaoFicticia(string Texto, string? Problema, string? Causa);

/// <param name="Inicio">Dias úteis a partir de hoje.</param>
/// <param name="Prazo">Dias úteis a partir de hoje.</param>
public sealed record ProjetoFicticio(string Chave, string Cliente, string Nome, int Inicio, int Prazo, int? EstimativaMinutos);

/// <summary>
/// Tarefa de uma solicitação, de um projeto ou só do cliente (uma das três chaves). Fica visível no portal quando é de
/// projeto. <see cref="Dia"/> é o prazo e o dia planejado.
/// </summary>
/// <param name="Dia">Dias úteis a partir de hoje (0 é hoje).</param>
/// <param name="Status">Status final (<c>A_FAZER</c>, <c>FAZENDO</c> ou <c>CONCLUIDA</c>), aplicado depois das horas.</param>
public sealed record TarefaFicticia(
    string Chave,
    string Titulo,
    string? Responsavel,
    string? Solicitacao,
    string? Projeto,
    string? Cliente,
    int Dia,
    int? EstimativaMinutos,
    StatusTarefa Status,
    IReadOnlyList<ItemFicticio>? Checklist);

public sealed record ItemFicticio(string Texto, bool Feito);

/// <summary>Horas apontadas por uma pessoa numa tarefa, faturáveis.</summary>
/// <param name="Dia">
/// Dias úteis a partir de hoje, nunca à frente; 0 é o último dia útil (hora não vai para o futuro).
/// </param>
/// <param name="Limite">
/// <c>semana</c>: só entra se cair na semana do último dia útil, antes dele; <c>mes</c>: antes do mês corrente, vai para
/// o último dia útil (o consumo do contrato é do mês). Sem limite, entra no dia.
/// </param>
public sealed record ApontamentoFicticio(string Por, string Tarefa, int Dia, int Minutos, string Descricao, string? Limite)
{
    public const string Semana = "semana";
    public const string Mes = "mes";
}

/// <summary>Recorrência do cliente (com o contrato dele), estimativa de 1h, visível no portal.</summary>
public sealed record RecorrenciaFicticia(
    string Cliente, string Titulo, string? Responsavel, Periodicidade Periodicidade, int DiaReferencia, int AntecedenciaDias);

/// <summary>Cronômetro rodando para a pessoa na tarefa.</summary>
public sealed record CronometroFicticio(string Por, string Tarefa);

/// <summary>O contato do cliente com acesso ao portal.</summary>
public sealed record PortalFicticio(string Cliente, EscopoPortal Escopo);
