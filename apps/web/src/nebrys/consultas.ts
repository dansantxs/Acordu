import { queryOptions } from '@tanstack/react-query'
import { apiNebrys, repetirConsulta, type SituacaoEmpresa } from './api'

/** Filtros da lista no endereço (convenções §4). `fase` e não `situacao`: o nome já é das abas dos projetos. */
export interface FiltrosEmpresas {
    fase?: SituacaoEmpresa
    pagina?: number
}

export const TAMANHO_PAGINA = 25

/** Chaves da área começam por `nebrys`, como as do portal por `portal`: sair da área remove todas. */
export const consultaEmpresas = (filtros: FiltrosEmpresas) =>
    queryOptions({
        queryKey: ['nebrys', 'empresas', { fase: filtros.fase, pagina: filtros.pagina ?? 1 }],
        queryFn: async () => {
            const { data } = await apiNebrys.GET('/api/v1/nebrys/empresas', {
                params: {
                    query: { situacao: filtros.fase, pagina: filtros.pagina ?? 1, tamanhoPagina: TAMANHO_PAGINA },
                },
            })
            return data!
        },
        retry: repetirConsulta,
    })

export const consultaEmpresa = (id: string) =>
    queryOptions({
        queryKey: ['nebrys', 'empresas', 'detalhe', id],
        queryFn: async () => {
            const { data } = await apiNebrys.GET('/api/v1/nebrys/empresas/{id}', { params: { path: { id } } })
            return data!
        },
        // A versão do detalhe vai no If-Match das ações: lida de novo ao voltar à aba, não guardada por muito tempo.
        staleTime: 0,
        retry: repetirConsulta,
    })

/** Página das listas de convites e da lista de espera, no endereço (`?pagina=`). */
export interface FiltroPagina {
    pagina?: number
}

/** `?pagina=` válido (inteiro maior que 1) ou nada: a primeira página fica fora do endereço. */
export function paginaDaBusca(busca: Record<string, unknown>): number | undefined {
    const pagina = Number(busca.pagina)
    return Number.isInteger(pagina) && pagina > 1 ? pagina : undefined
}

/** Convites de empresa, os emitidos por último primeiro (contrato §17). */
export const consultaConvites = (filtro: FiltroPagina) =>
    queryOptions({
        queryKey: ['nebrys', 'convites', { pagina: filtro.pagina ?? 1 }],
        queryFn: async () => {
            const { data } = await apiNebrys.GET('/api/v1/nebrys/convites', {
                params: { query: { pagina: filtro.pagina ?? 1, tamanhoPagina: TAMANHO_PAGINA } },
            })
            return data!
        },
        retry: repetirConsulta,
    })

export const consultaConvite = (id: string) =>
    queryOptions({
        queryKey: ['nebrys', 'convites', 'detalhe', id],
        queryFn: async () => {
            const { data } = await apiNebrys.GET('/api/v1/nebrys/convites/{id}', { params: { path: { id } } })
            return data!
        },
        staleTime: 0,
        retry: repetirConsulta,
    })

/** Lista de espera, por ordem de chegada (contrato §17). */
export const consultaListaEspera = (filtro: FiltroPagina) =>
    queryOptions({
        queryKey: ['nebrys', 'lista-espera', { pagina: filtro.pagina ?? 1 }],
        queryFn: async () => {
            const { data } = await apiNebrys.GET('/api/v1/nebrys/lista-espera', {
                params: { query: { pagina: filtro.pagina ?? 1, tamanhoPagina: TAMANHO_PAGINA } },
            })
            return data!
        },
        // As ações relêem a lista: sem cache velho ao voltar para a tela.
        staleTime: 0,
        retry: repetirConsulta,
    })
