import { createFileRoute } from '@tanstack/react-router'
import { SecaoPortal } from '@/features/configuracoes/SecaoPortal'
import { SecaoProtegida } from '@/features/configuracoes/TelaConfiguracoes'

export const Route = createFileRoute('/_app/configuracoes/portal')({ component: Pagina })

function Pagina() {
    const { perfil } = Route.useRouteContext()
    return (
        <SecaoProtegida perfil={perfil} para="/configuracoes/portal">
            <SecaoPortal />
        </SecaoProtegida>
    )
}
