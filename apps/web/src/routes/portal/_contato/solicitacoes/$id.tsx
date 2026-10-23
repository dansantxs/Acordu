import { createFileRoute } from '@tanstack/react-router'
import { DetalhePortal } from '@/features/portal/DetalhePortal'

export const Route = createFileRoute('/portal/_contato/solicitacoes/$id')({ component: Detalhe })

function Detalhe() {
    const { perfil } = Route.useRouteContext()
    const { id } = Route.useParams()
    return <DetalhePortal key={id} id={id} fuso={perfil.tenant.fusoHorario} />
}
