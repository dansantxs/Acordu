using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests;

/// <summary>Contratos e políticas de SLA de apoio aos testes.</summary>
internal static class Contratos
{
    public static readonly Guid Cliente = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly DateOnly InicioOutubro = new(2026, 10, 1);

    /// <summary>A política do exemplo do contrato da API (minutos úteis).</summary>
    public static PoliticaSla PoliticaPadrao() => new(
    [
        new PrazoSla(Prioridade.Urgente, Duracao.FromHoras(1), Duracao.FromHoras(4)),
        new PrazoSla(Prioridade.Alta, Duracao.FromHoras(2), Duracao.FromHoras(16)),
        new PrazoSla(Prioridade.Media, Duracao.FromHoras(4), Duracao.FromHoras(32)),
        new PrazoSla(Prioridade.Baixa, Duracao.FromHoras(8), Duracao.FromHoras(80)),
    ]);

    public static Contrato Criar(
        TipoContrato tipo = TipoContrato.BancoHoras,
        Duracao? franquia = null,
        DateOnly? fim = null) =>
        Contrato.Criar(
            Guid.NewGuid(),
            Calendarios.Tenant,
            Cliente,
            tipo,
            new Periodo(InicioOutubro, fim),
            franquia ?? (tipo == TipoContrato.BancoHoras ? Duracao.FromHoras(20) : null),
            PoliticaPadrao());
}
