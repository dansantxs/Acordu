import { queryOptions } from '@tanstack/react-query'
import { api } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'

export type Atividade = components['schemas']['AtividadeDto']

/** Tipos do feed que viram linha de histórico na conversa (as mensagens já aparecem como interações). */
export const TIPOS_HISTORICO: Atividade['tipo'][] = [
    'SOLICITACAO_ABERTA',
    'SOLICITACAO_ATRIBUIDA',
    'SOLICITACAO_STATUS',
]

/**
 * Histórico da solicitação (`GET /atividades?solicitacaoId=`, contrato §11f): a página mais recente (50 linhas), que
 * cobre o caso comum; o feed guarda 90 dias.
 */
export function consultaHistorico(id: string) {
    return queryOptions({
        queryKey: ['solicitacoes', 'historico', id],
        queryFn: async () =>
            (await api.GET('/api/v1/atividades', { params: { query: { solicitacaoId: id } } })).data!.itens.filter(
                (a) => TIPOS_HISTORICO.includes(a.tipo),
            ),
    })
}
