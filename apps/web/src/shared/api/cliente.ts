import createClient, { type Middleware } from 'openapi-fetch'
import { definirRenovador, encerrarSessao, renovar, tokenAtual, tokenVencendo } from '../sessao/sessao'
import type { paths as PathsInterno } from './interno'
import type { paths as PathsPortal } from './portal'
import { lerProblema } from './problema'

/**
 * Cliente HTTP central (convenções §4): base `/api/v1`, Bearer do access token em memória (`shared/sessao`) e Problem
 * Details virando `ErroApi`.
 *
 * Rotas com sessão renovam o token antes de enviar, se ele está vencendo, e repetem uma vez depois de um 401 (laptop
 * que dormiu, relógio adiantado). As rotas de `/auth` que abrem ou dispensam sessão ficam fora disso.
 */
const ROTAS_SEM_RENOVACAO = /^\/api\/v1\/auth\/(?!eu$|tenant-ativo$|confirmacao-email\/reenvio$)/

// Pela global a cada chamada (e não a capturada na criação), para os testes poderem trocar o fetch.
const buscar = (requisicao: Request) => globalThis.fetch(requisicao)

// Absoluta: o `Request` exige URL completa fora do navegador (testes); no navegador é a mesma origem do SPA.
const base = `${globalThis.location.origin}/`

const copias = new Map<string, Request>()

function comToken(requisicao: Request): Request {
    const token = tokenAtual()
    if (token) requisicao.headers.set('Authorization', `Bearer ${token}`)
    return requisicao
}

const autenticacao: Middleware = {
    async onRequest({ request, schemaPath, id }) {
        if (ROTAS_SEM_RENOVACAO.test(schemaPath)) return comToken(request)
        if (tokenVencendo()) await renovar()
        const pronta = comToken(request)
        copias.set(id, pronta.clone())
        return pronta
    },
    async onResponse({ response, id }) {
        const copia = copias.get(id)
        copias.delete(id)
        let final = response
        if (response.status === 401 && copia && tokenAtual()) {
            if (await renovar()) {
                final = await buscar(comToken(copia))
                // Recusou o token recém-renovado: o acesso acabou (vínculo revogado, contato inativado).
                if (final.status === 401) encerrarSessao()
            }
        }
        if (!final.ok) throw await lerProblema(final)
        return final
    },
    onError({ id }) {
        copias.delete(id)
    },
}

/** Cabeçalho `If-Match` com a versão (`versao` dos DTOs de detalhe), para edições com concorrência otimista. */
export function seVersao(versao: string): { headers: { 'If-Match': string } } {
    return { headers: { 'If-Match': `"${versao}"` } }
}

export const api = createClient<PathsInterno>({ baseUrl: base, fetch: buscar })
api.use(autenticacao)

/** O portal tem cliente próprio, só com as rotas `/api/v1/portal` (estrutura §6). */
export const apiPortal = createClient<PathsPortal>({ baseUrl: base, fetch: buscar })
apiPortal.use(autenticacao)

// O refresh vai sem Bearer (só o cookie) e por fora do middleware: um 401 aqui é a resposta, não motivo para renovar.
definirRenovador(async () => {
    const resposta = await buscar(
        new Request(`${base}api/v1/auth/refresh`, { method: 'POST', credentials: 'same-origin' }),
    )
    if (resposta.status === 401) return null
    if (!resposta.ok) throw await lerProblema(resposta)
    return (await resposta.json()) as { accessToken: string; expiraEm: string }
})
