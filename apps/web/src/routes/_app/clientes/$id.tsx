import { createFileRoute } from '@tanstack/react-router'
import { EmBreve } from '@/features/casca/EmBreve'

// Destino da paleta; a ficha do cliente chega no F2.
export const Route = createFileRoute('/_app/clientes/$id')({
    component: () => (
        <EmBreve titulo="Cliente" sobre="Ficha do cliente: contatos, contratos, solicitações e arquivos." />
    ),
})
