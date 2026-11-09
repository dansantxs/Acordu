import { createFileRoute } from '@tanstack/react-router'
import { PainelSolicitacao } from '@/features/solicitacoes/PainelSolicitacao'

/** `resolver`: abre direto a janela de resolução (sugestão depois da última tarefa, F11-15). */
export const Route = createFileRoute('/_app/solicitacoes/$id')({
    validateSearch: (busca: Record<string, unknown>): { resolver?: true } => ({
        resolver: busca.resolver === true || busca.resolver === 'true' || busca.resolver === 1 ? true : undefined,
    }),
    component: Solicitacao,
})

function Solicitacao() {
    const { perfil } = Route.useRouteContext()
    const { id } = Route.useParams()
    const { resolver } = Route.useSearch()
    // `key`: trocar de solicitação na fila não reaproveita janelas abertas da anterior.
    return <PainelSolicitacao key={id} perfil={perfil} id={id} abrirResolucao={resolver === true} />
}
