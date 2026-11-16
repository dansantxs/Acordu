/**
 * Configuração de build do SPA (variáveis `VITE_*`; deploy-ci §4, spec do F8). Sem elas, o comportamento é o do
 * desenvolvimento: API na mesma origem (proxy do Vite) e tempo real por SSE.
 */

/** Base da API com barra no fim: `VITE_URL_API` (produção: `https://api.acordu.app.br`) ou a origem do próprio SPA. */
export function baseDaApi(urlApi: string | undefined, origem: string): string {
    const base = urlApi?.trim() || origem
    return base.endsWith('/') ? base : `${base}/`
}

export const BASE_API = baseDaApi(import.meta.env.VITE_URL_API, globalThis.location.origin)

/**
 * Como as telas ficam atualizadas (ADR 0010 §4): `sse` (padrão; conexão com `/api/v1/eventos`) ou `polling` (nível 0
 * no Cloud Run: sem SSE, a Agenda e o Quadro recarregam a cada 30 s com a aba visível).
 */
export type ModoTempoReal = 'sse' | 'polling'

export const modoTempoReal = (valor: string | undefined): ModoTempoReal => (valor === 'polling' ? 'polling' : 'sse')

export const MODO_TEMPO_REAL = modoTempoReal(import.meta.env.VITE_TEMPO_REAL)

/**
 * Chave do site do Cloudflare Turnstile (`VITE_TURNSTILE_CHAVE`, pública; RNF-SEG-10). Sem ela, o cadastro não mostra o
 * desafio: em dev, testes e E2E a API também fica sem o segredo (spec do F9, decisão 5).
 */
export const chaveTurnstile = (valor: string | undefined): string | undefined => valor?.trim() || undefined

export const CHAVE_TURNSTILE = chaveTurnstile(import.meta.env.VITE_TURNSTILE_CHAVE)

/**
 * `refetchInterval` das telas que acompanham a equipe ao vivo (Agenda e Quadro). O TanStack Query não recarrega com a
 * aba escondida (`refetchIntervalInBackground` falso por padrão).
 */
export const intervaloDePolling = (modo: ModoTempoReal = MODO_TEMPO_REAL): number | false =>
    modo === 'polling' ? 30_000 : false

/**
 * Versão do build (`VITE_VERSAO`, o commit no deploy; spec do F13, F13d): vai junto do relato de erro. Sem ela, o app não
 * relata erros (desenvolvimento, testes, E2E: F13-21).
 */
export function versaoDoBuild(valor: string | undefined): string | undefined {
    const versao = valor?.trim()
    if (!versao) return undefined
    // O commit inteiro vira o curto, como o `App__Versao` da API: a mesma versão nos dois serviços.
    return /^[0-9a-f]{40}$/i.test(versao) ? versao.slice(0, 7) : versao.slice(0, 40)
}

export const VERSAO = versaoDoBuild(import.meta.env.VITE_VERSAO)

/**
 * Canal da Nebrys para ativar, cancelar e falar sobre a conta (`VITE_CONTATO_NEBRYS`; decisão 14 do G2 e D15 do F14):
 * em produção, a variável `CONTATO_NEBRYS` do GitHub. Sem ela (desenvolvimento, E2E, bot do manual), as faixas dizem só
 * "fale com a Nebrys", sem endereço.
 */
export const contatoNebrys = (valor: string | undefined): string | undefined => valor?.trim() || undefined

export const CONTATO_NEBRYS = contatoNebrys(import.meta.env.VITE_CONTATO_NEBRYS)
