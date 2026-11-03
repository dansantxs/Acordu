import createClient, { type Middleware } from 'openapi-fetch'
import { definirRenovador, encerrarSessao, renovar, tokenAtual, tokenVencendo } from '../sessao/sessao'
import type { paths as PathsInterno } from './interno'
import type { paths as PathsPortal } from './portal'
import { BASE_API } from '../config/ambiente'
import { lerProblema } from './problema'
import { encerrarSessaoLink, sessaoLink } from '../sessao/sessaoLink'

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

// Absoluta: o `Request` exige URL completa fora do navegador (testes). Em produção a API fica em outra origem do mesmo
// site (ADR 0010 §3): `VITE_URL_API`.
const base = BASE_API

// Com a API em outra origem, o cookie da sessão (`acordu_refresh`, restrito a `/api/v1/auth`) só vai com `include`; nas
// demais rotas o caminho do cookie já impede que ele seja enviado. Com a mesma origem, `include` equivale a
// `same-origin`.
const credenciais: RequestCredentials = 'include'

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

export const api = createClient<PathsInterno>({ baseUrl: base, fetch: buscar, credentials: credenciais })
api.use(autenticacao)

/** O portal tem cliente próprio, só com as rotas `/api/v1/portal` (estrutura §6). */
export const apiPortal = createClient<PathsPortal>({ baseUrl: base, fetch: buscar, credentials: credenciais })
apiPortal.use(autenticacao)

/**
 * Cliente do portal na sessão por link: o Bearer é o da `sessaoLink`, sem renovar; um 401 encerra a sessão por link
 * (a tela troca para "abra o link de novo").
 */
const autenticacaoLink: Middleware = {
    onRequest({ request }) {
        const atual = sessaoLink()
        if (atual) request.headers.set('Authorization', `Bearer ${atual.accessToken}`)
        return request
    },
    async onResponse({ response }) {
        if (response.status === 401) encerrarSessaoLink('expirado')
        if (!response.ok) throw await lerProblema(response)
        return response
    },
}

export const apiLink = createClient<PathsPortal>({ baseUrl: base, fetch: buscar, credentials: credenciais })
apiLink.use(autenticacaoLink)

// O refresh vai sem Bearer (só o cookie) e por fora do middleware: um 401 aqui é a resposta, não motivo para renovar.
definirRenovador(async () => {
    const resposta = await buscar(
        new Request(`${base}api/v1/auth/refresh`, { method: 'POST', credentials: credenciais }),
    )
    if (resposta.status === 401) return null
    if (!resposta.ok) throw await lerProblema(resposta)
    return (await resposta.json()) as { accessToken: string; expiraEm: string }
})
