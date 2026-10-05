using System.Text.Json;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Acordu.Infrastructure.Persistence.Conversores;

/// <summary>
/// <see cref="PoliticaSla"/> em <c>jsonb</c>: lista de prazos por prioridade, em minutos. Nada consulta a política por
/// SQL; a coluna JSON evita uma tabela só para quatro linhas por contrato.
/// </summary>
public sealed class PoliticaSlaConverter : ValueConverter<PoliticaSla, string>
{
    public PoliticaSlaConverter()
        : base(p => Serializar(p), j => Desserializar(j))
    {
    }

    private static string Serializar(PoliticaSla politica) =>
        JsonSerializer.Serialize(
            politica.Prazos.Select(p => new PrazoJson(
                EnumTexto<Prioridade>.Texto(p.Prioridade), p.PrimeiraResposta.Minutos, p.Resolucao.Minutos)).ToList(),
            OpcoesJson.Padrao);

    private static PoliticaSla Desserializar(string json) =>
        new((JsonSerializer.Deserialize<List<PrazoJson>>(json, OpcoesJson.Padrao) ?? [])
            .Select(p => new PrazoSla(
                EnumTexto<Prioridade>.Valor(p.Prioridade),
                Duracao.FromMinutos(p.PrimeiraRespostaMinutos),
                Duracao.FromMinutos(p.ResolucaoMinutos))));

    private sealed record PrazoJson(string Prioridade, int PrimeiraRespostaMinutos, int ResolucaoMinutos);
}

/// <summary><see cref="ModeloTarefa"/> em <c>jsonb</c> (molde das tarefas recorrentes; a checklist mantém a ordem).</summary>
public sealed class ModeloTarefaConverter : ValueConverter<ModeloTarefa, string>
{
    public ModeloTarefaConverter()
        : base(m => Serializar(m), j => Desserializar(j))
    {
    }

    private static string Serializar(ModeloTarefa modelo) =>
        JsonSerializer.Serialize(
            new ModeloJson(
                modelo.Titulo,
                modelo.Descricao,
                modelo.ResponsavelId,
                [.. modelo.Checklist],
                modelo.Estimativa?.Minutos,
                modelo.VisivelCliente),
            OpcoesJson.Padrao);

    private static ModeloTarefa Desserializar(string json)
    {
        var m = JsonSerializer.Deserialize<ModeloJson>(json, OpcoesJson.Padrao)
            ?? throw new InvalidOperationException("Modelo de tarefa vazio no banco.");
        return new ModeloTarefa(
            m.Titulo,
            m.Descricao,
            m.ResponsavelId,
            m.Checklist ?? [],
            m.EstimativaMinutos is { } minutos ? Duracao.FromMinutos(minutos) : null,
            m.VisivelCliente);
    }

    private sealed record ModeloJson(
        string Titulo,
        string? Descricao,
        Guid? ResponsavelId,
        List<string>? Checklist,
        int? EstimativaMinutos,
        bool VisivelCliente);
}

internal static class OpcoesJson
{
    public static readonly JsonSerializerOptions Padrao = new(JsonSerializerDefaults.Web);
}
