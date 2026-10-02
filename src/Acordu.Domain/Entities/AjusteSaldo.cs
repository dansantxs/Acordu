using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Ajuste manual do saldo de um banco de horas (RF-CTR-06): recarga (positivo) ou estorno (negativo),
/// com motivo e autor. Agregado próprio porque cresce sem limite (modelo de domínio §3.2).
/// </summary>
public sealed class AjusteSaldo : RaizComCliente
{
    private AjusteSaldo(
        Guid id,
        Guid tenantId,
        Guid clienteId,
        Guid contratoId,
        DuracaoAssinada quantidade,
        string motivo,
        DateOnly data,
        Guid autorId)
        : base(id, tenantId, clienteId)
    {
        ContratoId = contratoId;
        Quantidade = quantidade;
        Motivo = motivo;
        Data = data;
        AutorId = autorId;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private AjusteSaldo()
    {
        Motivo = null!;
    }

    public Guid ContratoId { get; private set; }

    public DuracaoAssinada Quantidade { get; private set; }

    public string Motivo { get; private set; }

    public DateOnly Data { get; private set; }

    public Guid AutorId { get; private set; }

    /// <summary>
    /// Registra o ajuste. Recebe o <see cref="Contrato"/> (só leitura) para herdar tenant e cliente e
    /// validar localmente que ele é um banco de horas não encerrado — sem depender da Application para isso.
    /// </summary>
    public static AjusteSaldo Registrar(
        Guid id,
        Contrato contrato,
        DuracaoAssinada quantidade,
        string motivo,
        DateOnly data,
        Guid autorId)
    {
        ArgumentNullException.ThrowIfNull(contrato);
        if (quantidade.Minutos == 0)
            throw new ArgumentException("O ajuste não pode ser zero.", nameof(quantidade));
        if (autorId == Guid.Empty)
            throw new ArgumentException("O autor é obrigatório.", nameof(autorId));
        var motivoValido = Texto.Obrigatorio(motivo, LimitesTexto.Motivo, nameof(motivo));

        if (contrato.Tipo != TipoContrato.BancoHoras)
            throw new ContratoIncoerenteException("Ajuste de saldo só existe em contrato de banco de horas.");
        if (contrato.Status == StatusContrato.Encerrado)
            throw new ContratoIncoerenteException("Contrato encerrado não aceita ajuste de saldo.");
        if (data < contrato.Vigencia.Inicio)
            throw new ContratoIncoerenteException("A data do ajuste não pode ser anterior ao início do contrato.");

        return new AjusteSaldo(id, contrato.TenantId, contrato.ClienteId, contrato.Id, quantidade, motivoValido, data, autorId);
    }
}
