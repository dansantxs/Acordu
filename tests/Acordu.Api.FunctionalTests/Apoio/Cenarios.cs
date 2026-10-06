using System.Net;
using System.Text.Json;

namespace Acordu.Api.FunctionalTests.Apoio;

/// <summary>Montagens repetidas pelos testes de execução: cliente com contrato e solicitação.</summary>
internal static class Cenarios
{
    public static object[] Sla(int primeiraResposta = 240, int resolucao = 1920) =>
    [
        new { prioridade = "URGENTE", primeiraRespostaMinutos = primeiraResposta, resolucaoMinutos = resolucao },
        new { prioridade = "ALTA", primeiraRespostaMinutos = primeiraResposta, resolucaoMinutos = resolucao },
        new { prioridade = "MEDIA", primeiraRespostaMinutos = primeiraResposta, resolucaoMinutos = resolucao },
        new { prioridade = "BAIXA", primeiraRespostaMinutos = primeiraResposta, resolucaoMinutos = resolucao },
    ];

    public static Guid Id(JsonElement elemento) => elemento.GetProperty("id").GetGuid();

    public static async Task<Guid> ClienteAsync(Equipe equipe, string nome = "Padaria Bom Grão") =>
        Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", new { nome }));

    /// <summary>Banco de horas de 20h desde 01/09/2026.</summary>
    public static async Task<Guid> ContratoAsync(Equipe equipe, Guid cliente) =>
        Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", new
        {
            clienteId = cliente, tipo = "BANCO_HORAS", inicio = "2026-09-01", franquiaMinutos = 1200, sla = Sla(),
        }));

    public static async Task<Guid> SolicitacaoAsync(Equipe equipe, Guid cliente) =>
        Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, titulo = "Ajustar banner da campanha", prioridade = "MEDIA",
        }));

    public static async Task<Guid> TarefaAsync(Equipe equipe, Guid? solicitacaoId = null, string titulo = "Refazer arte") =>
        Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new
        {
            titulo, solicitacaoId, visivelCliente = solicitacaoId is not null,
        }));
}
