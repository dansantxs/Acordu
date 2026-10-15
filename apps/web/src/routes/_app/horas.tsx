import { createFileRoute } from '@tanstack/react-router'
import { EmBreve } from '@/features/casca/EmBreve'

export const Route = createFileRoute('/_app/horas')({
    component: () => <EmBreve titulo="Horas" sobre="Apontamentos de tempo: os seus e, com permissão, os da equipe." />,
})
