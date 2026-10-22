import { createFileRoute } from '@tanstack/react-router'
import { TelaHoje } from '@/features/hoje/TelaHoje'

/** Início pessoal (protótipo "Hoje", F6a). */
export const Route = createFileRoute('/_app/')({ component: Hoje })

function Hoje() {
    const { perfil } = Route.useRouteContext()
    return <TelaHoje perfil={perfil} />
}
