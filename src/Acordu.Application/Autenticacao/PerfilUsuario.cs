using Acordu.Domain.Enums;

namespace Acordu.Application.Autenticacao;

/// <summary>
/// Resposta do <c>GET /auth/eu</c> (contrato §2). <see cref="EmailConfirmado"/> falso não bloqueia o uso: a SPA mostra o
/// aviso para confirmar. O envio a terceiros depende da empresa (<see cref="TenantAtivo.EnvioLiberado"/>), não da conta.
/// <see cref="TextosPendentes"/> (F14h, design D10 e D16): os textos legais cuja versão vigente a conta ainda não aceitou
/// nem marcou "ciente" (a falta de aceite também conta, F14-119). Sempre vazia com os textos não publicados (F14-118) e
/// para o contato do portal (F14-120). Não vazia, a SPA mostra o aviso até o <c>POST /auth/textos/ciente</c>.
/// </summary>
public sealed record PerfilUsuario(
    Guid UsuarioId,
    string Nome,
    string Email,
    bool EmailConfirmado,
    TenantAtivo Tenant,
    string Papel,
    IReadOnlyList<string> Permissoes,
    Guid? ClienteId,
    EscopoPortal? EscopoPortal,
    IReadOnlyList<TenantDoUsuario> Tenants,
    IReadOnlyList<TextoPendente> TextosPendentes);

/// <summary>Um texto legal com versão vigente ainda não aceita pela conta (F14-83, F14-119).</summary>
public sealed record TextoPendente(TipoTextoLegal Texto, string Versao);

/// <summary>
/// Tenant ativo, com o fuso em que a UI exibe instantes (convenções §4). <see cref="EnvioLiberado"/> falso: a empresa
/// ainda não envia convites nem links a terceiros (RF-AUT-06), e a SPA explica e desabilita esses botões.
/// <see cref="Situacao"/> e as datas (F14-02): <see cref="FimDoTeste"/> em TESTE (e o que era, depois de vencer);
/// <see cref="SomenteLeituraDesde"/> e <see cref="ExpurgoPrevistoEm"/> só em VENCIDA ou CANCELADA.
/// <see cref="DiasFaixaTeste"/>: a janela antes do fim do teste em que a SPA mostra a faixa (<c>Empresas__DiasFaixaTeste</c>,
/// F14-10, F14-11). <see cref="Demonstracao"/>: a empresa de demonstração (RF-NEB-02, F14s), fora do ciclo de situação e
/// sem envio de e-mail; a SPA mostra a faixa da demonstração.
/// </summary>
public sealed record TenantAtivo(
    Guid Id,
    string Nome,
    string FusoHorario,
    bool EnvioLiberado,
    SituacaoEmpresa Situacao,
    DateTimeOffset? FimDoTeste,
    DateTimeOffset? SomenteLeituraDesde,
    DateTimeOffset? ExpurgoPrevistoEm,
    int DiasFaixaTeste,
    bool Demonstracao);

/// <summary>Um tenant em que o usuário tem vínculo, para a troca de tenant.</summary>
public sealed record TenantDoUsuario(Guid Id, string Nome, string Papel);
