using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Agrupa tarefas de um cliente (modelo de domínio §4.7). Progresso (tarefas concluídas, horas × estimativa) é read model.
/// Trocar o contrato não reescreve horas já apontadas: o apontamento congela o contrato no registro.
/// </summary>
public sealed class Projeto : RaizComCliente
{
    private Projeto(Guid id, Guid tenantId, Guid clienteId, string nome)
        : base(id, tenantId, clienteId)
    {
        Nome = nome;
        Status = StatusProjeto.EmAndamento;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Projeto()
    {
        Nome = null!;
    }

    public string Nome { get; private set; }

    public Guid? ContratoId { get; private set; }

    /// <summary>Início planejado (barra na linha do tempo); sem ele o projeto aparece só no prazo.</summary>
    public DateOnly? Inicio { get; private set; }

    public DateOnly? Prazo { get; private set; }

    public Duracao? Estimativa { get; private set; }

    public StatusProjeto Status { get; private set; }

    /// <summary>A Application garante que o contrato, se houver, é do mesmo cliente.</summary>
    public static Projeto Criar(
        Guid id,
        Guid tenantId,
        Guid clienteId,
        string nome,
        Guid? contratoId,
        DateOnly? prazo,
        Duracao? estimativa,
        DateOnly? inicio = null)
    {
        ValidarPeriodo(inicio, prazo);
        return new(id, tenantId, clienteId, Texto.Obrigatorio(nome, LimitesTexto.Titulo, nameof(nome)))
        {
            ContratoId = Ids.Opcional(contratoId, nameof(contratoId)),
            Inicio = inicio,
            Prazo = prazo,
            Estimativa = ValidarEstimativa(estimativa),
        };
    }

    public void Editar(string nome, DateOnly? prazo, Duracao? estimativa, DateOnly? inicio = null)
    {
        GarantirEmAndamento(nameof(Editar));
        ValidarPeriodo(inicio, prazo);
        Nome = Texto.Obrigatorio(nome, LimitesTexto.Titulo, nameof(nome));
        Inicio = inicio;
        Prazo = prazo;
        Estimativa = ValidarEstimativa(estimativa);
    }

    public void AlterarContrato(Guid? contratoId)
    {
        GarantirEmAndamento(nameof(AlterarContrato));
        ContratoId = Ids.Opcional(contratoId, nameof(contratoId));
    }

    public void Concluir()
    {
        GarantirEmAndamento(nameof(Concluir));
        Status = StatusProjeto.Concluido;
    }

    public void Cancelar()
    {
        GarantirEmAndamento(nameof(Cancelar));
        Status = StatusProjeto.Cancelado;
    }

    private static void ValidarPeriodo(DateOnly? inicio, DateOnly? prazo)
    {
        if (inicio is { } i && prazo is { } p && i > p)
            throw new ArgumentException("O início do projeto não pode ser depois do prazo.", nameof(inicio));
    }

    private static Duracao? ValidarEstimativa(Duracao? estimativa) =>
        estimativa is { EhZero: true }
            ? throw new ArgumentException("A estimativa, se informada, deve ser maior que zero.", nameof(estimativa))
            : estimativa;

    private void GarantirEmAndamento(string operacao)
    {
        if (Status != StatusProjeto.EmAndamento)
            throw new TransicaoInvalidaException($"A operação {operacao} não é permitida para projeto {Status}.");
    }
}
