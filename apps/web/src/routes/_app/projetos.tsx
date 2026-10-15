import { createFileRoute } from '@tanstack/react-router'
import { EmBreve } from '@/features/casca/EmBreve'

export const Route = createFileRoute('/_app/projetos')({
    component: () => <EmBreve titulo="Projetos" sobre="Projetos com início, prazo e tarefas." />,
})
