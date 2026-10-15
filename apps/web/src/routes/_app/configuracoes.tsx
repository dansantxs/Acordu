import { createFileRoute } from '@tanstack/react-router'
import { EmBreve } from '@/features/casca/EmBreve'

export const Route = createFileRoute('/_app/configuracoes')({
    component: () => (
        <EmBreve titulo="Configurações" sobre="Empresa, horário e feriados, equipe e papéis, portal e auditoria." />
    ),
})
