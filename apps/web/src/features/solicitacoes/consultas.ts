import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'
import { EM_ABERTO, type Prioridade, type Status } from './rotulos'

export type SolicitacaoResumo = components['schemas']['SolicitacaoResumoDto']

/** Visões da fila: atalhos para o filtro de status. */
export const VISOES = {
    abertas: { rotulo: 'Em aberto', status: EM_ABERTO },
    'aguardando-cliente': { rotulo: 'Aguardando cliente', status: ['AGUARDANDO_CLIENTE'] },
    resolvidas: { rotulo: 'Resolvidas', status: ['RESOLVIDA', 'FECHADA'] },
    canceladas: { rotulo: 'Canceladas', status: ['CANCELADA'] },
    todas: { rotulo: 'Todas', status: [] },
} satisfies Record<string, { rotulo: string; status: Status[] }>

export type Visao = keyof typeof VISOES

export const ORDENS = {
    '-abertaEm': 'Mais recentes',
    abertaEm: 'Mais antigas',
    prazoResolucao: 'Prazo mais próximo',
    '-prioridade': 'Mais urgentes',
    '-numero': 'Número',
} as const

export type Ordem = keyof typeof ORDENS

/** Filtros no endereço (`?visao=&cliente=&responsavel=&...`); valores padrão omitidos. */
export interface FiltrosFila {
    visao?: Visao
    busca?: string
    cliente?: string
    /** Id da pessoa ou `eu`. */
    responsavel?: string
    prioridade?: Prioridade
    categoria?: string
    sla?: 'EM_RISCO' | 'VIOLADO'
    ordem?: Ordem
    pagina?: number
}

export const TAMANHO_PAGINA = 50

export function consultaFila(filtros: FiltrosFila, usuarioId: string) {
    const status = VISOES[filtros.visao ?? 'abertas'].status
    return queryOptions({
        queryKey: ['solicitacoes', 'lista', filtros, filtros.responsavel === 'eu' ? usuarioId : null],
        queryFn: async () =>
            (
                await api.GET('/api/v1/solicitacoes', {
                    params: {
                        query: {
                            status: status.length ? status.join(',') : undefined,
                            busca: filtros.busca,
                            clienteId: filtros.cliente,
                            responsavelId: filtros.responsavel === 'eu' ? usuarioId : filtros.responsavel,
                            prioridade: filtros.prioridade,
                            categoriaId: filtros.categoria,
                            situacaoSla: filtros.sla,
                            ordem: filtros.ordem,
                            pagina: filtros.pagina ?? 1,
                            tamanhoPagina: TAMANHO_PAGINA,
                        },
                    },
                })
            ).data!,
        placeholderData: keepPreviousData,
    })
}

export const consultaCategorias = queryOptions({
    queryKey: ['categorias'],
    queryFn: async () => (await api.GET('/api/v1/categorias')).data!,
    staleTime: 5 * 60_000,
})
