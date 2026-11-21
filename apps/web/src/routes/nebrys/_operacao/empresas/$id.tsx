import { createFileRoute } from '@tanstack/react-router'
import { DetalheEmpresa } from '@/nebrys/DetalheEmpresa'

export const Route = createFileRoute('/nebrys/_operacao/empresas/$id')({ component: Empresa })

function Empresa() {
    const { id } = Route.useParams()
    return <DetalheEmpresa key={id} id={id} />
}
