import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createRouter, RouterProvider } from '@tanstack/react-router'
import { routeTree } from '../routeTree.gen'
import { assinar, estadoSessao, iniciarSessao } from '../shared/sessao/sessao'
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
})

declare module '@tanstack/react-router' {
    interface Register {
        router: typeof roteador
    }
}

// Outra empresa, outro usuário ou fim da sessão (nesta aba ou em outra): nada do cache vale mais, e as guardas
// das rotas decidem de novo para onde ir. Renovação comum não mexe em nada.
assinar(() => {
    const { motivo } = estadoSessao()
    if (motivo !== 'troca' && motivo !== 'encerramento') return
    consultas.clear()
    void roteador.invalidate()
})
void iniciarSessao()

export function Aplicacao() {
    return (
        <QueryClientProvider client={consultas}>
            <RouterProvider router={roteador} />
        </QueryClientProvider>
    )
}
