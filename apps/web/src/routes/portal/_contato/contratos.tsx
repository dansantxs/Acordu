import { createFileRoute, redirect } from '@tanstack/react-router'
import { ContratosPortal, type FiltrosContratosPortal } from '@/features/portal/ContratosPortal'

const id = (v: unknown) => (typeof v === 'string' && /^[0-9a-f-]{36}$/i.test(v) ? v : undefined)

/** Contratos do cliente no portal: só para quem vê contratos (`veContratos`); os outros voltam para a lista. */
export const Route = createFileRoute('/portal/_contato/contratos')({
    validateSearch: (busca: Record<string, unknown>): FiltrosContratosPortal => ({
        contrato: id(busca.contrato),
        mes: typeof busca.mes === 'string' && /^\d{4}-\d{2}$/.test(busca.mes) ? busca.mes : undefined,
    }),
    beforeLoad: ({ context }) => {
        if (!context.eu.veContratos) throw redirect({ to: '/portal' })
    },
    component: Contratos,
})

function Contratos() {
    const { perfil } = Route.useRouteContext()
    return <ContratosPortal filtros={Route.useSearch()} fuso={perfil.tenant.fusoHorario} />
}
