/**
 * Relato dos erros do app à API (spec do F13, F13d; RNF-OPS-03): `POST /api/v1/erros-web`, pública, sem login. Vai
 * só o que ajuda a achar o defeito: mensagem, pilha, rota modelo (ids viram `:id`, sem query), versão e navegador. Nada
 * de id de contato, cliente ou link, nem de token; a API ainda limpa e-mails e números.
 *
 * Não relata: em desenvolvimento e testes (sem `VITE_VERSAO`, F13-21), resposta da API (`ErroApi`, a API já registra os
 * dela) e falha de rede (sem internet não é defeito, F13-17). O mesmo erro sai uma vez por carga de página (F13-16).
 */
import { BASE_API, VERSAO } from '../config/ambiente'
import { ehFalhaDeRede } from '../api/mensagens'
import { ErroApi } from '../api/problema'

const LIMITE_MENSAGEM = 500
const LIMITE_PILHA = 4000

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

const relatados = new Set<string>()

export interface RelatoDeErro {
    mensagem: string
    pilha?: string
    rota: string
    versao: string
    navegador: string
}

/** `/clientes/7c1f…/contratos` → `/clientes/:id/contratos`; números e trechos longos também viram `:id`. */
export function rotaModelo(caminho: string): string {
    return caminho
        .split('/')
        .map((parte) => (UUID.test(parte) || /^\d+$/.test(parte) || parte.length > 40 ? ':id' : parte))
        .join('/')
}

/** O relato de um erro, ou `null` quando ele não deve sair. */
export function montarRelato(
    erro: unknown,
    versao: string | null | undefined = VERSAO,
    caminho = globalThis.location?.pathname ?? '/',
): RelatoDeErro | null {
    if (!versao || erro instanceof ErroApi || ehFalhaDeRede(erro)) return null
    const comoErro = erro instanceof Error ? erro : null
    const mensagem = (comoErro ? `${comoErro.name}: ${comoErro.message}` : String(erro)).slice(0, LIMITE_MENSAGEM)
    if (!mensagem.trim()) return null
    return {
        mensagem,
        ...(comoErro?.stack ? { pilha: comoErro.stack.slice(0, LIMITE_PILHA) } : {}),
        rota: rotaModelo(caminho),
        versao,
        navegador: (globalThis.navigator?.userAgent ?? '').slice(0, 200),
    }
}

/** Manda o relato (uma vez por erro e por carga de página). Nunca lança: falhar ao relatar não vira outro erro. */
export function relatarErro(erro: unknown, enviar: typeof fetch = globalThis.fetch): boolean {
    const relato = montarRelato(erro)
    if (!relato) return false
    const chave = `${relato.mensagem}\n${relato.pilha?.split('\n')[1] ?? ''}`
    if (relatados.has(chave)) return false
    relatados.add(chave)
    try {
        void enviar(`${BASE_API}api/v1/erros-web`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(relato),
            // Sem cookie nem token: a rota é pública, e o relato não diz quem é a pessoa.
            credentials: 'omit',
            keepalive: true,
        }).catch(() => undefined)
    } catch {
        // fetch indisponível: fica sem relato.
    }
    return true
}

/** Erros fora do React (`error` e `unhandledrejection` da janela), uma vez, no início do app. */
export function instalarRelatoDeErros(janela: Window = window): void {
    janela.addEventListener('error', (evento) => relatarErro(evento.error ?? evento.message))
    janela.addEventListener('unhandledrejection', (evento) => relatarErro(evento.reason))
}

/** Só para os testes. */
export function esquecerRelatados(): void {
    relatados.clear()
}
