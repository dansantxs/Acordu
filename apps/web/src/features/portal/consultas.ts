import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { apiPortal } from '@/shared/api/cliente'
import type { components } from '@/shared/api/portal'

export type PortalEu = components['schemas']['PortalEuDto']
export type StatusExterno = components['schemas']['StatusExterno']
export type SolicitacaoPortal = components['schemas']['SolicitacaoPortalDto']
export type SolicitacaoPortalResumo = components['schemas']['SolicitacaoPortalResumoDto']

export const STATUS_EXTERNO: Record<StatusExterno, string> = {
    RECEBIDA: 'Recebida',
    EM_ANDAMENTO: 'Em andamento',
    AGUARDANDO_VOCE: 'Esperando você',
    CONCLUIDA: 'Concluída',
    CANCELADA: 'Cancelada',
}

export const ESTILO_STATUS_EXTERNO: Record<StatusExterno, string> = {
    RECEBIDA: 'bg-trilho text-tinta',
    EM_ANDAMENTO: 'bg-acento-suave text-acento-texto',
    AGUARDANDO_VOCE: 'bg-ambar-suave text-ambar-texto',
    CONCLUIDA: 'bg-verde-suave text-verde',
    CANCELADA: 'bg-trilho text-suave',
}

/** Grupos da lista do portal (decisão 3 do F7), pelo status externo. */
export const GRUPOS = {
    esperando: { rotulo: 'Esperando você', status: ['AGUARDANDO_VOCE'] },
    andamento: { rotulo: 'Em andamento', status: ['RECEBIDA', 'EM_ANDAMENTO'] },
    concluidas: { rotulo: 'Concluídas', status: ['CONCLUIDA', 'CANCELADA'] },
} satisfies Record<string, { rotulo: string; status: StatusExterno[] }>

export type Grupo = keyof typeof GRUPOS

/** `?grupo=` (padrão: em andamento) e `?pagina=`; `grupo`, e não `aba`, que já é das tarefas (convenções §4). */
export interface FiltrosPortal {
    grupo?: Grupo
    pagina?: number
}

export const TAMANHO_PAGINA = 20

/** Quem é o contato e de qual prestador (`GET /portal/eu`): nome, logotipo, escopo e se vê contratos. */
export const consultaEu = queryOptions({
    queryKey: ['portal', 'eu'],
    queryFn: async () => (await apiPortal.GET('/api/v1/portal/eu')).data!,
    staleTime: 5 * 60_000,
})

export function consultaSolicitacoesPortal(grupo: Grupo, pagina = 1) {
    return queryOptions({
        queryKey: ['portal', 'solicitacoes', grupo, pagina],
        queryFn: async () =>
            (
                await apiPortal.GET('/api/v1/portal/solicitacoes', {
                    params: {
                        query: { statusExterno: GRUPOS[grupo].status.join(','), pagina, tamanhoPagina: TAMANHO_PAGINA },
                    },
                })
            ).data!,
        placeholderData: keepPreviousData,
    })
}

export function consultaSolicitacaoPortal(id: string) {
    return queryOptions({
        queryKey: ['portal', 'solicitacao', id],
        queryFn: async () =>
            (await apiPortal.GET('/api/v1/portal/solicitacoes/{id}', { params: { path: { id } } })).data!,
    })
}
