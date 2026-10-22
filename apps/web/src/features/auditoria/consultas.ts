import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api } from '@/shared/api/cliente'

/** Filtros no endereço (`/configuracoes/auditoria?de=&ate=&acao=&quem=&alvo=&pagina=`). */
export interface FiltrosAuditoria {
    de?: string
    ate?: string
    acao?: string
    quem?: string
    alvo?: string
    pagina?: number
}

export const TAMANHO_PAGINA = 50

export function consultaAuditoria(filtros: FiltrosAuditoria) {
    return queryOptions({
        queryKey: ['auditoria', filtros],
        queryFn: async () =>
            (
                await api.GET('/api/v1/auditoria', {
                    params: {
                        query: {
                            de: filtros.de,
                            ate: filtros.ate,
                            acao: filtros.acao,
                            usuarioId: filtros.quem,
                            alvoId: filtros.alvo,
                            pagina: filtros.pagina ?? 1,
                            tamanhoPagina: TAMANHO_PAGINA,
                        },
                    },
                })
            ).data!,
        placeholderData: keepPreviousData,
    })
}
