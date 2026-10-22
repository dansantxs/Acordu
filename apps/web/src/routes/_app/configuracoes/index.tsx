import { createFileRoute, redirect } from '@tanstack/react-router'
import { secoesDe } from '@/features/configuracoes/secoes'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'

/** `/configuracoes` abre a primeira seção que o papel vê. */
export const Route = createFileRoute('/_app/configuracoes/')({
    beforeLoad: ({ context }) => {
        const primeira = secoesDe(context.perfil)[0]
        if (primeira) throw redirect({ to: primeira.para, replace: true })
    },
    component: () => (
        <EstadoVazio titulo="Nada para configurar">Seu papel não inclui nenhuma parte das configurações.</EstadoVazio>
    ),
})
