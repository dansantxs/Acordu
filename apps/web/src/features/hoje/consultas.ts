import { queryOptions } from '@tanstack/react-query'
import { api } from '@/shared/api/cliente'

/**
 * Hoje (decisão 5 do F6): o front junta chamadas que já existem, cada bloco com a sua (carrega e falha sozinho). As
 * chaves começam pelo recurso (`solicitacoes`, `tarefas`, `apontamentos`) para o tempo real e as mutações já
 * existentes as invalidarem.
 */
export function consultaMinhasSolicitacoes(usuarioId: string, status: string) {
    return queryOptions({
        queryKey: ['solicitacoes', 'hoje', usuarioId, status],
        queryFn: async () =>
            (
                await api.GET('/api/v1/solicitacoes', {
                    params: {
                        query: { responsavelId: usuarioId, status, ordem: 'prazoResolucao', tamanhoPagina: 20 },
                    },
                })
            ).data!,
    })
}

export const consultaMinhasTarefas = queryOptions({
    queryKey: ['tarefas', 'hoje'],
    queryFn: async () =>
        (
            await api.GET('/api/v1/tarefas', {
                params: { query: { minhas: true, status: 'A_FAZER,FAZENDO', tamanhoPagina: 200 } },
            })
        ).data!,
})

export function consultaMinhaSemana(semana: string) {
    return queryOptions({
        queryKey: ['tarefas', 'capacidade', semana],
        queryFn: async () => (await api.GET('/api/v1/capacidade', { params: { query: { semana } } })).data!,
    })
}
