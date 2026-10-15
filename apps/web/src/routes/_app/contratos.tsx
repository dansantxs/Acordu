import { createFileRoute } from '@tanstack/react-router'
import { EmBreve } from '@/features/casca/EmBreve'

export const Route = createFileRoute('/_app/contratos')({
    component: () => (
        <EmBreve titulo="Contratos" sobre="Pacotes de horas, banco de horas e mensalidades, com consumo e vigência." />
    ),
})
