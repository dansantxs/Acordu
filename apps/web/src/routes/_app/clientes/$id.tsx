import { createFileRoute } from '@tanstack/react-router'
import { FichaCliente } from '@/features/clientes/FichaCliente'

export const Route = createFileRoute('/_app/clientes/$id')({ component: Cliente })

function Cliente() {
    const { perfil } = Route.useRouteContext()
    const { id } = Route.useParams()
    // `key`: trocar de cliente pela paleta não reaproveita o estado das janelas do anterior.
    return <FichaCliente key={id} perfil={perfil} id={id} />
}
