import { createFileRoute } from '@tanstack/react-router'
import { PainelTarefa } from '@/features/tarefas/PainelTarefa'

export const Route = createFileRoute('/_app/tarefas/$id')({ component: Tarefa })

function Tarefa() {
    const { perfil } = Route.useRouteContext()
    const { id } = Route.useParams()
    return <PainelTarefa key={id} perfil={perfil} id={id} />
}
