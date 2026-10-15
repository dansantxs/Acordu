import { createFileRoute } from '@tanstack/react-router'
import { EmBreve } from '@/features/casca/EmBreve'

export const Route = createFileRoute('/_app/solicitacoes/')({
    component: () => (
        <EmBreve
            titulo="Solicitações"
            sobre="A fila de atendimento, com prazos de SLA, responsáveis e o que espera o cliente."
        />
    ),
})
