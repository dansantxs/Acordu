import { createFileRoute } from '@tanstack/react-router'
import { EmBreve } from '@/features/casca/EmBreve'

export const Route = createFileRoute('/_app/recorrencias')({
    component: () => <EmBreve titulo="Recorrências" sobre="Trabalhos que se repetem e as próximas ocorrências." />,
})
