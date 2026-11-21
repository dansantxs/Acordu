import createClient, { type Middleware } from 'openapi-fetch'
import type { components, paths } from '@/shared/api/nebrys'
import { BASE_API } from '@/shared/config/ambiente'
import { ErroApi, lerProblema } from '@/shared/api/problema'
import { encerrarSessaoOperacao, sessaoOperacao } from '@/shared/sessao/sessaoOperacao'

/**
 * Cliente da área da Nebrys (ADR 0014; contrato §17): só as rotas de `openapi/nebrys.json`, com o Bearer da sessão de
 * operação (`shared/sessao/sessaoOperacao`). Não renova, não manda cookie (`omit`) e não toca a sessão da equipe nem a
 * do portal. Token que já passou do prazo encerra a sessão antes de sair a requisição: a API responderia `404` (a
 * barreira não distingue), e a área volta para a entrada.
 */
export type EmpresaResumo = components['schemas']['EmpresaResumoNebrysDto']
export type Empresa = components['schemas']['EmpresaNebrysDto']
export type SituacaoEmpresa = components['schemas']['SituacaoEmpresa']

/** Recusa local: a sessão de operação acabou (prazo) antes de a requisição sair. */
export class SessaoOperacaoEncerrada extends Error {
    constructor() {
        super('A sessão da área acabou. Entre de novo.')
        this.name = 'SessaoOperacaoEncerrada'
    }
}

// Pela global a cada chamada, para os testes poderem trocar o fetch.
const buscar = (requisicao: Request) => globalThis.fetch(requisicao)

const ROTA_SESSAO = '/api/v1/nebrys/sessao'

const operacao: Middleware = {
    onRequest({ request, schemaPath }) {
        if (schemaPath === ROTA_SESSAO) return request
        const atual = sessaoOperacao()
        if (!atual) throw new SessaoOperacaoEncerrada()
        request.headers.set('Authorization', `Bearer ${atual.accessToken}`)
        return request
    },
    async onResponse({ response }) {
        if (!response.ok) throw await lerProblema(response)
        return response
    },
}

export const apiNebrys = createClient<paths>({ baseUrl: BASE_API, fetch: buscar, credentials: 'omit' })
apiNebrys.use(operacao)

/** A API recusou a sessão (a barreira da área responde `404` a token vencido ou conta fora da lista). */
export function ehSessaoRecusada(erro: unknown): boolean {
    return erro instanceof SessaoOperacaoEncerrada || (erro instanceof ErroApi && erro.status === 404)
}

/** Encerra a sessão quando a lista (que só a barreira recusa com `404`) foi recusada. */
export function encerrarSeRecusada(erro: unknown): void {
    if (ehSessaoRecusada(erro)) encerrarSessaoOperacao('expirada')
}
