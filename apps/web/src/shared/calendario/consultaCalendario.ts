import { queryOptions } from '@tanstack/react-query'
import { api } from '../api/cliente'

/** Calendário útil da empresa (ADR 0003): fuso, expediente por dia e feriados. Lido por qualquer pessoa da equipe. */
export const consultaCalendario = queryOptions({
    queryKey: ['configuracoes', 'calendario'],
    queryFn: async () => (await api.GET('/api/v1/configuracoes/calendario')).data!,
})
