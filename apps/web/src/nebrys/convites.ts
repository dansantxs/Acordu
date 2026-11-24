import { mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import type { Convite, SituacaoConvite } from './api'
import { FUSO_NEBRYS } from './situacao'

export const ROTULO_SITUACAO_CONVITE: Record<SituacaoConvite, string> = {
    PENDENTE: 'Pendente',
    VENCIDO: 'Vencido',
    USADO: 'Usado',
    REVOGADO: 'Revogado',
}

export const ESTILO_SITUACAO_CONVITE: Record<SituacaoConvite, string> = {
    PENDENTE: 'bg-ambar-suave text-ambar-texto',
    VENCIDO: 'bg-vermelho-suave text-vermelho',
    USADO: 'bg-verde-suave text-verde',
    REVOGADO: 'bg-trilho text-suave',
}

export type AcaoConvite = 'reenvio' | 'revogacao'

/**
 * As ações que valem em cada situação (contrato §17): reenviar e revogar o pendente ou o vencido; o usado e o revogado
 * não mudam mais. A API confere de novo (`422`).
 */
export const ACOES_CONVITE: Record<SituacaoConvite, readonly AcaoConvite[]> = {
    PENDENTE: ['reenvio', 'revogacao'],
    VENCIDO: ['reenvio', 'revogacao'],
    USADO: [],
    REVOGADO: [],
}

/** Data e hora no fuso da área ("23/11/2026, 15:00"): o convite vence na hora exata do envio mais 7 dias. */
export function dataHora(instante: string): string {
    return new Intl.DateTimeFormat('pt-BR', {
        timeZone: FUSO_NEBRYS,
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
    }).format(new Date(instante))
}

/** O que importa na situação do convite: até quando vale, quando venceu, foi usado ou revogado. */
export function marcoDoConvite(c: Pick<Convite, 'situacao' | 'expiraEm' | 'usadoEm' | 'revogadoEm'>): string {
    if (c.situacao === 'USADO' && c.usadoEm) return `Usado em ${dataHora(c.usadoEm)}`
    if (c.situacao === 'REVOGADO' && c.revogadoEm) return `Revogado em ${dataHora(c.revogadoEm)}`
    if (c.situacao === 'VENCIDO') return `Venceu em ${dataHora(c.expiraEm)}`
    return `Vale até ${dataHora(c.expiraEm)}`
}

/** O e-mail com o link não saiu (`emailEnviado: false`): o convite existe, e a Nebrys reenvia (contrato §17). */
export const EMAIL_NAO_SAIU = 'O convite foi gravado, mas o e-mail não saiu. Reenvie o convite daqui a pouco.'

/** Recusa da API dita na janela: `422` traz a explicação (convite em aberto para o e-mail, convite que já mudou). */
export function recusaDaAcao(erro: unknown): string | null {
    if (!erro) return null
    if (erro instanceof ErroApi && erro.status === 422) return erro.detalhe ?? erro.message
    return mensagemDeErro(erro)
}
