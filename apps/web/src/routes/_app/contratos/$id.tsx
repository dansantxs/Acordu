import { createFileRoute } from '@tanstack/react-router'
import { PaginaContrato } from '@/features/contratos/PaginaContrato'

/** Detalhe do contrato em página própria (decisão 2 do F4); `?mes=` escolhe o mês do consumo. */
export const Route = createFileRoute('/_app/contratos/$id')({
    validateSearch: (busca: Record<string, unknown>): { mes?: string } => ({
        mes: typeof busca.mes === 'string' && /^\d{4}-\d{2}$/.test(busca.mes) ? busca.mes : undefined,
    }),
    component: Contrato,
})

function Contrato() {
    const { perfil } = Route.useRouteContext()
    const { id } = Route.useParams()
    const { mes } = Route.useSearch()
    return <PaginaContrato key={id} perfil={perfil} id={id} mes={mes} />
}
