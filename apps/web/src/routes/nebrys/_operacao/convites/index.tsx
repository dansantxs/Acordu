import { createFileRoute } from '@tanstack/react-router'
import { paginaDaBusca, type FiltroPagina } from '@/nebrys/consultas'
import { ListaConvites } from '@/nebrys/ListaConvites'

export const Route = createFileRoute('/nebrys/_operacao/convites/')({
    validateSearch: (busca: Record<string, unknown>): FiltroPagina => ({ pagina: paginaDaBusca(busca) }),
    component: Convites,
})

function Convites() {
    return <ListaConvites filtro={Route.useSearch()} />
}
