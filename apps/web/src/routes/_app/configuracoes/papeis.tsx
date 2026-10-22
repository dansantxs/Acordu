import { createFileRoute } from '@tanstack/react-router'
import { SecaoEmBreve, SecaoProtegida } from '@/features/configuracoes/TelaConfiguracoes'

export const Route = createFileRoute('/_app/configuracoes/papeis')({ component: Pagina })

function Pagina() {
    const { perfil } = Route.useRouteContext()
    return (
        <SecaoProtegida perfil={perfil} para="/configuracoes/papeis">
            <SecaoEmBreve titulo="Papéis" sobre="Papéis sob medida, com as permissões escolhidas por área." />
        </SecaoProtegida>
    )
}
