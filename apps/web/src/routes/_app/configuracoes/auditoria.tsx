import { createFileRoute } from '@tanstack/react-router'
import { SecaoEmBreve, SecaoProtegida } from '@/features/configuracoes/TelaConfiguracoes'

export const Route = createFileRoute('/_app/configuracoes/auditoria')({ component: Pagina })

function Pagina() {
    const { perfil } = Route.useRouteContext()
    return (
        <SecaoProtegida perfil={perfil} para="/configuracoes/auditoria">
            <SecaoEmBreve
                titulo="Auditoria"
                sobre="Quem fez o quê: entradas, mudanças de papel, acessos ao portal, contratos e apontamentos."
            />
        </SecaoProtegida>
    )
}
