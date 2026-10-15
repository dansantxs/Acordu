import { createFileRoute } from '@tanstack/react-router'
import { EmBreve } from '@/features/casca/EmBreve'

export const Route = createFileRoute('/_app/agenda')({
    component: () => (
        <EmBreve
            titulo="Agenda"
            sobre="Linha do tempo da equipe, plano da semana e quadro, com prazos, tarefas, projetos, recorrências e marcos de contrato."
        />
    ),
})
