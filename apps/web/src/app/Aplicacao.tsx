import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createRouter, RouterProvider } from '@tanstack/react-router'
import { routeTree } from '../routeTree.gen'
import { RegiaoAvisos } from '../shared/avisos/RegiaoAvisos'
import { limparCacheAoTrocarSessao } from '../shared/sessao/cacheDaSessao'
import { iniciarSessao } from '../shared/sessao/sessao'
import { ErroTela } from '../shared/ui/ErroTela'
// Registra o renovador da sessão no cliente HTTP antes da primeira renovação.
import '../shared/api/cliente'

const consultas = new QueryClient({
    defaultOptions: {
        // Revalida ao focar a janela: cobre sinal de tempo real perdido (ADR 0009).
        queries: { staleTime: 30_000, refetchOnWindowFocus: true, retry: 1 },
    },
})

export const roteador = createRouter({
    routeTree,
    context: { consultas },
    defaultPreload: 'intent',
    defaultPendingMs: 300,
    defaultErrorComponent: ErroTela,
})

declare module '@tanstack/react-router' {
    interface Register {
        router: typeof roteador
    }
}

limparCacheAoTrocarSessao(consultas, () => void roteador.invalidate())
void iniciarSessao()

export function Aplicacao() {
    return (
        <QueryClientProvider client={consultas}>
            <RouterProvider router={roteador} />
            <RegiaoAvisos />
        </QueryClientProvider>
    )
}
