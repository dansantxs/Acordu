using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Acordo com o cliente: tipo, vigência, franquia de horas e política de SLA (modelo de domínio §4.2).
/// O consumo não é estado do contrato: é read model sobre apontamentos e ajustes (§3.2).
/// A regra "no máximo um ATIVO de cada tipo por cliente no período" envolve vários agregados e é da Application.
/// </summary>
public sealed class Contrato : RaizComCliente
{
    private Contrato(
        Guid id,
        Guid tenantId,
        Guid clienteId,
        TipoContrato tipo,
        Periodo vigencia,
        Duracao? franquia,
        PoliticaSla sla)
        : base(id, tenantId, clienteId)
    {
        Tipo = tipo;
        Vigencia = vigencia;
        Franquia = franquia;
        Sla = sla;
        Status = StatusContrato.Ativo;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Contrato()
    {
        Vigencia = null!;
        Sla = null!;
    }

    public TipoContrato Tipo { get; private set; }

    public Periodo Vigencia { get; private set; }

    /// <summary>Horas contratadas: franquia mensal (MENSAL_FIXO) ou saldo inicial (BANCO_HORAS).</summary>
    public Duracao? Franquia { get; private set; }

    public PoliticaSla Sla { get; private set; }

    public StatusContrato Status { get; private set; }

    public static Contrato Criar(
        Guid id,
        Guid tenantId,
        Guid clienteId,
        TipoContrato tipo,
        Periodo vigencia,
        Duracao? franquia,
        PoliticaSla sla)
    {
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de contrato inválido.");
        ArgumentNullException.ThrowIfNull(vigencia);
        ArgumentNullException.ThrowIfNull(sla);
        ValidarFranquia(tipo, franquia);

        return new Contrato(id, tenantId, clienteId, tipo, vigencia, franquia, sla);
    }

    public void AlterarFranquia(Duracao? franquia)
    {
        GarantirNaoEncerrado(nameof(AlterarFranquia));
        ValidarFranquia(Tipo, franquia);
        Franquia = franquia;
    }

    public void DefinirSla(PoliticaSla sla)
    {
        ArgumentNullException.ThrowIfNull(sla);
        GarantirNaoEncerrado(nameof(DefinirSla));
        Sla = sla;
    }

    /// <summary>Define ou remove o fim da vigência; o início é imutável.</summary>
    public void AlterarFimVigencia(DateOnly? fim)
    {
        GarantirNaoEncerrado(nameof(AlterarFimVigencia));
        Vigencia = new Periodo(Vigencia.Inicio, fim);
    }

    public void Suspender()
    {
        if (Status != StatusContrato.Ativo)
            throw Transicao(nameof(Suspender));
        Status = StatusContrato.Suspenso;
    }

    public void Reativar()
    {
        if (Status != StatusContrato.Suspenso)
            throw Transicao(nameof(Reativar));
        Status = StatusContrato.Ativo;
    }

    /// <summary>
    /// Encerra o contrato (final) com vigência até <paramref name="data"/>. Encerrar nunca estende a
    /// vigência: se o fim já definido for anterior, ele é mantido.
    /// </summary>
    public void Encerrar(DateOnly data)
    {
        GarantirNaoEncerrado(nameof(Encerrar));
        var fim = Vigencia.Fim is { } atual && atual < data ? atual : data;
        Vigencia = new Periodo(Vigencia.Inicio, fim);
        Status = StatusContrato.Encerrado;
    }

    /// <summary>ATIVO e com <paramref name="data"/> dentro da vigência. SUSPENSO não aceita novas solicitações nem SLA.</summary>
    public bool EstaVigenteEm(DateOnly data) => Status == StatusContrato.Ativo && Vigencia.Contem(data);

    private static void ValidarFranquia(TipoContrato tipo, Duracao? franquia)
    {
        switch (tipo)
        {
            case TipoContrato.MensalFixo when franquia is { EhZero: true }:
                throw new ContratoIncoerenteException("A franquia de um contrato mensal fixo, se informada, deve ser maior que zero.");
            case TipoContrato.BancoHoras when franquia is null or { EhZero: true }:
                throw new ContratoIncoerenteException("Contrato de banco de horas exige franquia (saldo contratado) maior que zero.");
            case TipoContrato.Avulso when franquia is not null:
                throw new ContratoIncoerenteException("Contrato avulso não tem franquia.");
        }
    }

    private void GarantirNaoEncerrado(string operacao)
    {
        if (Status == StatusContrato.Encerrado)
            throw Transicao(operacao);
    }

    private TransicaoInvalidaException Transicao(string operacao) =>
        new($"A operação {operacao} não é permitida para contrato {Status}.");
}
