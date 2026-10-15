import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render } from '@testing-library/react'
import { vi } from 'vitest'
import { routeTree } from '@/routeTree.gen'
import '@/shared/api/cliente'
import { iniciarSessao } from '@/shared/sessao/sessao'

/** Apoio dos testes de tela: o app inteiro (rotas reais) com a API simulada por caminho. */
export const daquiA = (ms: number) => new Date(Date.now() + ms).toISOString()

export const json = (corpo: unknown, status = 200) =>
    new Response(JSON.stringify(corpo), { status, headers: { 'content-type': 'application/json' } })

export const semSessao = () => new Response(null, { status: 401 })

export const comSessao = () => json({ accessToken: 'a', expiraEm: daquiA(3_600_000) })

export const perfil = {
    usuarioId: 'u1',
    nome: 'Ana Prado',
    email: 'ana@exemplo.com',
    emailConfirmado: true,
    tenant: { id: 't1', nome: 'Prado Consultoria', fusoHorario: 'America/Sao_Paulo' },
    papel: 'Dono',
    permissoes: [],
    clienteId: null,
    escopoPortal: null,
    tenants: [{ id: 't1', nome: 'Prado Consultoria', papel: 'Dono' }],
}

/** Tempo real sem sinais: a conexão fecha e a casca tenta de novo mais tarde (sem afetar o teste). */
const semEventos = () => new Response(null, { status: 204 })

export type Rotas = Record<string, (corpo: unknown) => Response>

/** Registra as chamadas (caminho e corpo JSON) e responde pela função do caminho. */
export function servidor(rotas: Rotas) {
    const chamadas: { caminho: string; corpo: unknown }[] = []
    vi.stubGlobal(
        'fetch',
        vi.fn(async (requisicao: Request) => {
            const caminho = new URL(requisicao.url).pathname
            const texto = await requisicao.text()
            const corpo: unknown = texto ? JSON.parse(texto) : null
            chamadas.push({ caminho, corpo })
            const resposta = rotas[caminho] ?? (caminho === '/api/v1/eventos' ? semEventos : undefined)
            if (!resposta) throw new Error(`Sem resposta para ${caminho}`)
            return resposta(corpo)
        }),
    )
    return chamadas
}

export async function abrir(caminho: string) {
    const roteador = createRouter({
        routeTree,
        context: { consultas: new QueryClient({ defaultOptions: { queries: { retry: false } } }) },
        history: createMemoryHistory({ initialEntries: [caminho] }),
    })
    await iniciarSessao()
    render(
        <QueryClientProvider client={roteador.options.context.consultas}>
            <RouterProvider router={roteador} />
        </QueryClientProvider>,
    )
    return roteador
}
