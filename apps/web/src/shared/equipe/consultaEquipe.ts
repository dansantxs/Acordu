import { queryOptions } from '@tanstack/react-query'
import { api } from '../api/cliente'

/** Pessoas da equipe (`GET /equipe`, só Id e nome): responsável, menções e filtros. Muda pouco: 5 min. */
export const consultaEquipe = queryOptions({
    queryKey: ['equipe'],
    queryFn: async () => (await api.GET('/api/v1/equipe')).data!,
    staleTime: 5 * 60_000,
})
