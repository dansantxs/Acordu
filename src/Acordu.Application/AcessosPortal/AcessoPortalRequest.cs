using Acordu.Domain.Enums;

namespace Acordu.Application.AcessosPortal;

/// <summary>Corpo do <c>POST</c> e do <c>PATCH .../acesso-portal</c> (contrato §4.2). O escopo é obrigatório.</summary>
public sealed record AcessoPortalRequest(EscopoPortal? EscopoPortal);
