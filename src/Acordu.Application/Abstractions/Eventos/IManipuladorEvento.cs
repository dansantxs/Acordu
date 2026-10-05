using Acordu.Domain.Common;

namespace Acordu.Application.Abstractions.Eventos;

/// <summary>
/// Reação de estado a um evento, executada <b>dentro</b> da transação da unidade de trabalho (ex.: registrar a primeira
/// resposta ao receber interação externa da equipe). Pode alterar outras raízes; os eventos que elas gerarem são
/// despachados na rodada seguinte. Falha aqui desfaz o caso de uso inteiro.
/// </summary>
public interface IManipuladorEvento<in TEvento>
    where TEvento : IEventoDominio
{
    Task TratarAsync(TEvento evento, CancellationToken cancellationToken);
}
