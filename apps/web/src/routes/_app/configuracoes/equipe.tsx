import { createFileRoute } from '@tanstack/react-router'
import { SecaoEmBreve, SecaoProtegida } from '@/features/configuracoes/TelaConfiguracoes'

export const Route = createFileRoute('/_app/configuracoes/equipe')({ component: Pagina })

function Pagina() {
    const { perfil } = Route.useRouteContext()
    return (
        <SecaoProtegida perfil={perfil} para="/configuracoes/equipe">
            <SecaoEmBreve
                titulo="Equipe"
                sobre="Pessoas da equipe, convites pendentes, papel e capacidade semanal de cada uma."
            />
        </SecaoProtegida>
    )
}
