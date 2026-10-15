import { createFileRoute } from '@tanstack/react-router'
import { EmBreve } from '@/features/casca/EmBreve'

// Destino da paleta e dos avisos; o detalhe chega no F2.
export const Route = createFileRoute('/_app/solicitacoes/$id')({
    component: () => (
        <EmBreve titulo="Solicitação" sobre="Conversa com o cliente, tarefas, apontamentos e histórico." />
    ),
})
