import { createFileRoute } from '@tanstack/react-router'
import { SecaoCalendario } from '@/features/configuracoes/SecaoCalendario'
import { SecaoProtegida } from '@/features/configuracoes/TelaConfiguracoes'

export const Route = createFileRoute('/_app/configuracoes/calendario')({ component: Pagina })

function Pagina() {
    const { perfil } = Route.useRouteContext()
    return (
        <SecaoProtegida perfil={perfil} para="/configuracoes/calendario">
            <SecaoCalendario />
        </SecaoProtegida>
    )
}
