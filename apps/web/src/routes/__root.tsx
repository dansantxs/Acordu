import type { QueryClient } from '@tanstack/react-query'
import { createRootRouteWithContext, Outlet } from '@tanstack/react-router'

export interface ContextoRotas {
    consultas: QueryClient
}

export const Route = createRootRouteWithContext<ContextoRotas>()({
    component: () => <Outlet />,
    notFoundComponent: () => (
        <main className="grid min-h-screen place-items-center p-8">
            <p className="font-titulo text-2xl">Página não encontrada.</p>
        </main>
    ),
})
