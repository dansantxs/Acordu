import createClient, { type Middleware } from 'openapi-fetch'
import type { paths as PathsInterno } from './interno'
import type { paths as PathsPortal } from './portal'
import { lerProblema } from './problema'

/**
 * Cliente HTTP central (convenções §4): base `/api/v1`, Bearer do access token em memória e Problem Details virando
 * `ErroApi`. O token vem de quem cuida da sessão (F1b); aqui só se pede a ele.
 */
export type FonteDeToken = () => string | null

let fonteDeToken: FonteDeToken = () => null

export function definirFonteDeToken(fonte: FonteDeToken): void {
    fonteDeToken = fonte
}

const autenticacao: Middleware = {
    onRequest({ request }) {
        const token = fonteDeToken()
        if (token) request.headers.set('Authorization', `Bearer ${token}`)
        return request
    },
    async onResponse({ response }) {
        if (!response.ok) throw await lerProblema(response)
        return response
    },
}

/** Cabeçalho `If-Match` com a versão (`versao` dos DTOs de detalhe), para edições com concorrência otimista. */
export function seVersao(versao: string): { headers: { 'If-Match': string } } {
    return { headers: { 'If-Match': `"${versao}"` } }
}

export const api = createClient<PathsInterno>({ baseUrl: '/' })
api.use(autenticacao)

/** O portal tem cliente próprio, só com as rotas `/api/v1/portal` (estrutura §6). */
export const apiPortal = createClient<PathsPortal>({ baseUrl: '/' })
apiPortal.use(autenticacao)
