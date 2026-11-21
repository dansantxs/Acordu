import { queryOptions } from '@tanstack/react-query'
import { apiNebrys, type SituacaoEmpresa } from './api'

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
    })
