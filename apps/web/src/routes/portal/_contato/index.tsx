import { createFileRoute } from '@tanstack/react-router'
import { GRUPOS, type FiltrosPortal, type Grupo } from '@/features/portal/consultas'
import { ListaPortal } from '@/features/portal/ListaPortal'

export const Route = createFileRoute('/portal/_contato/')({
    validateSearch: (busca: Record<string, unknown>): FiltrosPortal => ({
        grupo:
            Object.keys(GRUPOS).includes(busca.grupo as string) && busca.grupo !== 'andamento'
                ? (busca.grupo as Grupo)
                : undefined,
        pagina: Number.isInteger(Number(busca.pagina)) && Number(busca.pagina) > 1 ? Number(busca.pagina) : undefined,
    }),
    component: Lista,
})

function Lista() {
    const { eu, perfil } = Route.useRouteContext()
    return <ListaPortal eu={eu} filtros={Route.useSearch()} fuso={perfil.tenant.fusoHorario} />
}
