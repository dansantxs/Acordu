import { createFileRoute } from '@tanstack/react-router'
import { PainelSolicitacao } from '@/features/solicitacoes/PainelSolicitacao'

export const Route = createFileRoute('/_app/solicitacoes/$id')({ component: Solicitacao })

function Solicitacao() {
    const { perfil } = Route.useRouteContext()
    const { id } = Route.useParams()
    // `key`: trocar de solicitação na fila não reaproveita janelas abertas da anterior.
    return <PainelSolicitacao key={id} perfil={perfil} id={id} />
}
