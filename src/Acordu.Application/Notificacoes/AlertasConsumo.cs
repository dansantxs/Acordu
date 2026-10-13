using System.Globalization;
using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Application.Common;
using Acordu.Application.Contratos;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Notificacoes;

/// <summary>
/// Alertas de consumo de contrato (RF-CTR-07, read side §3.4), depois do commit de apontamento ou ajuste de saldo: ao
/// cruzar 80% ou 100% da franquia para cima, avisa quem tem <c>contrato.gerenciar</c>, uma vez por (contrato, período,
/// limiar). Cair abaixo rearma o limiar. Limiares fixos no v1.
/// </summary>
/// <remarks>
/// Período: o mês, no MENSAL_FIXO (a franquia é por mês); no BANCO_HORAS o saldo é acumulado, então o período é o
/// contrato inteiro e o percentual é o acumulado até hoje. AVULSO e contrato sem franquia não têm percentual.
/// </remarks>
public sealed class AlertasConsumo :
    IManipuladorPosCommit<ApontamentoRegistrado>,
    IManipuladorPosCommit<ApontamentoAlterado>,
    IManipuladorPosCommit<ApontamentoExcluido>,
    IManipuladorPosCommit<AjusteSaldoRegistrado>
{
    public static readonly IReadOnlyList<int> Limiares = [80, 100];

    private readonly IConsultasContrato _consultas;
    private readonly IRepositorio<Contrato> _contratos;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IPessoasDaEquipe _pessoas;
    private readonly IMarcadoresDeAviso _marcadores;
    private readonly IEnviadorEmail _email;
    private readonly RelogioDoTenant _relogio;

    public AlertasConsumo(
        IConsultasContrato consultas,
        IRepositorio<Contrato> contratos,
        IRepositorio<Cliente> clientes,
        IPessoasDaEquipe pessoas,
        IMarcadoresDeAviso marcadores,
        IEnviadorEmail email,
        RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(contratos);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(pessoas);
        ArgumentNullException.ThrowIfNull(marcadores);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(relogio);
        _consultas = consultas;
        _contratos = contratos;
        _clientes = clientes;
        _pessoas = pessoas;
        _marcadores = marcadores;
        _email = email;
        _relogio = relogio;
    }

    public Task TratarAsync(ApontamentoRegistrado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return VerificarAsync(evento.ContratoId, [evento.Data], cancellationToken);
    }

    public Task TratarAsync(ApontamentoAlterado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return VerificarAsync(evento.ContratoId, [evento.DataAnterior, evento.Data], cancellationToken);
    }

    public Task TratarAsync(ApontamentoExcluido evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return VerificarAsync(evento.ContratoId, [evento.Data], cancellationToken);
    }

    public Task TratarAsync(AjusteSaldoRegistrado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return VerificarAsync(evento.ContratoId, [evento.Data], cancellationToken);
    }

    private async Task VerificarAsync(Guid? contratoId, IReadOnlyList<DateOnly> datas, CancellationToken cancellationToken)
    {
        if (contratoId is not { } id)
            return;
        var contrato = await _contratos.ObterPorIdAsync(id, cancellationToken);
        if (contrato is null || contrato.Tipo == TipoContrato.Avulso)
            return;

        var hoje = await _relogio.HojeAsync(cancellationToken);
        var periodos = contrato.Tipo == TipoContrato.BancoHoras
            ? [MesReferencia.De(hoje)]
            : datas.Select(MesReferencia.De).Distinct().ToList();
        foreach (var periodo in periodos)
            await VerificarPeriodoAsync(contrato, periodo, hoje, cancellationToken);
    }

    private async Task VerificarPeriodoAsync(Contrato contrato, MesReferencia periodo, DateOnly hoje, CancellationToken cancellationToken)
    {
        var consumo = await _consultas.ConsumoAsync(contrato.Id, periodo, hoje, cancellationToken);
        if (consumo?.Percentual is not { } percentual)
            return;

        var chavePeriodo = contrato.Tipo == TipoContrato.BancoHoras ? "acumulado" : periodo.ToString();
        foreach (var limiar in Limiares)
        {
            var chave = $"consumo:{chavePeriodo}:{limiar.ToString(CultureInfo.InvariantCulture)}";
            if (percentual < limiar)
            {
                await _marcadores.DesmarcarAsync(contrato.Id, chave, cancellationToken);
                continue;
            }

            if (!await _marcadores.MarcarAsync(contrato.Id, chave, cancellationToken))
                continue;
            try
            {
                await AvisarAsync(contrato, consumo, limiar, cancellationToken);
            }
            catch
            {
                await _marcadores.DesmarcarAsync(contrato.Id, chave, CancellationToken.None);
                throw;
            }
        }
    }

    private async Task AvisarAsync(Contrato contrato, ConsumoContratoDto consumo, int limiar, CancellationToken cancellationToken)
    {
        var cliente = await _clientes.ObterPorIdAsync(contrato.ClienteId, cancellationToken);
        var banco = contrato.Tipo == TipoContrato.BancoHoras;
        var consumido = banco ? consumo.ConsumidoAcumuladoMinutos ?? consumo.ConsumidoMinutos : consumo.ConsumidoMinutos;
        var descricao = banco ? "do banco de horas" : $"da franquia de {consumo.Periodo}";
        foreach (var pessoa in await _pessoas.ComPermissaoAsync(Permissoes.ContratoGerenciar, cancellationToken))
        {
            await _email.EnviarAsync(
                new MensagemEmail(
                    pessoa.Email,
                    $"Contrato de {cliente?.Nome} atingiu {limiar}% {(banco ? "do banco de horas" : "da franquia")}",
                    $"""
                    Olá, {pessoa.Nome}!

                    O contrato de {cliente?.Nome} chegou a {consumo.Percentual}% {descricao}.
                    Consumido: {Horas(consumido)}{(consumo.FranquiaMinutos is { } franquia ? $" de {Horas(franquia)}" : string.Empty)}{(consumo.SaldoMinutos is { } saldo ? $" (saldo: {Horas(saldo)})" : string.Empty)}.
                    """,
                    "aviso-consumo"),
                cancellationToken);
        }
    }

    /// <summary>Minutos como "17h15" (convenções: tempo em minutos, formatado só na apresentação).</summary>
    private static string Horas(int minutos)
    {
        var sinal = minutos < 0 ? "-" : string.Empty;
        var absoluto = Math.Abs(minutos);
        return string.Create(CultureInfo.InvariantCulture, $"{sinal}{absoluto / 60}h{absoluto % 60:00}");
    }
}
