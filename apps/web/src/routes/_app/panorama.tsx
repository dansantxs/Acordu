import { createFileRoute } from '@tanstack/react-router'
import { EmBreve } from '@/features/casca/EmBreve'

export const Route = createFileRoute('/_app/panorama')({
    component: () => (
        <EmBreve titulo="Panorama" sobre="Fila, SLA, horas por cliente e consumo dos contratos num período." />
    ),
})
