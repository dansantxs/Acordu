/**
 * Sessão por link (RF-POR-08, decisão 2 do F7): o token da sessão restrita a uma solicitação fica **só nesta
 * memória**, separado da sessão com conta (`sessao.ts`). Não tem refresh nem vai para as outras abas: recarregar a
 * página pede o link do e-mail de novo, e um 401 encerra (link revogado, trocado por um mais novo ou vencido).
 */
export interface SessaoLink {
    accessToken: string
    expiraEm: string
    solicitacaoId: string
}

/** Por que não há sessão por link: nunca houve nesta aba, o link não valia, ou ele deixou de valer no meio do uso. */
export type MotivoSemLink = 'nenhum' | 'invalido' | 'expirado' | 'saiu'

let sessao: SessaoLink | null = null
let motivo: MotivoSemLink = 'nenhum'
let versao = 0
const ouvintes = new Set<() => void>()

function avisar(): void {
    versao++
    ouvintes.forEach((o) => o())
}

export function abrirSessaoLink(nova: SessaoLink): void {
    sessao = nova
    motivo = 'nenhum'
    avisar()
}

export function encerrarSessaoLink(novoMotivo: MotivoSemLink): void {
    if (!sessao && motivo === novoMotivo) return
    sessao = null
    motivo = novoMotivo
    avisar()
}

export function sessaoLink(): SessaoLink | null {
    return sessao
}

export function motivoSemLink(): MotivoSemLink {
    return motivo
}

export function assinarSessaoLink(ouvinte: () => void): () => void {
    ouvintes.add(ouvinte)
    return () => ouvintes.delete(ouvinte)
}

/** Para `useSyncExternalStore`: muda a cada abertura ou encerramento. */
export function versaoSessaoLink(): number {
    return versao
}

/** Só para testes. */
export function reiniciarSessaoLinkParaTestes(): void {
    sessao = null
    motivo = 'nenhum'
    versao = 0
    ouvintes.clear()
}
