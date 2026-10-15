import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createRouter, RouterProvider } from '@tanstack/react-router'
import { routeTree } from '../routeTree.gen'

const consultas = new QueryClient({
    defaultOptions: {
        // Revalida ao focar a janela: cobre sinal de tempo real perdido (ADR 0009).
        queries: { staleTime: 30_000, refetchOnWindowFocus: true, retry: 1 },
    },
})

export const roteador = createRouter({ routeTree, context: { consultas }, defaultPreload: 'intent' })

declare module '@tanstack/react-router' {
    interface Register {
        router: typeof roteador
    }
}

export function Aplicacao() {
    return (
        <QueryClientProvider client={consultas}>
            <RouterProvider router={roteador} />
        </QueryClientProvider>
    )
}
