using Acordu.Domain.Common;
using Acordu.Domain.Enums;

namespace Acordu.Domain.Entities;

/// <summary>
/// Participação de um usuário (global) em um tenant, com o papel que ele tem ali (segurança §2, ADR 0002). Com papel
/// CLIENTE, <see cref="ClienteId"/> e <see cref="EscopoPortal"/> são obrigatórios; com os demais, proibidos. Um vínculo
/// por usuário e tenant (índice único).
/// </summary>
public sealed class Vinculo : EntidadeBase
{
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
        }
        else
        {
            if (clienteId is not null)
                throw new ArgumentException("Só vínculo de cliente tem cliente.", nameof(clienteId));
            if (escopo is not null)
                throw new ArgumentException("Só vínculo de cliente tem escopo do portal.", nameof(escopo));
            ClienteId = null;
            EscopoPortal = null;
        }

        PapelId = papel.Id;
    }

    private static EscopoPortal ValidarEscopo(EscopoPortal escopo) =>
        Enum.IsDefined(escopo) ? escopo : throw new ArgumentOutOfRangeException(nameof(escopo), escopo, "Escopo inválido.");
}
