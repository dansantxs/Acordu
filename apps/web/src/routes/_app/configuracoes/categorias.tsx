import { createFileRoute } from '@tanstack/react-router'
import { SecaoCategorias } from '@/features/configuracoes/SecaoCategorias'
import { SecaoProtegida } from '@/features/configuracoes/TelaConfiguracoes'

export const Route = createFileRoute('/_app/configuracoes/categorias')({ component: Pagina })

function Pagina() {
    const { perfil } = Route.useRouteContext()
    return (
        <SecaoProtegida perfil={perfil} para="/configuracoes/categorias">
            <SecaoCategorias />
        </SecaoProtegida>
    )
}
