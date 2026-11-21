/**
 * Sessão de operação da área da Nebrys (ADR 0014, item 2; design do F14, "Área no SPA"): o token de 30 min fica **só
 * nesta memória**, separado da sessão da equipe (`sessao.ts`) e da sessão por link (`sessaoLink.ts`). Sem refresh, sem
 * cookie e sem `BroadcastChannel`: recarregar a página ou abrir outra aba pede a senha de novo. Quando o prazo acaba, a
 * sessão se encerra sozinha (motivo `expirada`).
 */
export interface SessaoOperacao {
    accessToken: string
    expiraEm: string
}

/** Por que não há sessão de operação: nunca houve nesta aba, o prazo acabou (ou a API a recusou), ou a pessoa saiu. */
export type MotivoSemOperacao = 'nenhum' | 'expirada' | 'saiu'

let sessao: SessaoOperacao | null = null
let motivo: MotivoSemOperacao = 'nenhum'
let versao = 0
let temporizador: ReturnType<typeof setTimeout> | undefined
const ouvintes = new Set<() => void>()

function avisar(): void {
    versao++
    ouvintes.forEach((o) => o())
}

function expirou(s: SessaoOperacao, agora: number): boolean {
    return new Date(s.expiraEm).getTime() <= agora
}

export function abrirSessaoOperacao(nova: SessaoOperacao): void {
    sessao = nova
    motivo = 'nenhum'
    clearTimeout(temporizador)
    // Sem renovação: no fim do prazo a área volta para a entrada.
    const espera = Math.max(new Date(nova.expiraEm).getTime() - Date.now(), 0)
    temporizador = setTimeout(() => encerrarSessaoOperacao('expirada'), espera)
    avisar()
}

export function encerrarSessaoOperacao(novoMotivo: MotivoSemOperacao): void {
    clearTimeout(temporizador)
    if (!sessao && motivo === novoMotivo) return
    sessao = null
    motivo = novoMotivo
    avisar()
}

/** A sessão de agora, ou `null`; a que já passou do prazo é encerrada aqui mesmo. */
export function sessaoOperacao(agora = Date.now()): SessaoOperacao | null {
    if (sessao && expirou(sessao, agora)) encerrarSessaoOperacao('expirada')
    return sessao
}

export function motivoSemOperacao(): MotivoSemOperacao {
    return motivo
}

export function assinarSessaoOperacao(ouvinte: () => void): () => void {
    ouvintes.add(ouvinte)
    return () => ouvintes.delete(ouvinte)
}

/** Para `useSyncExternalStore`: muda a cada abertura ou encerramento. */
export function versaoSessaoOperacao(): number {
    return versao
}

/** Só para testes. */
export function reiniciarSessaoOperacaoParaTestes(): void {
    clearTimeout(temporizador)
    sessao = null
    motivo = 'nenhum'
    versao = 0
    ouvintes.clear()
}
