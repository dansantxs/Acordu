import { createFileRoute } from '@tanstack/react-router'
import { TelaConta } from '@/features/conta/TelaConta'

export const Route = createFileRoute('/_app/conta')({ component: Conta })

function Conta() {
    const { perfil } = Route.useRouteContext()
    return <TelaConta perfil={perfil} />
}
