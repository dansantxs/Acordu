import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render } from '@testing-library/react'
import { vi } from 'vitest'
import { routeTree } from '@/routeTree.gen'
import '@/shared/api/cliente'
import { RegiaoAvisos } from '@/shared/avisos/RegiaoAvisos'
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

export type Rotas = Record<string, (corpo: unknown, url: URL) => Response>

export interface Chamada {
    metodo: string
    caminho: string
    consulta: URLSearchParams
    corpo: unknown
    ifMatch: string | null
}

/**
 * Registra as chamadas e responde pela função da rota: primeiro `'MÉTODO /caminho'`, depois só `'/caminho'`
 * (qualquer método).
 */
export function servidor(rotas: Rotas) {
    const chamadas: Chamada[] = []
    vi.stubGlobal(
        'fetch',
        vi.fn(async (requisicao: Request) => {
            const url = new URL(requisicao.url)
            const caminho = url.pathname
            const tipo = requisicao.headers.get('content-type') ?? ''
            // Multipart (anexos) chega como FormData; o resto, JSON.
            const texto = tipo.includes('multipart') ? '' : await requisicao.text()
            const corpo: unknown = tipo.includes('multipart')
                ? await requisicao.formData()
                : texto
                  ? JSON.parse(texto)
                  : null
            chamadas.push({
                metodo: requisicao.method,
                caminho,
                consulta: url.searchParams,
                corpo,
                ifMatch: requisicao.headers.get('If-Match'),
            })
            const resposta =
                rotas[`${requisicao.method} ${caminho}`] ??
                rotas[caminho] ??
                (caminho === '/api/v1/eventos' ? semEventos : undefined)
            if (!resposta) throw new Error(`Sem resposta para ${requisicao.method} ${caminho}`)
            return resposta(corpo, url)
        }),
    )
    return chamadas
}

/** Sessão da Ana com as permissões dadas, sem avisos no sino. */
export function comPerfil(permissoes: string[], rotas: Rotas = {}) {
    return servidor({
        '/api/v1/auth/refresh': comSessao,
        '/api/v1/auth/eu': () => json({ ...perfil, permissoes }),
        '/api/v1/notificacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 10, total: 0, naoLidas: 0 }),
        ...rotas,
    })
}

/** Contato de cliente com sessão de portal (escopo dado), prestador "Prado Consultoria". */
export function comContato(rotas: Rotas = {}, escopoPortal: 'PROPRIAS' | 'TODAS_DO_CLIENTE' = 'TODAS_DO_CLIENTE') {
    return servidor({
        '/api/v1/auth/refresh': comSessao,
        '/api/v1/auth/eu': () =>
            json({ ...perfil, nome: 'Marta Souza', papel: 'Cliente', permissoes: [], clienteId: 'c1', escopoPortal }),
        '/api/v1/portal/eu': () =>
            json({
                contato: { id: 'k1', nome: 'Marta Souza', email: 'marta@bomgrao.com' },
                cliente: { id: 'c1', nome: 'Padaria Bom Grão' },
                prestador: { nome: 'Prado Consultoria', logoUrl: null },
                escopoPortal,
                veContratos: escopoPortal === 'TODAS_DO_CLIENTE',
                solicitacaoId: null,
            }),
        ...rotas,
    })
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
            <RegiaoAvisos />
        </QueryClientProvider>,
    )
    return roteador
}
