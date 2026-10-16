import { createFileRoute } from '@tanstack/react-router'
import { ListaClientes } from '@/features/clientes/ListaClientes'
import type { FiltrosClientes } from '@/features/clientes/consultas'

export const Route = createFileRoute('/_app/clientes/')({
    validateSearch: (busca: Record<string, unknown>): FiltrosClientes => ({
        busca: typeof busca.busca === 'string' && busca.busca.trim() ? busca.busca.trim() : undefined,
        inativos: busca.inativos === true || busca.inativos === 'true' || busca.inativos === 1 ? true : undefined,
        pagina: Number.isInteger(Number(busca.pagina)) && Number(busca.pagina) > 1 ? Number(busca.pagina) : undefined,
    }),
    component: Clientes,
})

function Clientes() {
    const { perfil } = Route.useRouteContext()
    const filtros = Route.useSearch()
    return <ListaClientes perfil={perfil} filtros={filtros} />
}
