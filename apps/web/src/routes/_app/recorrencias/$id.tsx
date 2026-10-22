import { createFileRoute } from '@tanstack/react-router'
import { PainelRecorrencia } from '@/features/recorrencias/PainelRecorrencia'

export const Route = createFileRoute('/_app/recorrencias/$id')({ component: Recorrencia })

function Recorrencia() {
    const { perfil } = Route.useRouteContext()
    const { id } = Route.useParams()
    return <PainelRecorrencia key={id} perfil={perfil} id={id} />
}
