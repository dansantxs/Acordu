import { createFileRoute } from '@tanstack/react-router'
import type { FiltrosPanorama } from '@/features/panorama/consultas'
import { TelaPanorama } from '@/features/panorama/TelaPanorama'

const data = (v: unknown) => (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(v) ? v : undefined)

export const Route = createFileRoute('/_app/panorama')({
    validateSearch: (busca: Record<string, unknown>): FiltrosPanorama => ({
        periodo: busca.periodo === 'anterior' ? 'anterior' : undefined,
        de: data(busca.de),
        ate: data(busca.ate),
    }),
    component: Panorama,
})

function Panorama() {
    const { perfil } = Route.useRouteContext()
    return <TelaPanorama perfil={perfil} filtros={Route.useSearch()} />
}
