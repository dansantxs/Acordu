import { createFileRoute } from '@tanstack/react-router'
import { SecaoEmpresa } from '@/features/configuracoes/SecaoEmpresa'
import { SecaoProtegida } from '@/features/configuracoes/TelaConfiguracoes'

export const Route = createFileRoute('/_app/configuracoes/empresa')({ component: Pagina })

function Pagina() {
    const { perfil } = Route.useRouteContext()
    return (
        <SecaoProtegida perfil={perfil} para="/configuracoes/empresa">
            <SecaoEmpresa tenant={perfil.tenant} />
        </SecaoProtegida>
    )
}
