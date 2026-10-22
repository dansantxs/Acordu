import { createFileRoute } from '@tanstack/react-router'
import { SecaoPapeis } from '@/features/configuracoes/SecaoPapeis'
import { SecaoProtegida } from '@/features/configuracoes/TelaConfiguracoes'

export const Route = createFileRoute('/_app/configuracoes/papeis')({ component: Pagina })

function Pagina() {
    const { perfil } = Route.useRouteContext()
    return (
        <SecaoProtegida perfil={perfil} para="/configuracoes/papeis">
            <SecaoPapeis />
        </SecaoProtegida>
    )
}
