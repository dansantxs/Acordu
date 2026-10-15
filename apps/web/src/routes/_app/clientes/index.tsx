import { createFileRoute } from '@tanstack/react-router'
import { EmBreve } from '@/features/casca/EmBreve'

export const Route = createFileRoute('/_app/clientes/')({
    component: () => (
        <EmBreve titulo="Clientes" sobre="Clientes, contatos, acesso ao portal, contratos e arquivos de cada um." />
    ),
})
