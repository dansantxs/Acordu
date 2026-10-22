import { createFileRoute } from '@tanstack/react-router'
import { SecaoEquipe } from '@/features/configuracoes/SecaoEquipe'
import { SecaoProtegida } from '@/features/configuracoes/TelaConfiguracoes'

export const Route = createFileRoute('/_app/configuracoes/equipe')({ component: Pagina })

function Pagina() {
    const { perfil } = Route.useRouteContext()
    return (
        <SecaoProtegida perfil={perfil} para="/configuracoes/equipe">
            <SecaoEquipe perfil={perfil} />
        </SecaoProtegida>
    )
}
