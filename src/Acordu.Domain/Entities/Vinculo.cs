using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Participação de um usuário (global) em um tenant, com o papel que ele tem ali (segurança §2, ADR 0002). Com papel
/// CLIENTE, <see cref="ClienteId"/> e <see cref="EscopoPortal"/> são obrigatórios; com os demais, proibidos. Um vínculo
/// por usuário e tenant (índice único).
/// </summary>
public sealed class Vinculo : EntidadeBase
{
    /// <summary>Capacidade semanal padrão de quem entra na equipe (40h).</summary>
    public static readonly Duracao CapacidadePadrao = Duracao.FromHoras(40);

    /// <summary>Teto da capacidade semanal (as 168h da semana).</summary>
    public static readonly Duracao CapacidadeMaxima = Duracao.FromHoras(168);

    private Vinculo(Guid id, Guid tenantId, Guid usuarioId)
        : base(id, tenantId)
    {
        UsuarioId = usuarioId;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Vinculo()
    {
    }

    public Guid UsuarioId { get; private set; }

    public Guid PapelId { get; private set; }

    /// <summary>Cliente do contato; só com papel CLIENTE.</summary>
    public Guid? ClienteId { get; private set; }

    /// <summary>Só com papel CLIENTE.</summary>
    public EscopoPortal? EscopoPortal { get; private set; }

    /// <summary>
    /// Horas que a pessoa tem por semana para o planejamento (agenda e capacidade). Só na equipe: nula com papel CLIENTE.
    /// Zero vale (pessoa fora do planejamento, como férias longas).
    /// </summary>
    public Duracao? CapacidadeSemanal { get; private set; }

    /// <summary>Recebe o papel só para validar; guarda o Id. O tenant do vínculo é o do papel.</summary>
    public static Vinculo Criar(Guid id, Guid usuarioId, Papel papel, Guid? clienteId = null, EscopoPortal? escopo = null)
    {
        ArgumentNullException.ThrowIfNull(papel);
        var vinculo = new Vinculo(id, papel.TenantId, Ids.Obrigatorio(usuarioId, nameof(usuarioId)));
        vinculo.Atribuir(papel, clienteId, escopo);
        return vinculo;
    }

    /// <summary>Troca o papel; cliente e escopo seguem a regra do novo papel.</summary>
    public void TrocarPapel(Papel papel, Guid? clienteId = null, EscopoPortal? escopo = null)
    {
        ArgumentNullException.ThrowIfNull(papel);
        if (papel.TenantId != TenantId)
            throw new ArgumentException("O papel é de outro tenant.", nameof(papel));
        Atribuir(papel, clienteId, escopo);
    }

    /// <summary>Define a capacidade semanal; só para a equipe, entre zero e <see cref="CapacidadeMaxima"/>.</summary>
    public void DefinirCapacidade(Duracao capacidade)
    {
        if (ClienteId is not null)
            throw new ArgumentException("Capacidade semanal só existe em vínculo da equipe.", nameof(capacidade));
        if (capacidade > CapacidadeMaxima)
            throw new ArgumentOutOfRangeException(nameof(capacidade), capacidade, "A capacidade semanal vai de zero a 168h.");
        CapacidadeSemanal = capacidade;
    }

    public void AlterarEscopo(EscopoPortal escopo)
    {
        if (ClienteId is null)
            throw new ArgumentException("Escopo do portal só existe em vínculo de cliente.", nameof(escopo));
        EscopoPortal = ValidarEscopo(escopo);
    }

    private void Atribuir(Papel papel, Guid? clienteId, EscopoPortal? escopo)
    {
        if (papel.Tipo == TipoPapel.Cliente)
        {
            if (clienteId is null)
                throw new ArgumentException("Vínculo de cliente exige o cliente.", nameof(clienteId));
            if (escopo is null)
                throw new ArgumentException("Vínculo de cliente exige o escopo do portal.", nameof(escopo));
            ClienteId = Ids.Obrigatorio(clienteId.Value, nameof(clienteId));
            EscopoPortal = ValidarEscopo(escopo.Value);
            CapacidadeSemanal = null;
        }
        else
        {
            if (clienteId is not null)
                throw new ArgumentException("Só vínculo de cliente tem cliente.", nameof(clienteId));
            if (escopo is not null)
                throw new ArgumentException("Só vínculo de cliente tem escopo do portal.", nameof(escopo));
            ClienteId = null;
            EscopoPortal = null;
            CapacidadeSemanal ??= CapacidadePadrao;
        }

        PapelId = papel.Id;
    }

    private static EscopoPortal ValidarEscopo(EscopoPortal escopo) =>
        Enum.IsDefined(escopo) ? escopo : throw new ArgumentOutOfRangeException(nameof(escopo), escopo, "Escopo inválido.");
}
