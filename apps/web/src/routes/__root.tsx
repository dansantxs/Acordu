import type { QueryClient } from '@tanstack/react-query'
import { createRootRouteWithContext, Outlet } from '@tanstack/react-router'

export interface ContextoRotas {
    consultas: QueryClient
}

export const Route = createRootRouteWithContext<ContextoRotas>()({
    component: () => <Outlet />,
    pendingComponent: () => (
        <main className="grid min-h-screen place-items-center p-8" aria-busy="true">
            <span className="font-titulo text-2xl italic text-suave">acordu</span>
        </main>
    ),
    notFoundComponent: () => (
        <main className="grid min-h-screen place-items-center p-8">
            <p className="font-titulo text-2xl">Página não encontrada.</p>
        </main>
    ),
})
