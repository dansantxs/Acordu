import { useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { avisar } from '@/shared/avisos/avisos'
import { consultaSolicitacao } from './consultas'

/** Ainda dá para resolver (F11-15): o que não está resolvida, fechada ou cancelada. */
const RESOLVIVEL = ['ABERTA', 'EM_ANDAMENTO', 'AGUARDANDO_CLIENTE', 'AGUARDANDO_TERCEIRO']
const ABERTA = ['A_FAZER', 'FAZENDO']

/**
 * Depois de concluir uma tarefa (painel ou quadro): se era a última aberta da solicitação, sugere resolvê-la, com o
 * botão que abre a janela de resolução (F11-15). Ignorar não muda nada (F11-16); sem `solicitacao.editar`, nada aparece
 * (F11-17). Nunca resolve sozinho: a solução é obrigatória e avisa o cliente (decisão 3).
 */
export function useSugestaoResolucao(podeEditarSolicitacao: boolean) {
    const consultas = useQueryClient()
    const navegar = useNavigate()
    return async (tarefa: { solicitacaoId?: string | null }) => {
        if (!podeEditarSolicitacao || !tarefa.solicitacaoId) return
        const id = tarefa.solicitacaoId
        try {
            const s = await consultas.fetchQuery({ ...consultaSolicitacao(id), staleTime: 0 })
            if (!RESOLVIVEL.includes(s.status) || s.tarefas.some((t) => ABERTA.includes(t.status))) return
            avisar(`Era a última tarefa da solicitação #${s.numero}. Resolver a solicitação agora?`, {
                acao: {
                    rotulo: 'Resolver',
                    executar: () => navegar({ to: '/solicitacoes/$id', params: { id }, search: { resolver: true } }),
                },
            })
        } catch {
            // Sugestão é conveniência: falhar ao consultar não atrapalha a conclusão da tarefa.
        }
    }
}
