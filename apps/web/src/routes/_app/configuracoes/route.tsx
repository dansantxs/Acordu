import { createFileRoute, Outlet } from '@tanstack/react-router'
import { TelaConfiguracoes } from '@/features/configuracoes/TelaConfiguracoes'

/** Configurações com menu de seções e uma rota por seção (decisão 2 do F5). */
export const Route = createFileRoute('/_app/configuracoes')({ component: Configuracoes })

function Configuracoes() {
    const { perfil } = Route.useRouteContext()
    return (
        <TelaConfiguracoes perfil={perfil}>
            <Outlet />
        </TelaConfiguracoes>
    )
}
