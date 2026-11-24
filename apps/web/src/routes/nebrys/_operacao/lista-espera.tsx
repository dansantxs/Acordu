import { createFileRoute } from '@tanstack/react-router'
import { paginaDaBusca, type FiltroPagina } from '@/nebrys/consultas'
import { ListaEspera } from '@/nebrys/ListaEspera'

export const Route = createFileRoute('/nebrys/_operacao/lista-espera')({
    validateSearch: (busca: Record<string, unknown>): FiltroPagina => ({ pagina: paginaDaBusca(busca) }),
    component: Lista,
})

function Lista() {
    return <ListaEspera filtro={Route.useSearch()} />
}
