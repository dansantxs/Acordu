import { useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { consultaTarefasDoProjeto } from '@/features/tarefas/consultas'
import { avisar } from '@/shared/avisos/avisos'
import { consultaProjeto } from './consultas'

/**
 * Depois de concluir uma tarefa (painel ou quadro): se era a última aberta de um projeto em andamento, sugere concluí-lo,
 * com o botão que abre a confirmação (F11-28). Só para quem tem `projeto.gerenciar`; ignorar não muda nada.
 */
export function useSugestaoConclusaoProjeto(podeGerenciarProjeto: boolean) {
    const consultas = useQueryClient()
    const navegar = useNavigate()
    return async (tarefa: { projetoId?: string | null }) => {
        if (!podeGerenciarProjeto || !tarefa.projetoId) return
        const id = tarefa.projetoId
        try {
            const [projeto, tarefas] = await Promise.all([
                consultas.fetchQuery({ ...consultaProjeto(id), staleTime: 0 }),
                consultas.fetchQuery({ ...consultaTarefasDoProjeto(id), staleTime: 0 }),
            ])
            if (
                projeto.status !== 'EM_ANDAMENTO' ||
                tarefas.some((t) => t.status === 'A_FAZER' || t.status === 'FAZENDO')
            )
                return
            avisar('Era a última tarefa do projeto. Concluir o projeto agora?', {
                acao: {
                    rotulo: 'Concluir projeto',
                    executar: () => navegar({ to: '/projetos/$id', params: { id }, search: { concluir: true } }),
                },
            })
        } catch {
            // Sugestão é conveniência: falhar ao consultar não atrapalha a conclusão da tarefa.
        }
    }
}
